using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Drives the decal mesh job pipeline for one receiver object: skinning + broad phase, clip, and mesh
    ///     build. Owned by <c>AirStickerSystem</c> and reused across launches (one launch runs at a time).
    /// </summary>
    /// <remarks>
    ///     Usage from the projector's launch body (two async segments with a cheap main-thread step between):
    ///     <code>
    ///     var h1 = pipeline.ScheduleClipStage(source, ...);   // segment 1
    ///     while (!h1.IsCompleted) await Awaitable.NextFrameAsync(); h1.Complete();
    ///     pipeline.CountBuild(source, decalMeshes);           // main thread
    ///     var h2 = pipeline.ScheduleBuildStage(source, decalMeshes, ...); // segment 2
    ///     while (!h2.IsCompleted) await Awaitable.NextFrameAsync(); h2.Complete();
    ///     pipeline.ApplyToDecalMeshes(decalMeshes);           // main thread merge + (caller) upload
    ///     </code>
    /// </remarks>
    internal sealed class DecalMeshJobPipeline : IDisposable
    {
        private const int SkinningBatch = 64;
        private const int ClipBatch = 16;

        private readonly DecalMeshJobBuffers _buffers = new DecalMeshJobBuffers();
        private NativeArray<float4> _clipPlanes = new NativeArray<float4>(6, Allocator.Persistent);

        // Per decal mesh (grown as needed).
        private NativeArray<int> _decalMeshComponentIndices;
        private NativeArray<int> _decalMeshVertexOffsets;
        private NativeArray<int> _decalMeshIndexOffsets;
        private NativeArray<int> _decalMeshVertexCounts;
        private NativeArray<int> _decalMeshIndexCounts;

        // Appended geometry outputs (grown as needed).
        private NativeArray<float3> _outPositions;
        private NativeArray<float3> _outNormals;
        private NativeArray<float2> _outUvs;
        private NativeArray<float4> _outTangents;
        private NativeArray<BoneWeight> _outBoneWeights;
        private NativeArray<int> _outIndices;
        private NativeArray<float3> _tangentAccumulation;
        private NativeArray<float3> _bitangentAccumulation;

        private int _decalMeshCount;
        // The handle of the most recently scheduled stage. Completed before disposing so a job that is still
        // running (e.g. when the scene is unloaded mid-launch) never reads freed NativeArrays.
        private JobHandle _lastScheduledHandle;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Ensure no scheduled job is still reading/writing the buffers before they are freed. Unity does
            // not guarantee that AirStickerProjector.OnDestroy (which completes its own handle) runs before
            // AirStickerSystem.OnDestroy (which calls this).
            _lastScheduledHandle.Complete();
            _buffers.Dispose();
            DisposeIfCreated(ref _clipPlanes);
            DisposeIfCreated(ref _decalMeshComponentIndices);
            DisposeIfCreated(ref _decalMeshVertexOffsets);
            DisposeIfCreated(ref _decalMeshIndexOffsets);
            DisposeIfCreated(ref _decalMeshVertexCounts);
            DisposeIfCreated(ref _decalMeshIndexCounts);
            DisposeIfCreated(ref _outPositions);
            DisposeIfCreated(ref _outNormals);
            DisposeIfCreated(ref _outUvs);
            DisposeIfCreated(ref _outTangents);
            DisposeIfCreated(ref _outBoneWeights);
            DisposeIfCreated(ref _outIndices);
            DisposeIfCreated(ref _tangentAccumulation);
            DisposeIfCreated(ref _bitangentAccumulation);
        }

        /// <summary>
        ///     Segment 1: build the per-component palette (main thread) and schedule the skinning/broad-phase
        ///     and clip jobs. Returns the handle of the clip job.
        /// </summary>
        public JobHandle ScheduleClipStage(
            ReceiverConvexPolygonsMesh source,
            float3 centerPositionOfDecalBox,
            float3 decalSpaceEx,
            float3 decalSpaceEy,
            float3 decalSpaceEz,
            float width,
            float height,
            float depth,
            bool projectionBackside)
        {
            var componentCount = source.ComponentCount;
            var triangleCount = source.TriangleCount;

            var totalBones = 0;
            for (var i = 0; i < componentCount; i++)
                if (source.ComponentByIndex[i] is SkinnedMeshRenderer smr && smr.rootBone != null &&
                    smr.sharedMesh != null)
                    totalBones += smr.bones.Length;

            _buffers.EnsureComponentCapacity(componentCount, totalBones);
            _buffers.EnsurePerTriangleCapacity(triangleCount);

            var boneCursor = 0;
            for (var i = 0; i < componentCount; i++)
            {
                var component = source.ComponentByIndex[i];
                var localToWorld = float4x4.identity;
                var existsRootBone = false;
                var boneOffset = -1;

                if (component is SkinnedMeshRenderer smr)
                {
                    if (smr.rootBone != null && smr.sharedMesh != null)
                    {
                        existsRootBone = true;
                        boneOffset = boneCursor;
                        var bindPoses = smr.sharedMesh.bindposes;
                        var bones = smr.bones;
                        for (var b = 0; b < bones.Length; b++)
                            _buffers.BoneMatrices[boneCursor++] = ToFloat4x4(bones[b].localToWorldMatrix * bindPoses[b]);
                    }

                    localToWorld = ToFloat4x4(smr.localToWorldMatrix);
                }
                else if (component is Renderer renderer)
                {
                    localToWorld = ToFloat4x4(renderer.localToWorldMatrix);
                }
                else if (component is Terrain terrain)
                {
                    localToWorld = ToFloat4x4(terrain.transform.localToWorldMatrix);
                }

                _buffers.ComponentLocalToWorld[i] = localToWorld;
                _buffers.ComponentExistsRootBone[i] = existsRootBone;
                _buffers.ComponentBoneMatrixOffset[i] = boneOffset;
            }

            BuildClipPlanes(centerPositionOfDecalBox, decalSpaceEx, decalSpaceEy, decalSpaceEz, width, height, depth);

            var radius = math.sqrt(width * width + height * height + depth * depth) * 0.5f;

            var skinningJob = new SkinningBroadPhaseJob
            {
                SourcePositionsMs = source.SourcePositionsMs,
                SourceBoneWeights = source.SourceBoneWeights,
                TriangleComponentIndices = source.TriangleComponentIndices,
                ComponentIsSkinned = source.ComponentIsSkinned,
                ComponentExistsRootBone = _buffers.ComponentExistsRootBone,
                ComponentLocalToWorld = _buffers.ComponentLocalToWorld,
                ComponentBoneMatrixOffset = _buffers.ComponentBoneMatrixOffset,
                BoneMatrices = _buffers.BoneMatrices,
                CenterPositionOfDecalBox = centerPositionOfDecalBox,
                DecalSpaceNormalWs = decalSpaceEz,
                Radius = radius,
                SqrRadius = radius * radius,
                ProjectionBackside = projectionBackside,
                WorldPositions = _buffers.WorldPositions,
                SurviveFlags = _buffers.SurviveFlags
            };
            var skinningHandle = skinningJob.Schedule(triangleCount, SkinningBatch);

            var clipJob = new ConvexPolygonClipJob
            {
                SurviveFlags = _buffers.SurviveFlags,
                WorldPositions = _buffers.WorldPositions,
                SourcePositionsMs = source.SourcePositionsMs,
                SourceNormalsMs = source.SourceNormalsMs,
                SourceBoneWeights = source.SourceBoneWeights,
                ClipPlanes = _clipPlanes,
                ClipWorldPositions = _buffers.ClipWorldPositions,
                ClipModelPositions = _buffers.ClipModelPositions,
                ClipModelNormals = _buffers.ClipModelNormals,
                ClipBoneWeights = _buffers.ClipBoneWeights,
                ClipVertexCounts = _buffers.ClipVertexCounts
            };
            _lastScheduledHandle = clipJob.Schedule(triangleCount, ClipBatch, skinningHandle);
            return _lastScheduledHandle;
        }

        /// <summary>
        ///     Main-thread step between the two segments: count the appended geometry of each decal mesh and
        ///     size the output buffers. Must be called after the clip job has completed.
        /// </summary>
        public void CountBuild(ReceiverConvexPolygonsMesh source, IList<DecalMesh> decalMeshes)
        {
            _decalMeshCount = decalMeshes.Count;
            EnsurePerDecalMeshCapacity(_decalMeshCount);

            var triangleCount = source.TriangleCount;
            var vertexCursor = 0;
            var indexCursor = 0;
            var maxDecalMeshVertexCount = 0;

            for (var dm = 0; dm < _decalMeshCount; dm++)
            {
                var componentIndex = source.IndexOfComponent(decalMeshes[dm].ReceiverComponent);
                _decalMeshComponentIndices[dm] = componentIndex;
                _decalMeshVertexOffsets[dm] = vertexCursor;
                _decalMeshIndexOffsets[dm] = indexCursor;

                var vertexCount = 0;
                var indexCount = 0;
                if (componentIndex >= 0)
                    for (var tri = 0; tri < triangleCount; tri++)
                    {
                        var vc = _buffers.ClipVertexCounts[tri];
                        if (vc < 3) continue;
                        if (source.TriangleComponentIndices[tri] != componentIndex) continue;
                        vertexCount += vc;
                        indexCount += (vc - 2) * 3;
                    }

                _decalMeshVertexCounts[dm] = vertexCount;
                _decalMeshIndexCounts[dm] = indexCount;
                vertexCursor += vertexCount;
                indexCursor += indexCount;
                maxDecalMeshVertexCount = math.max(maxDecalMeshVertexCount, vertexCount);
            }

            EnsureOutputCapacity(vertexCursor, indexCursor, maxDecalMeshVertexCount);
        }

        /// <summary>
        ///     Segment 2: schedule the (serial) mesh build job.
        /// </summary>
        public JobHandle ScheduleBuildStage(
            ReceiverConvexPolygonsMesh source,
            float3 decalSpaceOriginWs,
            float3 decalSpaceEx,
            float3 decalSpaceEy,
            float width,
            float height,
            float zOffsetInDecalSpace)
        {
            var job = new DecalMeshBuildJob
            {
                TriangleCount = source.TriangleCount,
                DecalMeshCount = _decalMeshCount,
                TriangleComponentIndices = source.TriangleComponentIndices,
                ClipWorldPositions = _buffers.ClipWorldPositions,
                ClipModelPositions = _buffers.ClipModelPositions,
                ClipModelNormals = _buffers.ClipModelNormals,
                ClipBoneWeights = _buffers.ClipBoneWeights,
                ClipVertexCounts = _buffers.ClipVertexCounts,
                DecalMeshComponentIndices = _decalMeshComponentIndices,
                DecalMeshVertexOffsets = _decalMeshVertexOffsets,
                DecalMeshIndexOffsets = _decalMeshIndexOffsets,
                DecalSpaceOriginWs = decalSpaceOriginWs,
                DecalSpaceTangentWs = decalSpaceEx,
                DecalSpaceBiNormalWs = decalSpaceEy,
                DecalWidth = width,
                DecalHeight = height,
                ZOffsetInDecalSpace = zOffsetInDecalSpace,
                OutPositions = _outPositions,
                OutNormals = _outNormals,
                OutUvs = _outUvs,
                OutTangents = _outTangents,
                OutBoneWeights = _outBoneWeights,
                OutIndices = _outIndices,
                TangentAccumulation = _tangentAccumulation,
                BitangentAccumulation = _bitangentAccumulation
            };
            _lastScheduledHandle = job.Schedule();
            return _lastScheduledHandle;
        }

        /// <summary>
        ///     Merge the built geometry into the decal meshes' CPU buffers. Must be called after the build
        ///     job has completed. The caller uploads each decal mesh afterwards.
        /// </summary>
        public void ApplyToDecalMeshes(IList<DecalMesh> decalMeshes)
        {
            for (var dm = 0; dm < _decalMeshCount; dm++)
                decalMeshes[dm].AppendFromJobOutput(
                    _outPositions, _outNormals, _outUvs, _outTangents, _outBoneWeights, _outIndices,
                    _decalMeshVertexOffsets[dm], _decalMeshVertexCounts[dm],
                    _decalMeshIndexOffsets[dm], _decalMeshIndexCounts[dm]);
        }

        private void BuildClipPlanes(float3 basePoint, float3 ex, float3 ey, float3 ez,
            float width, float height, float depth)
        {
            var halfDepth = depth * 0.5f;
            // Order must match the old ClipPlane enum (Left, Right, Bottom, Top, Front, Back).
            _clipPlanes[0] = new float4(ex, width / 2.0f - math.dot(ex, basePoint));
            _clipPlanes[1] = new float4(-ex, width / 2.0f + math.dot(ex, basePoint));
            _clipPlanes[2] = new float4(ey, height / 2.0f - math.dot(ey, basePoint));
            _clipPlanes[3] = new float4(-ey, height / 2.0f + math.dot(ey, basePoint));
            _clipPlanes[4] = new float4(-ez, halfDepth + math.dot(ez, basePoint));
            _clipPlanes[5] = new float4(ez, halfDepth - math.dot(ez, basePoint));
        }

        private void EnsurePerDecalMeshCapacity(int decalMeshCount)
        {
            EnsureCapacity(ref _decalMeshComponentIndices, decalMeshCount);
            EnsureCapacity(ref _decalMeshVertexOffsets, decalMeshCount);
            EnsureCapacity(ref _decalMeshIndexOffsets, decalMeshCount);
            EnsureCapacity(ref _decalMeshVertexCounts, decalMeshCount);
            EnsureCapacity(ref _decalMeshIndexCounts, decalMeshCount);
        }

        private void EnsureOutputCapacity(int vertexCount, int indexCount, int maxDecalMeshVertexCount)
        {
            EnsureCapacity(ref _outPositions, vertexCount);
            EnsureCapacity(ref _outNormals, vertexCount);
            EnsureCapacity(ref _outUvs, vertexCount);
            EnsureCapacity(ref _outTangents, vertexCount);
            EnsureCapacity(ref _outBoneWeights, vertexCount);
            EnsureCapacity(ref _outIndices, indexCount);
            EnsureCapacity(ref _tangentAccumulation, maxDecalMeshVertexCount);
            EnsureCapacity(ref _bitangentAccumulation, maxDecalMeshVertexCount);
        }

        private static void EnsureCapacity<T>(ref NativeArray<T> buffer, int requiredLength) where T : struct
        {
            if (buffer.IsCreated && buffer.Length >= requiredLength) return;

            var newLength = buffer.IsCreated
                ? math.max(requiredLength, buffer.Length * 2)
                : math.max(requiredLength, 1);
            if (buffer.IsCreated) buffer.Dispose();
            buffer = new NativeArray<T>(newLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private static void DisposeIfCreated<T>(ref NativeArray<T> buffer) where T : struct
        {
            if (buffer.IsCreated) buffer.Dispose();
        }

        private static float4x4 ToFloat4x4(Matrix4x4 m)
        {
            return new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3));
        }
    }
}
