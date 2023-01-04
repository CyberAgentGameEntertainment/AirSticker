using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     三角ポリゴン情報のファクトリ
    /// </summary>
    public static class CyTrianglePolygonsFactory
    {
        private static readonly int VertexCountOfTrianglePolygon = 3;
        public static int MaxGeneratedPolygonPerFrame { get; set; } = 100; //

        /// <summary>
        ///     行列をスカラー倍する
        /// </summary>
        /// <remarks>
        ///     下記の計算が行われます。<br />
        ///     mOut = m * s;
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
        ///     行列をスカラー倍して加算する。
        /// </summary>
        /// <remarks>
        ///     下記の計算が行われます。<br />
        ///     mOut *= m * s;
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
        ///     デカールを貼り付けられるレシーバーオブジェクトの情報から凸ポリゴン情報を登録する。
        /// </summary>
        /// <param name="meshFilters">レシーバーオブジェクトのメッシュフィルター</param>
        /// <param name="meshRenderers">レシーバーオブジェクトのメッシュレンダラー</param>
        /// <param name="skinnedMeshRenderers">レシーバーオブジェクトのスキンメッシュレンダラー</param>
        /// <param name="convexPolygonInfos">凸ポリゴン情報の格納先</param>
        internal static IEnumerator BuildFromReceiverObject(
            MeshFilter[] meshFilters,
            MeshRenderer[] meshRenderers,
            SkinnedMeshRenderer[] skinnedMeshRenderers,
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            CalculateCapacityConvexPolygonInfos(meshFilters, skinnedMeshRenderers, convexPolygonInfos);
            yield return BuildFromMeshFilter(meshFilters, meshRenderers, convexPolygonInfos);
            yield return BuildFromSkinMeshRenderer(skinnedMeshRenderers, convexPolygonInfos);

            yield return null;
        }

        /// <summary>
        ///     SkinModelRendererコンポーネントからポリゴン数を取得する。
        /// </summary>
        /// <param name="skinnedMeshRenderers">スキンモデルレンダラー</param>
        /// <returns>ポリゴン数</returns>
        private static int GetNumPolygonsFromSkinModelRenderers(SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            var numPolygon = 0;

            foreach (var renderer in skinnedMeshRenderers)
            {
                if (!renderer || renderer.sharedMesh == null) return -1;
                var mesh = renderer.sharedMesh;
                numPolygon += mesh.triangles.Length / 3;
            }

            return numPolygon;
        }

        /// <summary>
        ///     メッシュフィルターからポリゴン数を取得する。
        /// </summary>
        /// <param name="meshFilters"></param>
        /// <returns>ポリゴン数</returns>
        private static int GetNumPolygonsFromMeshFilters(MeshFilter[] meshFilters)
        {
            var numPolygon = 0;
            foreach (var meshFilter in meshFilters)
            {
                if (!meshFilter || meshFilter.sharedMesh == null) return -1;
                var mesh = meshFilter.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                numPolygon += numPoly;
            }

            return numPolygon;
        }

        /// <summary>
        ///     凸ポリゴン情報のリストのキャパシティを計算する
        /// </summary>
        /// <remarks>
        ///     キャパシティを設定すると、その分のメモリ確保が一気に行われるため、
        ///     Addの際の配列拡張によるメモリ確保を防ぐことができるので、事前に計算する。
        /// </remarks>
        /// <param name="skinnedMeshRenderers"></param>
        /// <param name="convexPolygonInfos"></param>
        /// <param name="meshFilters"></param>
        /// <exception cref="NotImplementedException"></exception>
        private static void CalculateCapacityConvexPolygonInfos(MeshFilter[] meshFilters,
            SkinnedMeshRenderer[] skinnedMeshRenderers, List<ConvexPolygonInfo> convexPolygonInfos)
        {
            var capacity = 0;
            capacity += GetNumPolygonsFromMeshFilters(meshFilters);
            capacity += GetNumPolygonsFromSkinModelRenderers(skinnedMeshRenderers);
            if (capacity > 0) convexPolygonInfos.Capacity = capacity;
        }

        /// <summary>
        ///     MeshFilterから凸ポリゴン情報を登録する。
        /// </summary>
        /// <param name="meshFilters">レシーバーオブジェクトのメッシュフィルター</param>
        /// <param name="meshRenderers">レシーバーオブジェクトのメッシュレンダラー</param>
        /// <param name="convexPolygonInfos">凸ポリゴン情報の格納先</param>
        private static IEnumerator BuildFromMeshFilter(MeshFilter[] meshFilters, MeshRenderer[] meshRenderers,
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            var numBuildConvexPolygon = GetNumPolygonsFromMeshFilters(meshFilters);
            if (numBuildConvexPolygon < 0) yield break;
            var newConvexPolygonInfos = new ConvexPolygonInfo[numBuildConvexPolygon];

            // Calculate size of some buffers.
            var bufferSize = 0;
            foreach (var meshFilter in meshFilters)
            {
                if (!meshFilter || meshFilter.sharedMesh == null)
                    continue;
                var mesh = meshFilter.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                bufferSize += numPoly * VertexCountOfTrianglePolygon;
            }

            // Allocate some buffers.
            var positionBuffer = new Vector3[bufferSize];
            var boneWeightBuffer = new BoneWeight[bufferSize];
            var normalBuffer = new Vector3[bufferSize];
            var lineBuffer = new CyLine[bufferSize];
            var startOffsetOfBuffer = 0;

            var rendererNo = 0;
            var newConvexPolygonNo = 0;
            foreach (var meshFilter in meshFilters)
            {
                if (!meshFilter || meshFilter.sharedMesh == null)
                    // meshFilterが削除されているので打ち切る
                    yield break;
                var localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
                var mesh = meshFilter.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                var meshTriangles = mesh.triangles;
                var meshVertices = mesh.vertices;
                var meshNormals = mesh.normals;
                for (var i = 0; i < numPoly; i++)
                {
                    if ((newConvexPolygonNo + 1) % MaxGeneratedPolygonPerFrame == 0)
                        // 1フレームに処理するポリゴンは最大で100まで
                        yield return null;
                    if (!meshFilter || meshFilter.sharedMesh == null)
                        // meshFilterが削除されているので打ち切る
                        yield break;
                    var v0_no = meshTriangles[i * 3];
                    var v1_no = meshTriangles[i * 3 + 1];
                    var v2_no = meshTriangles[i * 3 + 2];

                    positionBuffer[startOffsetOfBuffer] = localToWorldMatrix.MultiplyPoint3x4(meshVertices[v0_no]);
                    positionBuffer[startOffsetOfBuffer + 1] = localToWorldMatrix.MultiplyPoint3x4(meshVertices[v1_no]);
                    positionBuffer[startOffsetOfBuffer + 2] = localToWorldMatrix.MultiplyPoint3x4(meshVertices[v2_no]);

                    normalBuffer[startOffsetOfBuffer] = localToWorldMatrix.MultiplyVector(meshNormals[v0_no]);
                    normalBuffer[startOffsetOfBuffer + 1] = localToWorldMatrix.MultiplyVector(meshNormals[v1_no]);
                    normalBuffer[startOffsetOfBuffer + 2] = localToWorldMatrix.MultiplyVector(meshNormals[v2_no]);

                    boneWeightBuffer[startOffsetOfBuffer] = default;
                    boneWeightBuffer[startOffsetOfBuffer + 1] = default;
                    boneWeightBuffer[startOffsetOfBuffer + 2] = default;
                    newConvexPolygonInfos[newConvexPolygonNo] = new ConvexPolygonInfo
                    {
                        ConvexPolygon = new CyConvexPolygon(
                            positionBuffer,
                            normalBuffer,
                            boneWeightBuffer,
                            lineBuffer,
                            meshRenderers[rendererNo],
                            startOffsetOfBuffer,
                            VertexCountOfTrianglePolygon,
                            VertexCountOfTrianglePolygon)
                    };
                    newConvexPolygonNo++;
                    startOffsetOfBuffer += VertexCountOfTrianglePolygon;
                }

                rendererNo++;
            }

            convexPolygonInfos.AddRange(newConvexPolygonInfos);
        }

        /// <summary>
        ///     SkinModelRendererから凸ポリゴン情報を登録する
        /// </summary>
        /// <param name="skinnedMeshRenderers">レシーバーオブジェクトのスキンメッシュレンダラー</param>
        /// <param name="convexPolygonInfos">凸ポリゴン情報の格納先</param>
        private static IEnumerator BuildFromSkinMeshRenderer(SkinnedMeshRenderer[] skinnedMeshRenderers,
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            var numBuildConvexPolygon = GetNumPolygonsFromSkinModelRenderers(skinnedMeshRenderers);
            if (numBuildConvexPolygon < 0) yield break;

            var newConvexPolygonInfos = new ConvexPolygonInfo[numBuildConvexPolygon];
            var boneWeights = new BoneWeight[3];
            var localToWorldMatrices = new Matrix4x4[3];
            var boneMatricesPallet = CalculateMatricesPallet(skinnedMeshRenderers);
            var skinnedMeshRendererNo = 0;
            var newConvexPolygonNo = 0;

            // Calculate size of some buffers.
            var bufferSize = 0;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (!skinnedMeshRenderer || skinnedMeshRenderer.sharedMesh == null)
                    // スキンモデルレンダラーが無効になっているので打ち切る。
                    continue;
                var mesh = skinnedMeshRenderer.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                bufferSize += numPoly * VertexCountOfTrianglePolygon;
            }

            // Allocate some buffers.
            var positionBuffer = new Vector3[bufferSize];
            var boneWeightBuffer = new BoneWeight[bufferSize];
            var normalBuffer = new Vector3[bufferSize];
            var lineBuffer = new CyLine[bufferSize];
            var startOffsetOfBuffer = 0;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (!skinnedMeshRenderer || skinnedMeshRenderer.sharedMesh == null)
                    // スキンモデルレンダラーが無効になっているので打ち切る。
                    yield break;
                var localToWorldMatrix = skinnedMeshRenderer.localToWorldMatrix;
                var mesh = skinnedMeshRenderer.sharedMesh;
                var numPoly = mesh.triangles.Length / 3;
                var meshTriangles = mesh.triangles;
                var meshVertices = mesh.vertices;
                var meshNormals = mesh.normals;
                var meshBoneWeights = mesh.boneWeights;

                for (var i = 0; i < numPoly; i++)
                {
                    if ((newConvexPolygonNo + 1) % MaxGeneratedPolygonPerFrame == 0)
                        // 1フレームに処理するポリゴンは最大でMaxGeneratedPolygonPerFrameまで
                        yield return null;
                    if (!skinnedMeshRenderer || skinnedMeshRenderer.sharedMesh == null)
                        // スキンモデルレンダラーが無効になっているので打ち切る。
                        yield break;
                    var v0No = meshTriangles[i * 3];
                    var v1No = meshTriangles[i * 3 + 1];
                    var v2No = meshTriangles[i * 3 + 2];

                    // ワールド行列を計算。
                    if (skinnedMeshRenderer.rootBone != null)
                    {
                        var boneMatrices = boneMatricesPallet[skinnedMeshRendererNo];
                        boneWeights[0] = meshBoneWeights[v0No];
                        boneWeights[1] = meshBoneWeights[v1No];
                        boneWeights[2] = meshBoneWeights[v2No];
                        boneWeightBuffer[startOffsetOfBuffer] = boneWeights[0];
                        boneWeightBuffer[startOffsetOfBuffer + 1] = boneWeights[1];
                        boneWeightBuffer[startOffsetOfBuffer + 2] = boneWeights[2];
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
                        boneWeightBuffer[startOffsetOfBuffer] = default;
                        boneWeightBuffer[startOffsetOfBuffer + 1] = default;
                        boneWeightBuffer[startOffsetOfBuffer + 2] = default;

                        localToWorldMatrices[0] = localToWorldMatrix;
                        localToWorldMatrices[1] = localToWorldMatrix;
                        localToWorldMatrices[2] = localToWorldMatrix;
                    }

                    positionBuffer[startOffsetOfBuffer] = localToWorldMatrices[0].MultiplyPoint3x4(meshVertices[v0No]);
                    positionBuffer[startOffsetOfBuffer + 1] =
                        localToWorldMatrices[1].MultiplyPoint3x4(meshVertices[v1No]);
                    positionBuffer[startOffsetOfBuffer + 2] =
                        localToWorldMatrices[2].MultiplyPoint3x4(meshVertices[v2No]);

                    normalBuffer[startOffsetOfBuffer] = localToWorldMatrices[0].MultiplyVector(meshNormals[v0No]);
                    normalBuffer[startOffsetOfBuffer + 1] = localToWorldMatrices[1].MultiplyVector(meshNormals[v1No]);
                    normalBuffer[startOffsetOfBuffer + 2] = localToWorldMatrices[2].MultiplyVector(meshNormals[v2No]);
                    newConvexPolygonInfos[newConvexPolygonNo] = new ConvexPolygonInfo
                    {
                        ConvexPolygon = new CyConvexPolygon(
                            positionBuffer,
                            normalBuffer,
                            boneWeightBuffer,
                            lineBuffer,
                            skinnedMeshRenderer,
                            startOffsetOfBuffer,
                            3,
                            VertexCountOfTrianglePolygon)
                    };
                    newConvexPolygonNo++;
                    startOffsetOfBuffer += VertexCountOfTrianglePolygon;
                }

                skinnedMeshRendererNo++;
            }

            convexPolygonInfos.AddRange(newConvexPolygonInfos);
        }


        /// <summary>
        ///     スキニングのための行列パレットを計算
        /// </summary>
        /// <param name="skinnedMeshRenderers">レシーバーオブジェクトのスキンメッシュレンダラー</param>
        /// <returns>計算された行列パレット</returns>
        private static Matrix4x4[][] CalculateMatricesPallet(SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            var boneMatricesPallet = new Matrix4x4[skinnedMeshRenderers.Length][];
            var skindMeshRendererNo = 0;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (!skinnedMeshRenderer) continue;
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
