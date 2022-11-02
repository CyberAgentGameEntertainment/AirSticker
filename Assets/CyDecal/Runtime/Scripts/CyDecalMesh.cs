using System.Collections.Generic;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールメッシュ
    /// </summary>
    public class CyDecalMesh
    {
        private readonly Matrix4x4[] _bindPoses;
        private readonly List<BoneWeight> _boneWeightsBuffer = new();
        private readonly Material _decalMaterial;
        private readonly List<int> _indexBuffer = new(); // インデックスバッファ
        private readonly Mesh _mesh; // デカールテクスチャを貼り付けるためのデカールメッシュ
        private readonly List<Vector3> _normalBuffer = new(); // 法線。
        private readonly List<Vector3> _positionBuffer = new(); // 頂点座標のバッファ
        private readonly Renderer _receiverMeshRenderer;
        private readonly List<Vector2> _uvBuffer = new(); // UVバッファ
        private CyDecalMeshRenderer _decalMeshRenderer;
        private int _indexBase;

        public CyDecalMesh(
            GameObject projectorObject,
            Material decalMaterial,
            Renderer receiverMeshRenderer)
        {
            _mesh = new Mesh();
            _receiverMeshRenderer = receiverMeshRenderer;
            _decalMaterial = decalMaterial;
            if (_receiverMeshRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
                _bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
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
        /// <param name="decalSPaceOriginPosWS">デカールスペースの原点(ワールド空間)</param>
        /// <param name="decalSpaceNormalWS">デカールスペースの法線(ワールド空間)</param>
        /// <param name="decalSpaceTangentWS">デカールスペースの接ベクトル(ワールド空間)</param>
        /// <param name="decalSpaceBiNormalWS">デカールスペースの従ベクトル(ワールド空間)</param>
        /// <param name="decalSpaceWidth">デカールスペースの幅</param>
        /// <param name="decalSpaceHeight">デカールスペースの高さ</param>
        public void AddPolygonsToDecalMesh(
            List<CyConvexPolygon> convexPolygons,
            Vector3 decalSPaceOriginPosWS,
            Vector3 decalSpaceNormalWS,
            Vector3 decalSpaceTangentWS,
            Vector3 decalSpaceBiNormalWS,
            float decalSpaceWidth,
            float decalSpaceHeight
        )
        {
            var toReceiverObjectSpaceMatrix = _receiverMeshRenderer.transform.worldToLocalMatrix;
            var uv = new Vector2();
            foreach (var convexPolygon in convexPolygons)
            {
                if (convexPolygon.ReceiverMeshRenderer != _receiverMeshRenderer) continue;

                var numVertex = convexPolygon.NumVertices;
                for (var vertNo = 0; vertNo < numVertex; vertNo++)
                {
                    var vertPos = convexPolygon.GetVertexPosition(vertNo);
                    var normal = convexPolygon.GetVertexNormal(vertNo);

                    // Zファイティング回避のために、デカールの投影方向の逆向きに少しオフセットを加える。
                    // TODO: この数値は後で調整できるようにする。
                    // vertPos += decalSpaceNormalWS * 0.001f;
                    uv.x = Vector3.Dot(decalSpaceTangentWS, vertPos - decalSPaceOriginPosWS) / decalSpaceWidth + 0.5f;
                    uv.y = Vector3.Dot(decalSpaceBiNormalWS, vertPos - decalSPaceOriginPosWS) / decalSpaceHeight +
                           0.5f;
                    _uvBuffer.Add(uv);
                    // 座標と回転を親の空間に変換する。
                    vertPos = toReceiverObjectSpaceMatrix.MultiplyPoint3x4(vertPos);
                    normal = toReceiverObjectSpaceMatrix.MultiplyVector(normal);

                    vertPos += normal * 0.005f;
                    _positionBuffer.Add(vertPos);
                    _normalBuffer.Add(normal);
                    _boneWeightsBuffer.Add(convexPolygon.GetBoneWeight(vertNo));
                }

                // 多角形は頂点数-2の三角形によって構築されている。
                var numTriangle = numVertex - 2;
                for (var triNo = 0; triNo < numTriangle; triNo++)
                {
                    _indexBuffer.Add(_indexBase);
                    _indexBuffer.Add(_indexBase + triNo + 1);
                    _indexBuffer.Add(_indexBase + triNo + 2);
                }

                _indexBase += numVertex;
            }

            // デカールメッシュレンダラーを作成。
            _decalMeshRenderer?.Destroy();

            if (_positionBuffer.Count <= 0) return;

            _mesh.SetVertices(_positionBuffer.ToArray());
            _mesh.SetIndices(_indexBuffer.ToArray(), MeshTopology.Triangles, 0);
            _mesh.SetNormals(_normalBuffer.ToArray(), 0, _normalBuffer.Count);
            if (_bindPoses != null && _bindPoses.Length > 0)
            {
                _mesh.boneWeights = _boneWeightsBuffer.ToArray();
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
    }
}
