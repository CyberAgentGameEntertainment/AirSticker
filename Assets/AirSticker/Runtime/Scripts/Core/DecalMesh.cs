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
        private BoneWeight[] _boneWeightsBuffer;
        private DecalMeshRenderer _decalMeshRenderer;
        private bool _disposed;

        private int[] _indexBuffer;
        private Mesh _mesh;
        private Vector3[] _normalBuffer;
        private int _numIndex;
        private int _numVertex;
        private Vector3[] _positionBuffer;
        private Vector4[] _tangentBuffer;
        private Vector2[] _uvBuffer;

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

        public void Dispose()
        {
            if (_disposed) return;
            if (_mesh && _mesh != null) Object.Destroy(_mesh);
            GC.SuppressFinalize(this);
            _disposed = true;
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
                meshData.GetIndexData<int>().CopyFrom(_indexBuffer);
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

        ~DecalMesh()
        {
            Dispose();
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
            Array.Resize(ref _positionBuffer, _numVertex);
            Array.Resize(ref _normalBuffer, _numVertex);
            Array.Resize(ref _boneWeightsBuffer, _numVertex);
            Array.Resize(ref _uvBuffer, _numVertex);
            Array.Resize(ref _tangentBuffer, _numVertex);
            for (var k = 0; k < vertexCount; k++)
            {
                _positionBuffer[addVertNo + k] = positions[vertexOffset + k];
                _normalBuffer[addVertNo + k] = normals[vertexOffset + k];
                _uvBuffer[addVertNo + k] = uvs[vertexOffset + k];
                _tangentBuffer[addVertNo + k] = tangents[vertexOffset + k];
                _boneWeightsBuffer[addVertNo + k] = boneWeights[vertexOffset + k];
            }

            _numIndex += indexCount;
            Array.Resize(ref _indexBuffer, _numIndex);
            for (var k = 0; k < indexCount; k++)
                _indexBuffer[addIndexNo + k] = indices[indexOffset + k] + indexDelta;
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
