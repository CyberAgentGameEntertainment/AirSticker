using System.Collections.Generic;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     凸多角形情報
    /// </summary>
    public class ConvexPolygonInfo
    {
        public CyConvexPolygon ConvexPolygon { get; set; } // 凸多角形
        public bool IsOutsideClipSpace { get; set; } // クリップ平面の外側？
    }


    /// <summary>
    ///     ターゲットオブジェクトの三角形ポリゴンブール
    /// </summary>
    public class CyReceiverObjectTrianglePolygonsPool
    {
        public Dictionary<GameObject, List<ConvexPolygonInfo>> ConvexPolygonsPool { get; } = new Dictionary<GameObject, List<ConvexPolygonInfo>>();
        /// <summary>
        /// プールをクリア
        /// </summary>
        public void Clear()
        {
            ConvexPolygonsPool.Clear();
        }
        /// <summary>
        ///     行列をスカラー倍する
        /// </summary>
        /// <remarks>
        /// 下記の計算が行われます。<br/>
        /// mOut = m * s;
        /// </remarks>
        /// <param name="mOut">計算結果の格納先</param>
        /// <param name="m">行列</param>
        /// <param name="s">スカラー倍</param>
        private static void Multiply(ref Matrix4x4 mOut, Matrix4x4 m, float s)
        {
            mOut = m;
            mOut.m00 *= s;
            mOut.m01 *= s;
            mOut.m02 *= s;
            mOut.m03 *= s;

            mOut.m10 *= s;
            mOut.m11 *= s;
            mOut.m12 *= s;
            mOut.m13 *= s;

            mOut.m20 *= s;
            mOut.m21 *= s;
            mOut.m22 *= s;
            mOut.m23 *= s;

            mOut.m30 *= s;
            mOut.m31 *= s;
            mOut.m32 *= s;
            mOut.m33 *= s;
        }
        /// <summary>
        /// 行列をスカラー倍して加算する。
        /// </summary>
        /// <remarks>
        /// 下記の計算が行われます。<br/>
        /// mOut *= m * s;
        /// </remarks>
        /// <param name="mOut">計算結果の格納先</param>
        /// <param name="m">行列</param>
        /// <param name="s">スカラー倍</param>
        private static void MultiplyAdd(ref Matrix4x4 mOut, Matrix4x4 m, float s)
        {
            mOut.m00 += m.m00 * s;
            mOut.m01 += m.m01 * s;
            mOut.m02 += m.m02 * s;
            mOut.m03 += m.m03 * s;

            mOut.m10 += m.m10 * s;
            mOut.m11 += m.m11 * s;
            mOut.m12 += m.m12 * s;
            mOut.m13 += m.m13 * s;

            mOut.m20 += m.m20 * s;
            mOut.m21 += m.m21 * s;
            mOut.m22 += m.m22 * s;
            mOut.m23 += m.m23 * s;

            mOut.m30 += m.m30 * s;
            mOut.m31 += m.m31 * s;
            mOut.m32 += m.m32 * s;
            mOut.m33 += m.m33 * s;
        }

        /// <summary>
        ///     デカールを貼り付けるレシーバーオブジェクトの情報から凸多角形ポリゴンを登録する。
        /// </summary>
        /// <param name="receiverObject">デカールが貼り付けられるレシーバーオブジェクト</param>
        public void RegisterConvexPolygons(GameObject receiverObject)
        {
            if (ConvexPolygonsPool.ContainsKey(receiverObject))
                // 登録済み
                return;

            // 新規登録。
            var convexPolygonInfos = new List<ConvexPolygonInfo>();
            
            RegisterConvexPolygonsFromMeshFilter(receiverObject, convexPolygonInfos);
            RegisterConvexPolygonsFromSkinMeshRenderer(receiverObject, convexPolygonInfos);
        }
        /// <summary>
        /// SkinModelRendererから凸ポリゴン情報を登録する
        /// </summary>
        /// <param name="receiverObject">デカールを貼り付けるレシーバーオブジェクト</param>
        /// <param name="convexPolygonInfos">凸ポリゴン情報の格納先</param>
        private void RegisterConvexPolygonsFromSkinMeshRenderer(GameObject receiverObject, List<ConvexPolygonInfo> convexPolygonInfos)
        {
            var skinnedMeshRenderers = receiverObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            var boneMatricesPallet = CalculateMatricesPallet(skinnedMeshRenderers);
            var vertices = new Vector3[3];
            var normals = new Vector3[3];
            var boneWeights = new BoneWeight[3];
            
            var localToWorldMatrices = new Matrix4x4[3];
            var skindMeshRendererNo = 0;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                var localToWorldMatrix = skinnedMeshRenderer.localToWorldMatrix;
                var mesh = skinnedMeshRenderer.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                int[] meshTriangles = mesh.triangles;
                Vector3[] meshVertices = mesh.vertices;
                Vector3[] meshNormals = mesh.normals;
                BoneWeight[] meshBoneWeights = mesh.boneWeights;
                for (var i = 0; i < numPoly; i++)
                {
                    var v0_no = meshTriangles[i * 3];
                    var v1_no = meshTriangles[i * 3 + 1];
                    var v2_no = meshTriangles[i * 3 + 2];

                    // ワールド行列を計算。
                    if (skinnedMeshRenderer.rootBone != null)
                    {
                        var boneMatrices = boneMatricesPallet[skindMeshRendererNo];
                        boneWeights[0] = meshBoneWeights[v0_no];
                        boneWeights[1] = meshBoneWeights[v1_no];
                        boneWeights[2] = meshBoneWeights[v2_no];

                        Multiply(ref localToWorldMatrices[0],
                            boneMatrices[boneWeights[0].boneIndex0],
                            boneWeights[0].weight0);
                        MultiplyAdd(
                            ref localToWorldMatrices[0],
                            boneMatrices[boneWeights[0].boneIndex1],
                            boneWeights[0].weight1);
                        MultiplyAdd(
                            ref localToWorldMatrices[0],
                            boneMatrices[boneWeights[0].boneIndex2],
                            boneWeights[0].weight2);
                        MultiplyAdd(
                            ref localToWorldMatrices[0],
                            boneMatrices[boneWeights[0].boneIndex3],
                            boneWeights[0].weight3);


                        Multiply(ref localToWorldMatrices[1],
                            boneMatrices[boneWeights[1].boneIndex0],
                            boneWeights[1].weight0);
                        MultiplyAdd(
                            ref localToWorldMatrices[1],
                            boneMatrices[boneWeights[1].boneIndex1],
                            boneWeights[1].weight1);
                        MultiplyAdd(
                            ref localToWorldMatrices[1],
                            boneMatrices[boneWeights[1].boneIndex2],
                            boneWeights[1].weight2);
                        MultiplyAdd(
                            ref localToWorldMatrices[1],
                            boneMatrices[boneWeights[1].boneIndex3],
                            boneWeights[1].weight3);

                        Multiply(ref localToWorldMatrices[2],
                            boneMatrices[boneWeights[2].boneIndex0],
                            boneWeights[2].weight0);
                        MultiplyAdd(
                            ref localToWorldMatrices[2],
                            boneMatrices[boneWeights[2].boneIndex1],
                            boneWeights[2].weight1);
                        MultiplyAdd(
                            ref localToWorldMatrices[2],
                            boneMatrices[boneWeights[2].boneIndex2],
                            boneWeights[2].weight2);
                        MultiplyAdd(
                            ref localToWorldMatrices[2],
                            boneMatrices[boneWeights[2].boneIndex3],
                            boneWeights[2].weight3);
                    }
                    else
                    {
                        boneWeights[0] = default;
                        boneWeights[1] = default;
                        boneWeights[2] = default;

                        localToWorldMatrices[0] = localToWorldMatrix;
                        localToWorldMatrices[1] = localToWorldMatrix;
                        localToWorldMatrices[2] = localToWorldMatrix;
                    }

                    vertices[0] = localToWorldMatrices[0].MultiplyPoint3x4(meshVertices[v0_no]);
                    vertices[1] = localToWorldMatrices[1].MultiplyPoint3x4(meshVertices[v1_no]);
                    vertices[2] = localToWorldMatrices[2].MultiplyPoint3x4(meshVertices[v2_no]);

                    normals[0] = localToWorldMatrices[0].MultiplyVector(meshNormals[v0_no]);
                    normals[1] = localToWorldMatrices[1].MultiplyVector(meshNormals[v1_no]);
                    normals[2] = localToWorldMatrices[2].MultiplyVector(meshNormals[v2_no]);

                    convexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                        ConvexPolygon = new CyConvexPolygon(
                            vertices,
                            normals,
                            boneWeights,
                            skinnedMeshRenderer)
                    });
                }

                skindMeshRendererNo++;
            }

            ConvexPolygonsPool.Add(receiverObject, convexPolygonInfos);
        }
        /// <summary>
        /// MeshFilterから凸ポリゴン情報を登録する。
        /// </summary>
        /// <param name="receiverObject">デカールを貼り付けるレシーバーオブジェクト</param>
        /// <param name="convexPolygonInfos">凸ポリゴン情報の格納先</param>
        private void RegisterConvexPolygonsFromMeshFilter(GameObject receiverObject, List<ConvexPolygonInfo> convexPolygonInfos)
        {
            var meshRenderers = receiverObject.GetComponentsInChildren<MeshRenderer>();
            var meshFilters = receiverObject.GetComponentsInChildren<MeshFilter>();

            var vertices = new Vector3[3];
            var normals = new Vector3[3];
            var boneWeights = new BoneWeight[3];

            var rendererNo = 0;
            foreach (var meshFilter in meshFilters)
            {
                var localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
                var mesh = meshFilter.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                for (var i = 0; i < numPoly; i++)
                {
                    var v0_no = mesh.triangles[i * 3];
                    var v1_no = mesh.triangles[i * 3 + 1];
                    var v2_no = mesh.triangles[i * 3 + 2];

                    vertices[0] = localToWorldMatrix.MultiplyPoint3x4(mesh.vertices[v0_no]);
                    vertices[1] = localToWorldMatrix.MultiplyPoint3x4(mesh.vertices[v1_no]);
                    vertices[2] = localToWorldMatrix.MultiplyPoint3x4(mesh.vertices[v2_no]);

                    normals[0] = localToWorldMatrix.MultiplyVector(mesh.normals[v0_no]);
                    normals[1] = localToWorldMatrix.MultiplyVector(mesh.normals[v1_no]);
                    normals[2] = localToWorldMatrix.MultiplyVector(mesh.normals[v2_no]);

                    boneWeights[0] = default;
                    boneWeights[1] = default;
                    boneWeights[2] = default;

                    convexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                        ConvexPolygon = new CyConvexPolygon(
                            vertices,
                            normals,
                            boneWeights,
                            meshRenderers[rendererNo])
                    });
                }

                rendererNo++;
            }
        }

        /// <summary>
        /// スキニングのための行列パレットを計算
        /// </summary>
        /// <param name="skinnedMeshRenderers"></param>
        /// <returns></returns>
        private Matrix4x4[][] CalculateMatricesPallet(SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            var boneMatricesPallet = new Matrix4x4[skinnedMeshRenderers.Length][];
            var skindMeshRendererNo = 0;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.rootBone != null)
                {
                    var mesh = skinnedMeshRenderer.sharedMesh;
                    var numBone = skinnedMeshRenderer.bones.Length;

                    var boneMatrices = new Matrix4x4[numBone];
                    for (var boneNo = 0; boneNo < numBone; boneNo++)
                        boneMatrices[boneNo] = skinnedMeshRenderer.bones[boneNo].localToWorldMatrix
                                               * mesh.bindposes[boneNo];

                    boneMatricesPallet[skindMeshRendererNo] = boneMatrices;
                }

                skindMeshRendererNo++;
            }

            return boneMatricesPallet;
        }
    }
}
