using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Per-launch working buffers of the decal mesh job pipeline, pooled to avoid per-launch allocations.
    /// </summary>
    /// <remarks>
    ///     Reusing the buffers across launches is safe because <c>DecalProjectorLauncher</c> runs only one
    ///     launch at a time and each launch's jobs complete (and are copied out into the decal meshes) before
    ///     the next launch starts. The buffers only grow; they are released when the AirStickerSystem is
    ///     destroyed. This replaces the static pool that <c>BroadPhaseConvexPolygonsDetection</c> used, and its
    ///     lifetime is tied to the system so the memory is not held forever (old residual task 1).
    ///
    ///     The clip buffers are sized by the source triangle count (not the survivor count), so no compaction
    ///     pass is needed between the skinning and clip jobs. Dropping the per-vertex world normal and the
    ///     Line data shrank each vertex from ~252 B to 68 B, so even sized by triangle count these buffers are
    ///     smaller than the old survivor-sized pool.
    /// </remarks>
    internal sealed class DecalMeshJobBuffers : IDisposable
    {
        /// <summary>
        ///     Maximum number of vertices a convex polygon can grow to while being clipped by the six planes.
        ///     Matches the fixed stride of the clip working buffers.
        /// </summary>
        public const int MaxVertexCountPerConvexPolygon = 64;

        private bool _disposed;

        // --- Per source triangle vertex / triangle (skinning job outputs) ---
        // World-space positions of every triangle vertex (triangleCount * 3).
        public NativeArray<float3> WorldPositions;
        // 1 if the triangle survives the broad phase, 0 otherwise.
        public NativeArray<int> SurviveFlags;

        // --- Per receiver component (sized by componentCount / total bone count) ---
        public NativeArray<float4x4> ComponentLocalToWorld;
        public NativeArray<bool> ComponentExistsRootBone;
        // Start index into BoneMatrices for each skinned component, or -1 if the component has no bone palette.
        public NativeArray<int> ComponentBoneMatrixOffset;
        public NativeArray<float4x4> BoneMatrices;

        // --- Clip working set (sized by triangleCount; each triangle owns a fixed stride slot) ---
        public NativeArray<float3> ClipWorldPositions;
        public NativeArray<float3> ClipModelPositions;
        public NativeArray<float3> ClipModelNormals;
        public NativeArray<BoneWeight> ClipBoneWeights;
        // Final vertex count of each triangle's convex polygon after clipping. 0 means the polygon was
        // rejected by the broad phase or fully clipped away, i.e. it contributes no geometry.
        public NativeArray<int> ClipVertexCounts;

        public void EnsurePerTriangleCapacity(int triangleCount)
        {
            var vertexCount = triangleCount * ReceiverConvexPolygonsMesh.VerticesPerTriangle;
            var clipVertexCount = triangleCount * MaxVertexCountPerConvexPolygon;
            EnsureCapacity(ref WorldPositions, vertexCount);
            EnsureCapacity(ref SurviveFlags, triangleCount);
            EnsureCapacity(ref ClipWorldPositions, clipVertexCount);
            EnsureCapacity(ref ClipModelPositions, clipVertexCount);
            EnsureCapacity(ref ClipModelNormals, clipVertexCount);
            EnsureCapacity(ref ClipBoneWeights, clipVertexCount);
            EnsureCapacity(ref ClipVertexCounts, triangleCount);
        }

        public void EnsureComponentCapacity(int componentCount, int boneMatrixCount)
        {
            EnsureCapacity(ref ComponentLocalToWorld, math.max(1, componentCount));
            EnsureCapacity(ref ComponentExistsRootBone, math.max(1, componentCount));
            EnsureCapacity(ref ComponentBoneMatrixOffset, math.max(1, componentCount));
            EnsureCapacity(ref BoneMatrices, math.max(1, boneMatrixCount));
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DisposeIfCreated(ref WorldPositions);
            DisposeIfCreated(ref SurviveFlags);
            DisposeIfCreated(ref ComponentLocalToWorld);
            DisposeIfCreated(ref ComponentExistsRootBone);
            DisposeIfCreated(ref ComponentBoneMatrixOffset);
            DisposeIfCreated(ref BoneMatrices);
            DisposeIfCreated(ref ClipWorldPositions);
            DisposeIfCreated(ref ClipModelPositions);
            DisposeIfCreated(ref ClipModelNormals);
            DisposeIfCreated(ref ClipBoneWeights);
            DisposeIfCreated(ref ClipVertexCounts);
        }

        private static void DisposeIfCreated<T>(ref NativeArray<T> buffer) where T : struct
        {
            if (buffer.IsCreated) buffer.Dispose();
        }
    }
}
