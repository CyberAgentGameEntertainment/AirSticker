using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Decal mesh.
    ///     Its instance will be created by AirStickerProjector.
    /// </summary>
    public sealed class DecalMesh : IDisposable
    {
        // Interleaved vertex layouts for the writable MeshData API.
        // The attributes must be declared in ascending order of the VertexAttribute enum.
        private static readonly VertexAttributeDescriptor[] StaticMeshVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        // A skinned mesh is not allowed to interleave all attributes into one stream. Unity
        // requires the attributes that are deformed by skinning (Position/Normal/Tangent) in
        // stream 0, the static attributes (TexCoord0) in stream 1, and the skinning data
        // (BlendWeight/BlendIndices) in stream 2, and rejects other layouts with the error
        // "Skinned mesh attributes use wrong streams".
        private static readonly VertexAttributeDescriptor[] SkinnedMeshVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1),
            new VertexAttributeDescriptor(VertexAttribute.BlendWeight, VertexAttributeFormat.Float32, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.BlendIndices, VertexAttributeFormat.UInt32, 4, 2)
        };

        private readonly Matrix4x4[] _bindPoses;
        private readonly Material _decalMaterial;
        private readonly Component _receiverComponent;
        private readonly GameObject _receiverObject;
        private DecalMeshRenderer _decalMeshRenderer;
        private bool _disposed;
        private Mesh _mesh;

        // The CPU-side geometry, accumulated across every launch that projects onto this mesh.
        // They are NativeArrays with doubling capacity rather than managed arrays sized exactly to
        // _numVertex / _numIndex: Array.Resize allocates a new array of the exact length on every append, so
        // pasting a second decal onto the same mesh copied the first decal's vertices into fresh garbage, and
        // the garbage grew quadratically with the number of decals. Native memory is not counted as GC.Alloc,
        // and doubling makes the reallocations amortized-constant.
        // Only the main thread touches them (append and upload run after the jobs have completed), so no job
        // dependency has to be tracked; Dispose is the only owner (see the remarks on Dispose).
        private NativeArray<float3> _positionBuffer;
        private NativeArray<float3> _normalBuffer;
        private NativeArray<float2> _uvBuffer;
        private NativeArray<float4> _tangentBuffer;
        private NativeArray<BoneWeight> _boneWeightsBuffer;
        private NativeArray<int> _indexBuffer;

        // The logical lengths of the buffers above. They are independent of the buffers' capacity.
        private int _numIndex;
        private int _numVertex;

        public DecalMesh(
            GameObject receiverObject,
            Material decalMaterial,
            Component receiverComponent,
            int groupId = 0)
        {
            _mesh = new Mesh();
            _receiverComponent = receiverComponent;
            _decalMaterial = decalMaterial;
            _receiverObject = receiverObject;
            GroupId = groupId;

            if (_receiverComponent is SkinnedMeshRenderer skinnedMeshRenderer)
                _bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
        }

        /// <summary>
        ///     The group ID that was specified when the decal was projected.
        /// </summary>
        public int GroupId { get; }

        internal GameObject ReceiverObject => _receiverObject;
        internal Material DecalMaterial => _decalMaterial;
        internal Component ReceiverComponent => _receiverComponent;

        /// <summary>
        ///     Destroy the mesh and free the CPU-side buffers.
        /// </summary>
        /// <remarks>
        ///     There is deliberately no finalizer to fall back on. Object.Destroy and NativeArray.Dispose are
        ///     both unsafe on the finalizer thread (the implementation before the buffers became NativeArrays
        ///     called Dispose from one), so a finalizer could not release anything here. Every DecalMesh is
        ///     owned by DecalMeshPool, which disposes it from GarbageCollect / RemoveDecalMeshes / Dispose, so
        ///     a missed Dispose means the pool was bypassed; the editor's Native Collection leak detection
        ///     reports the buffers in that case.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_mesh && _mesh != null) Object.Destroy(_mesh);
            DisposeIfCreated(ref _positionBuffer);
            DisposeIfCreated(ref _normalBuffer);
            DisposeIfCreated(ref _uvBuffer);
            DisposeIfCreated(ref _tangentBuffer);
            DisposeIfCreated(ref _boneWeightsBuffer);
            DisposeIfCreated(ref _indexBuffer);
        }

        /// <summary>
        ///     Post-processing with results of worker thread execution.<br />
        ///     1. Create the decal mesh.<br />
        ///     2. Create the decal mesh renderer.<br />
        /// </summary>
        public void ExecutePostProcessingAfterWorkerThread()
        {
            _decalMeshRenderer?.Destroy();

            if (_numVertex <= 0) return;

            System.Diagnostics.Stopwatch swUpload = null;
            if (AirStickerPerformanceLog.Enabled) swUpload = System.Diagnostics.Stopwatch.StartNew();

            // Build the vertex/index buffers through the writable MeshData API so that they are
            // constructed in one pass and uploaded with a single Apply call. Tangents are computed
            // on the worker thread (see AddTrianglePolygonsToDecalMesh), so no Recalculate* call is
            // needed here except for the bounds. Step 3 of the Job System migration will move this
            // buffer construction into the job chain.
            // Mesh.Optimize() is intentionally not called here. The index buffer is already emitted
            // in sequential triangle-fan order, and the mesh is re-uploaded on every launch,
            // so Optimize() only adds a per-launch main-thread cost that grows with the vertex count.
            var isSkinned = _bindPoses != null && _bindPoses.Length > 0;
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArray[0];

            // Set both buffer sizes before acquiring any data view, as the official samples do,
            // because changing the buffer params can invalidate previously acquired views.
            meshData.SetVertexBufferParams(_numVertex, isSkinned ? SkinnedMeshVertexLayout : StaticMeshVertexLayout);
            var indexFormat = _numVertex > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            meshData.SetIndexBufferParams(_numIndex, indexFormat);

            if (isSkinned)
            {
                // The skinning weights are stored as vertex attributes (BlendWeight/BlendIndices)
                // instead of the Mesh.boneWeights property, so that the whole vertex buffer is
                // built in one pass without a re-layout on assignment.
                var deformedVertices = meshData.GetVertexData<SkinnedMeshDeformedVertex>();
                var uvs = meshData.GetVertexData<Vector2>(1);
                var blendVertices = meshData.GetVertexData<SkinnedMeshBlendVertex>(2);
                for (var i = 0; i < _numVertex; i++)
                {
                    deformedVertices[i] = new SkinnedMeshDeformedVertex
                    {
                        Position = _positionBuffer[i],
                        Normal = _normalBuffer[i],
                        Tangent = _tangentBuffer[i]
                    };
                    uvs[i] = _uvBuffer[i];
                    var boneWeight = _boneWeightsBuffer[i];
                    blendVertices[i] = new SkinnedMeshBlendVertex
                    {
                        BlendWeights = new Vector4(
                            boneWeight.weight0,
                            boneWeight.weight1,
                            boneWeight.weight2,
                            boneWeight.weight3),
                        BlendIndex0 = (uint)boneWeight.boneIndex0,
                        BlendIndex1 = (uint)boneWeight.boneIndex1,
                        BlendIndex2 = (uint)boneWeight.boneIndex2,
                        BlendIndex3 = (uint)boneWeight.boneIndex3
                    };
                }
            }
            else
            {
                var vertices = meshData.GetVertexData<StaticMeshVertex>();
                for (var i = 0; i < _numVertex; i++)
                    vertices[i] = new StaticMeshVertex
                    {
                        Position = _positionBuffer[i],
                        Normal = _normalBuffer[i],
                        Tangent = _tangentBuffer[i],
                        Uv = _uvBuffer[i]
                    };
            }
            if (indexFormat == IndexFormat.UInt16)
            {
                var indices = meshData.GetIndexData<ushort>();
                for (var i = 0; i < _numIndex; i++) indices[i] = (ushort)_indexBuffer[i];
            }
            else
            {
                // Copied by range instead of CopyFrom, because the buffer's capacity is larger than _numIndex.
                NativeArray<int>.Copy(_indexBuffer, 0, meshData.GetIndexData<int>(), 0, _numIndex);
            }

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, _numIndex));

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _mesh);
            if (isSkinned) _mesh.bindposes = _bindPoses;
            _mesh.RecalculateBounds();

            if (swUpload != null)
            {
                swUpload.Stop();
                Debug.Log($"[AirSticker][Perf] ExecutePostProcessingAfterWorkerThread (mesh upload): {swUpload.Elapsed.TotalMilliseconds:F2} ms (vertices={_numVertex})");
            }

            _decalMeshRenderer = new DecalMeshRenderer(
                _receiverComponent,
                _decalMaterial,
                _mesh);
        }

        /// <summary>
        ///     Check to can the decal mesh remove from the pool.
        ///     If this function return true, it will be removed from the pool.
        /// </summary>
        /// <returns></returns>
        public bool CanRemoveFromPool()
        {
            return !_decalMaterial
                   || !_receiverComponent
                   || !_receiverObject;
        }

        /// <summary>
        ///     Clear the decal mesh.
        /// </summary>
        public void Clear()
        {
            _decalMeshRenderer?.Destroy();
            // The logical lengths are reset but the buffers keep their capacity, so re-pasting onto this mesh
            // does not have to grow them again.
            _numIndex = 0;
            _numVertex = 0;
            Object.Destroy(_mesh);
            _decalMeshRenderer = null;
            _mesh = new Mesh();
        }

        /// <summary>
        ///     Destroy the decal mesh renderer that was spawned under the receiver object.
        /// </summary>
        internal void DestroyDecalMeshRenderer()
        {
            _decalMeshRenderer?.Destroy();
            _decalMeshRenderer = null;
        }

        public void DisableDecalMeshRenderer()
        {
            _decalMeshRenderer?.DisableDecalMeshRenderer();
        }

        public void EnableDecalMeshRenderer()
        {
            _decalMeshRenderer?.EnableDecalMeshRenderer();
        }

        /// <summary>
        ///     Append this launch's built geometry (produced by <see cref="DecalMeshBuildJob" />) to the
        ///     CPU-side buffers. Called on the main thread after the build job has completed.
        /// </summary>
        /// <remarks>
        ///     The indices in the job output are in the output array's space (they index Out* directly), so
        ///     they are shifted by (existing vertex count - vertexOffset) to reference this mesh's full vertex
        ///     buffer, which accumulates across launches.
        /// </remarks>
        internal void AppendFromJobOutput(
            NativeArray<float3> positions,
            NativeArray<float3> normals,
            NativeArray<float2> uvs,
            NativeArray<float4> tangents,
            NativeArray<BoneWeight> boneWeights,
            NativeArray<int> indices,
            int vertexOffset,
            int vertexCount,
            int indexOffset,
            int indexCount)
        {
            if (!_receiverComponent || vertexCount <= 0) return;

            var indexDelta = _numVertex - vertexOffset;
            var addVertNo = _numVertex;
            var addIndexNo = _numIndex;

            _numVertex += vertexCount;
            EnsureCapacity(ref _positionBuffer, _numVertex, addVertNo);
            EnsureCapacity(ref _normalBuffer, _numVertex, addVertNo);
            EnsureCapacity(ref _uvBuffer, _numVertex, addVertNo);
            EnsureCapacity(ref _tangentBuffer, _numVertex, addVertNo);
            EnsureCapacity(ref _boneWeightsBuffer, _numVertex, addVertNo);
            NativeArray<float3>.Copy(positions, vertexOffset, _positionBuffer, addVertNo, vertexCount);
            NativeArray<float3>.Copy(normals, vertexOffset, _normalBuffer, addVertNo, vertexCount);
            NativeArray<float2>.Copy(uvs, vertexOffset, _uvBuffer, addVertNo, vertexCount);
            NativeArray<float4>.Copy(tangents, vertexOffset, _tangentBuffer, addVertNo, vertexCount);
            NativeArray<BoneWeight>.Copy(boneWeights, vertexOffset, _boneWeightsBuffer, addVertNo, vertexCount);

            _numIndex += indexCount;
            EnsureCapacity(ref _indexBuffer, _numIndex, addIndexNo);
            // Not a bulk copy, because every index has to be shifted into this mesh's vertex space.
            for (var k = 0; k < indexCount; k++)
                _indexBuffer[addIndexNo + k] = indices[indexOffset + k] + indexDelta;
        }

        /// <summary>
        ///     Make sure the buffer can hold <paramref name="requiredLength" /> elements, preserving the first
        ///     <paramref name="copyLength" /> of them.
        /// </summary>
        /// <remarks>
        ///     The capacity is doubled rather than grown to exactly the required length, so that appending to a
        ///     mesh that already holds decals stays amortized-constant instead of copying the whole buffer on
        ///     every append.
        /// </remarks>
        private static void EnsureCapacity<T>(ref NativeArray<T> buffer, int requiredLength, int copyLength)
            where T : struct
        {
            if (buffer.IsCreated && buffer.Length >= requiredLength) return;

            var newLength = buffer.IsCreated
                ? math.max(requiredLength, buffer.Length * 2)
                : math.max(requiredLength, 1);
            var newBuffer = new NativeArray<T>(newLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            if (buffer.IsCreated)
            {
                if (copyLength > 0) NativeArray<T>.Copy(buffer, 0, newBuffer, 0, copyLength);
                buffer.Dispose();
            }

            buffer = newBuffer;
        }

        private static void DisposeIfCreated<T>(ref NativeArray<T> buffer) where T : struct
        {
            if (buffer.IsCreated) buffer.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StaticMeshVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Tangent;
            public Vector2 Uv;
        }

        // Stream 0 of the skinned mesh layout. The UVs (stream 1) are written as raw Vector2
        // and the skinning data lives in stream 2 (see SkinnedMeshVertexLayout).
        [StructLayout(LayoutKind.Sequential)]
        private struct SkinnedMeshDeformedVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Tangent;
        }

        // Stream 2 of the skinned mesh layout.
        [StructLayout(LayoutKind.Sequential)]
        private struct SkinnedMeshBlendVertex
        {
            public Vector4 BlendWeights;
            public uint BlendIndex0;
            public uint BlendIndex1;
            public uint BlendIndex2;
            public uint BlendIndex3;
        }
    }
}
