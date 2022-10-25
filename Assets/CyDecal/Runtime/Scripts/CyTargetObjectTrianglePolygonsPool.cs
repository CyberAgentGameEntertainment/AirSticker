

using System.Collections.Generic;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// 凸多角形情報
    /// </summary>
    public class ConvexPolygonInfo
    {
        public CyConvexPolygon ConvexPolygon { get; set; }      // 凸多角形
        public bool IsOutsideClipSpace { get; set; } = false;   // クリップ平面の外側？
    };
    /// <summary>
    /// ターゲットオブジェクトの三角形ポリゴンブール
    /// </summary>
    public class CyTargetObjectTrianglePolygonsPool
    {
        public Dictionary<GameObject, List<ConvexPolygonInfo>> ConvexPolygonsPool { get; private set; } =
            new Dictionary<GameObject, List<ConvexPolygonInfo>>();
        /// <summary>
        /// デカールを貼り付けるレシーバーオブジェクトの情報から凸多角形ポリゴンを登録する。
        /// </summary>
        /// <param name="receiverObject">デカールが貼り付けられるレシーバーオブジェクト</param>
        public void RegisterConvexPolygons(GameObject receiverObject)
        {
            if (ConvexPolygonsPool.ContainsKey(receiverObject))
            {
                // 登録済み
                return;
            }            
            // 新規登録。
            var convexPolygonInfos = new List<ConvexPolygonInfo>();
            var meshFilters = receiverObject.GetComponentsInChildren<MeshFilter>();
            
            Vector3[] vertices = new Vector3[3];
            Vector3[] normals = new Vector3[3];
            foreach (var meshFilter in meshFilters)
            {
                // TODO: これはどうしたものか・・・。
                var localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
                var mesh = meshFilter.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                for (int i = 0; i < numPoly; i++)
                {
                    int v0_no = mesh.triangles[i * 3];
                    int v1_no = mesh.triangles[i * 3 + 1];
                    int v2_no = mesh.triangles[i * 3 + 2];

                    vertices[0] = localToWorldMatrix.MultiplyPoint3x4(mesh.vertices[v0_no]);
                    vertices[1] = localToWorldMatrix.MultiplyPoint3x4(mesh.vertices[v1_no]);
                    vertices[2] = localToWorldMatrix.MultiplyPoint3x4(mesh.vertices[v2_no]);

                    normals[0] = localToWorldMatrix.MultiplyVector(mesh.normals[v0_no]);
                    normals[1] = localToWorldMatrix.MultiplyVector(mesh.normals[v1_no]);
                    normals[2] = localToWorldMatrix.MultiplyVector(mesh.normals[v2_no]);
                    
                    convexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                        ConvexPolygon = new CyConvexPolygon(vertices, normals)
                    });
                }
            }
            ConvexPolygonsPool.Add(receiverObject, convexPolygonInfos);
        }
    }
    
}
