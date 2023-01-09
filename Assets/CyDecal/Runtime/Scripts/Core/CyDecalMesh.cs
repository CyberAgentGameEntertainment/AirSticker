using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカールメッシュ
    /// </summary>
    public sealed class CyDecalMesh : IDisposable
    {
        private readonly Matrix4x4[] _bindPoses; // バインドポーズ行列
        private readonly Material _decalMaterial;
        private readonly Renderer _receiverMeshRenderer;
        private readonly GameObject _receiverObject; // デカールメッシュを貼り付けるレシーバーオブジェクト
        private BoneWeight[] _boneWeightsBuffer;
        private CyDecalMeshRenderer _decalMeshRenderer;

        private int[] _indexBuffer;
        private Mesh _mesh; // デカールテクスチャを貼り付けるためのデカールメッシュ
        private Vector3[] _normalBuffer;
        private int _numIndex;
        private int _numVertex;
        private Vector3[] _positionBuffer;
        private Vector2[] _uvBuffer;
        private bool _disposed = false;
        private Matrix4x4 toReceiverMeshRendererSpace;

        public void PrepareAddPolygonsToDecalMesh()
        {
            toReceiverMeshRendererSpace = _receiverMeshRenderer.worldToLocalMatrix;
        }

        public void PostProcessAddPolygonsToDecalMesh()
        {
            // デカールメッシュレンダラーを作成。
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

            _decalMeshRenderer = new CyDecalMeshRenderer(
                _receiverMeshRenderer,
                _decalMaterial,
                _mesh);
        }
        public CyDecalMesh(
            GameObject receiverObject,
            Material decalMaterial,
            Renderer receiverMeshRenderer)
        {
            _mesh = new Mesh();
            _receiverMeshRenderer = receiverMeshRenderer;
            _decalMaterial = decalMaterial;
            _receiverObject = receiverObject;

            if (_receiverMeshRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
                _bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
        }

        ~CyDecalMesh()
        {
            Dispose();
        }

        /// <summary>
        ///     プールから削除可能？
        /// </summary>
        /// <returns></returns>
        public bool CanRemoveFromPool()
        {
            // プールのキーとなっているオブジェクトのうち、一つでも死亡していたらプールから削除可能。
            return !_decalMaterial
                   || !_receiverMeshRenderer
                   || !_receiverObject;
        }

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
        ///     デカールメッシュレンダラーを無効にする。
        /// </summary>
        public void DisableDecalMeshRenderer()
        {
            _decalMeshRenderer?.DisableDecalMeshRenderer();
        }

        /// <summary>
        ///     デカールメッシュレンダラーを有効にする。
        /// </summary>
        public void EnableDecalMeshRenderer()
        {
            _decalMeshRenderer?.EnableDecalMeshRenderer();
        }

        /// <summary>
        ///     三角形ポリゴンをデカールメッシュを追加。
        /// </summary>
        /// <remarks>
        ///     凸ポリゴン情報から三角形ポリゴンの情報を追加で構築し、デカールメッシュに追加します。
        /// </remarks>
        /// <param name="convexPolygons">凸ポリゴンのリスト</param>
        /// <param name="decalSpaceOriginPosWS">デカールスペースの原点(ワールド空間)</param>
        /// <param name="decalSpaceNormalWS">デカールスペースの法線(ワールド空間)</param>
        /// <param name="decalSpaceTangentWS">デカールスペースの接ベクトル(ワールド空間)</param>
        /// <param name="decalSpaceBiNormalWS">デカールスペースの従ベクトル(ワールド空間)</param>
        /// <param name="decalSpaceWidth">デカールスペースの幅</param>
        /// <param name="decalSpaceHeight">デカールスペースの高さ</param>
        public void AddPolygonsToDecalMesh(
            List<CyConvexPolygon> convexPolygons,
            Vector3 decalSpaceOriginPosWS,
            Vector3 decalSpaceNormalWS,
            Vector3 decalSpaceTangentWS,
            Vector3 decalSpaceBiNormalWS,
            float decalSpaceWidth,
            float decalSpaceHeight 
        )
        {
            if (!_receiverMeshRenderer) return;
            
            var uv = new Vector2();
            // 増える頂点数とインデックス数を計算する
            var deltaVertex = 0;
            var deltaIndex = 0;
            foreach (var convexPolygon in convexPolygons)
            {
                if (convexPolygon.ReceiverMeshRenderer != _receiverMeshRenderer) continue;
                deltaVertex += convexPolygon.VertexCount;
                // インデックスバッファが増えるのは三角形の数＊３
                deltaIndex += (convexPolygon.VertexCount - 2) * 3;
            }

            var addVertNo = _numVertex;
            var addIndexNo = _numIndex;
            var indexBase = addVertNo;
            // 頂点バッファを拡張。
            _numVertex += deltaVertex;
            Array.Resize(ref _positionBuffer, _numVertex);
            Array.Resize(ref _normalBuffer, _numVertex);
            Array.Resize(ref _boneWeightsBuffer, _numVertex);
            Array.Resize(ref _uvBuffer, _numVertex);

            // インデックスバッファを拡張
            _numIndex += deltaIndex;
            Array.Resize(ref _indexBuffer, _numIndex);

            foreach (var convexPolygon in convexPolygons)
            {
                if (convexPolygon.ReceiverMeshRenderer != _receiverMeshRenderer) continue;

                var numVertex = convexPolygon.VertexCount;
                for (var vertNo = 0; vertNo < numVertex; vertNo++)
                {
                    var vertPos = convexPolygon.GetVertexPosition(vertNo);
                    var normal = convexPolygon.GetVertexNormal(vertNo);

                    // Zファイティング回避のために、デカールの投影方向の逆向きに少しオフセットを加える。
                    // TODO: この数値は後で調整できるようにする。
                    // vertPos += decalSpaceNormalWS * 0.001f;
                    var decalSpaceToVertPos = vertPos - decalSpaceOriginPosWS;

                    uv.x = Vector3.Dot(decalSpaceTangentWS, decalSpaceToVertPos) / decalSpaceWidth + 0.5f;
                    uv.y = Vector3.Dot(decalSpaceBiNormalWS, decalSpaceToVertPos) / decalSpaceHeight +
                           0.5f;
                    _uvBuffer[addVertNo] = uv;
                    // 座標と回転を親の空間に変換する。
                    // vertPos = toReceiverMeshRendererSpace.MultiplyPoint3x4(vertPos);
                    // normal = toReceiverMeshRendererSpace.MultiplyVector(normal);
                    vertPos = convexPolygon.GetVertexLocalPosition(vertNo);
                    normal = convexPolygon.GetVertexLocalNormal(vertNo);

                    vertPos += normal * 0.005f;
                    _positionBuffer[addVertNo] = vertPos;
                    _normalBuffer[addVertNo] = normal;
                    _boneWeightsBuffer[addVertNo] = convexPolygon.GetVertexBoneWeight(vertNo);
                    addVertNo++;
                }

                // 多角形は頂点数-2の三角形によって構築されている。
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

        public void Dispose()
        {
            if (_disposed) return;
            if (_mesh && _mesh != null) Object.Destroy(_mesh);
            GC.SuppressFinalize(this);
            _disposed = true;
        }
    }
}
