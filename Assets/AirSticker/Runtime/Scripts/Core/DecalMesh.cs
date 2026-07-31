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
        private int _numIndexOnSnapshot;
        private int _numVertex;
        private int _numVertexOnSnapshot;
        private Vector3[] _positionBuffer;
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

            _mesh.SetVertices(_positionBuffer);
            _mesh.SetIndices(_indexBuffer, MeshTopology.Triangles, 0);
            _mesh.SetNormals(_normalBuffer, 0, _numVertex);
            if (_bindPoses != null && _bindPoses.Length > 0)
            {
                _mesh.boneWeights = _boneWeightsBuffer;
                _mesh.bindposes = _bindPoses;
            }

            _mesh.SetUVs(0, _uvBuffer);
            // RecalculateTangents depends on UV0, so it must be called after SetUVs.
            _mesh.RecalculateTangents();
            // Mesh.Optimize() is intentionally not called here. The index buffer is already emitted
            // in sequential triangle-fan order, and the mesh is re-uploaded on every launch,
            // so Optimize() only adds a per-launch main-thread cost that grows with the vertex count.
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
            _numIndexOnSnapshot = 0;
            _numVertexOnSnapshot = 0;
            Object.Destroy(_mesh);
            _decalMeshRenderer = null;
            _mesh = new Mesh();
        }

        /// <summary>
        ///     Snapshot the sizes of the CPU-side buffers.
        ///     Taken just before the worker thread appends geometry,
        ///     so that a canceled launch can be rolled back (see RollbackAppendedGeometry).
        /// </summary>
        internal void SnapshotBufferSizes()
        {
            _numVertexOnSnapshot = _numVertex;
            _numIndexOnSnapshot = _numIndex;
        }

        /// <summary>
        ///     Roll back the CPU-side buffers to the last snapshot.
        /// </summary>
        /// <remarks>
        ///     A launch that is canceled after its worker thread ran (the projector was destroyed,
        ///     or the worker thread failed) leaves the appended geometry in the buffers without
        ///     uploading it. Because the decal mesh is pooled and shared, that geometry would be
        ///     uploaded by the next launch unless it is discarded here.
        ///     Must be called on the main thread after the worker thread has finished.
        /// </remarks>
        internal void RollbackAppendedGeometry()
        {
            // Nothing was appended after the snapshot (or the mesh was cleared), so nothing to roll back.
            if (_numVertex <= _numVertexOnSnapshot) return;

            _numVertex = _numVertexOnSnapshot;
            _numIndex = _numIndexOnSnapshot;
            // Keep the invariant that the buffer lengths equal the vertex/index counts,
            // because the upload passes the whole arrays to the Unity Mesh API.
            Array.Resize(ref _positionBuffer, _numVertex);
            Array.Resize(ref _normalBuffer, _numVertex);
            Array.Resize(ref _boneWeightsBuffer, _numVertex);
            Array.Resize(ref _uvBuffer, _numVertex);
            Array.Resize(ref _indexBuffer, _numIndex);
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
