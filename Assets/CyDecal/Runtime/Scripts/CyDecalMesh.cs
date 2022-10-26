using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// デカールメッシュ
    /// </summary>
     public class CyDecalMesh 
    {
        private readonly Mesh _mesh;                                            // デカールテクスチャを貼り付けるためのデカールメッシュ
        private readonly List<int> _indexBuffer = new List<int>();              // インデックスバッファ
        private readonly List<Vector3> _positionBuffer = new List<Vector3>();   // 頂点座標のバッファ
        private readonly List<Vector2> _uvBuffer = new List<Vector2>();         // UVバッファ
        private readonly List<Vector3> _normalBuffer = new List<Vector3>();     // 法線。
        int _indexBase = 0;
        public Mesh Mesh { get => _mesh; }
        public Material Material { get; set; }
        private GameObject _projectorObject;
        private GameObject _receiverObject;
        public CyDecalMesh(
            GameObject projectorObject, 
            GameObject receiverObject,
            Material decalMaterial)
        {
            _mesh = new Mesh();
            Material = decalMaterial;
            _projectorObject = projectorObject;
            _receiverObject = receiverObject;
        }

        /// <summary>
        /// 三角形ポリゴンをデカールメッシュを追加。
        /// </summary>
        /// <remarks>
        /// 凸ポリゴン情報から三角形ポリゴンの情報を追加で構築し、デカールメッシュに追加します。
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
            var toReceiverObjectSpaceMatrix = _receiverObject.transform.worldToLocalMatrix;
            Vector2 uv = new Vector2();
            foreach (var convexPolygon in convexPolygons)
            {
                int numVertex = convexPolygon.NumVertices;
                for (int vertNo = 0; vertNo < numVertex; vertNo++)
                {
                    Vector3 vertPos = convexPolygon.GetVertexPosition(vertNo);
                    Vector3 normal = convexPolygon.GetVertexNormal(vertNo);
                    
                    // Zファイティング回避のために、デカールの投影方向の逆向きに少しオフセットを加える。
                    // TODO: この数値は後で調整できるようにする。
                    vertPos += decalSpaceNormalWS * 0.001f;
                    uv.x = Vector3.Dot(decalSpaceTangentWS, (vertPos - decalSPaceOriginPosWS))/decalSpaceWidth + 0.5f;
                    uv.y = Vector3.Dot(decalSpaceBiNormalWS, (vertPos - decalSPaceOriginPosWS))/decalSpaceHeight + 0.5f;
                    _uvBuffer.Add(uv);
                    if (!_projectorObject.isStatic)
                    {
                        // プロジェクターオブジェクトが静的でない場合は、座標と回転を親の空間に変換する。
                        vertPos = toReceiverObjectSpaceMatrix.MultiplyPoint3x4(vertPos);
                        normal = toReceiverObjectSpaceMatrix.MultiplyVector(normal);
                    }
                    _positionBuffer.Add(vertPos);
                    _normalBuffer.Add(normal);
                }

                // 多角形は頂点数-2の三角形によって構築されている。
                int numTriangle = numVertex-2;
                for (int triNo = 0; triNo < numTriangle; triNo++)
                {
                    _indexBuffer.Add(_indexBase);
                    _indexBuffer.Add(_indexBase+triNo+1);
                    _indexBuffer.Add(_indexBase+triNo+2);
                }
                _indexBase += numVertex;
            }
            _mesh.SetVertices(_positionBuffer.ToArray());
            _mesh.SetIndices(_indexBuffer.ToArray(), MeshTopology.Triangles, 0);
            _mesh.SetNormals(_normalBuffer.ToArray(), 0, _normalBuffer.Count);
            _mesh.RecalculateTangents();
            _mesh.SetUVs(0, _uvBuffer);
        }
    }
}
