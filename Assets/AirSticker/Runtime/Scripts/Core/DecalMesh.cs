using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Decal mesh.
    ///     Its instance will be created by AirStickerProjector.
    /// </summary>
    public sealed class DecalMesh : IDisposable
    {
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
        private Vector2[] _uvBuffer;

        public DecalMesh(
            GameObject receiverObject,
            Material decalMaterial,
            Component receiverComponent)
        {
            _mesh = new Mesh();
            _receiverComponent = receiverComponent;
            _decalMaterial = decalMaterial;
            _receiverObject = receiverObject;

            if (_receiverComponent is SkinnedMeshRenderer skinnedMeshRenderer)
                _bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
        }

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

            _mesh.SetVertices(_positionBuffer);
            _mesh.SetIndices(_indexBuffer, MeshTopology.Triangles, 0);
            _mesh.SetNormals(_normalBuffer, 0, _numVertex);
            if (_bindPoses != null && _bindPoses.Length > 0)
            {
                _mesh.boneWeights = _boneWeightsBuffer;
                _mesh.bindposes = _bindPoses;
            }

            _mesh.RecalculateTangents();
            _mesh.SetUVs(0, _uvBuffer);
            _mesh.Optimize();
            _mesh.RecalculateBounds();

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

        public void DisableDecalMeshRenderer()
        {
            _decalMeshRenderer?.DisableDecalMeshRenderer();
        }

        public void EnableDecalMeshRenderer()
        {
            _decalMeshRenderer?.EnableDecalMeshRenderer();
        }

        /// <summary>
        ///     Add triangle polygons to decal mesh from convex polygons.
        /// </summary>
        public void AddTrianglePolygonsToDecalMesh(
            List<ConvexPolygon> convexPolygons,
            Vector3 decalSpaceOriginPosInWorldSpace,
            Vector3 decalSpaceTangentInWorldSpace,
            Vector3 decalSpaceBiNormalInWorldSpace,
            float decalSpaceWidth,
            float decalSpaceHeight,
            float zOffsetInDecalSpace
        )
        {
            if (!_receiverComponent) return;

            var uv = new Vector2();
            // Calculate the vertex count and the index count to be added.
            var deltaVertex = 0;
            var deltaIndex = 0;
            foreach (var convexPolygon in convexPolygons)
            {
                if (convexPolygon.ReceiverComponent != _receiverComponent) continue;
                deltaVertex += convexPolygon.VertexCount;
                // Index count increases with the number of triangles*3
                deltaIndex += (convexPolygon.VertexCount - 2) * 3;
            }

            var addVertNo = _numVertex;
            var addIndexNo = _numIndex;
            var indexBase = addVertNo;
            // Expand the vertex buffer.
            _numVertex += deltaVertex;
            Array.Resize(ref _positionBuffer, _numVertex);
            Array.Resize(ref _normalBuffer, _numVertex);
            Array.Resize(ref _boneWeightsBuffer, _numVertex);
            Array.Resize(ref _uvBuffer, _numVertex);

            // Expand the index buffer.
            _numIndex += deltaIndex;
            Array.Resize(ref _indexBuffer, _numIndex);

            foreach (var convexPolygon in convexPolygons)
            {
                if (convexPolygon.ReceiverComponent != _receiverComponent) continue;

                var numVertex = convexPolygon.VertexCount;
                for (var localVertNo = 0; localVertNo < numVertex; localVertNo++)
                {
                    var vertNo = convexPolygon.GetRealVertexNo(localVertNo);
                    var vertPos = convexPolygon.GetVertexPositionInWorldSpace(vertNo);

                    var decalSpaceToVertPos = vertPos - decalSpaceOriginPosInWorldSpace;

                    uv.x = Vector3.Dot(decalSpaceTangentInWorldSpace, decalSpaceToVertPos) / decalSpaceWidth + 0.5f;
                    uv.y = Vector3.Dot(decalSpaceBiNormalInWorldSpace, decalSpaceToVertPos) / decalSpaceHeight +
                           0.5f;
                    _uvBuffer[addVertNo] = uv;
                    // Convert position and rotation to parent space.
                    vertPos = convexPolygon.GetVertexPositionInModelSpace(vertNo);
                    var normal = convexPolygon.GetVertexNormalInModelSpace(vertNo);

                    // Add a slight offset in the opposite direction of the decal projection to avoid Z-fighting.
                    // TODO: This number can be adjusted later.
                    vertPos += normal * zOffsetInDecalSpace;
                    _positionBuffer[addVertNo] = vertPos;
                    _normalBuffer[addVertNo] = normal;
                    _boneWeightsBuffer[addVertNo] = convexPolygon.GetVertexBoneWeight(vertNo);
                    addVertNo++;
                }

                // The convex polygon is constructed by the number of vertices - 2 triangles.
                var numTriangle = numVertex - 2;
                for (var triNo = 0; triNo < numTriangle; triNo++)
                {
                    _indexBuffer[addIndexNo++] = indexBase;
                    _indexBuffer[addIndexNo++] = indexBase + triNo + 1;
                    _indexBuffer[addIndexNo++] = indexBase + triNo + 2;
                }

                indexBase += numVertex;
            }
        }
    }
}
