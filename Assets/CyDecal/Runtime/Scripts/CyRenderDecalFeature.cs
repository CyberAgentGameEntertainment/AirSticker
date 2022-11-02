using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CyDecal.Runtime.Scripts
{
    public class CyRenderDecalFeature : ScriptableRendererFeature
    {
        public static float splitMeshTotalTime = 0.0f;
        private readonly CyDecalMeshPool _decalMeshPool = new CyDecalMeshPool();

        private readonly CyReceiverObjectTrianglePolygonsPool _receiverObjectTrianglePolygonsPool = new CyReceiverObjectTrianglePolygonsPool();

        public CyRenderDecalFeature()
        {
            Instance = this;
        }

        public static CyRenderDecalFeature Instance { get; private set; }

        public override void Create()
        {
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
        }
        
        public static void DisableDecalMeshRenderers()
        {
            if (Instance == null) return ;
            Instance._decalMeshPool.DisableDecalMeshRenderers();
        }
        public static void EnableDecalMeshRenderers()
        {
            if (Instance == null) return ;
            Instance._decalMeshPool.EnableDecalMeshRenderers();
        }
        public static List<CyDecalMesh> GetDecalMeshes(
            GameObject projectorObject,
            GameObject receiverObject,
            Material decalMaterial)
        {
            if (Instance == null) return null;
            var decalMeshes = Instance._decalMeshPool.GetDecalMeshes(
                projectorObject,
                receiverObject,
                decalMaterial);
            return decalMeshes;
        }

        public static bool ExistTrianglePolygons(GameObject receiverObject)
        {
            return Instance._receiverObjectTrianglePolygonsPool.ExistConvexPolygons(receiverObject);
        }
        public static IEnumerator RegisterTrianglePolygons(
            GameObject receiverObject, 
            MeshFilter[] meshFilters,
            MeshRenderer[] meshRenderers,
            SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            if (Instance == null)
            {
                yield break;
            }
            yield return Instance._receiverObjectTrianglePolygonsPool.RegisterConvexPolygons(
                receiverObject,
                meshFilters,
                meshRenderers,
                skinnedMeshRenderers);
        }
        /// <summary>
        /// デカールを貼り付けるレシーバーオブジェクトの三角形ポリゴン情報を取得。
        /// </summary>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <returns></returns>
        public static List<ConvexPolygonInfo> GetTrianglePolygons(
            GameObject receiverObject,
            MeshFilter[] meshFilters,
            SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            if (Instance == null)
            {
                return null;
            }
       
            var convexPolygonInfos =Instance._receiverObjectTrianglePolygonsPool.ConvexPolygonsPool[receiverObject];
            foreach (var info in convexPolygonInfos)
            {
                info.IsOutsideClipSpace = false;
            }
            return convexPolygonInfos;
        }

        /// <summary>
        /// デカールの描画で使っている全てのプールをクリア。
        /// </summary>
        public static void ClearALlPools()
        {
            if (Instance == null) return;
            Instance._decalMeshPool.Clear();
            Instance._receiverObjectTrianglePolygonsPool.Clear();
        }
        /// <summary>
        /// レシーバーオブジェクトの三角形ポリゴンプールをクリア。
        /// </summary>
        public static void ClearReceiverObjectTrianglePolygonsPool()
        {
            if (Instance == null) return;
            Instance._receiverObjectTrianglePolygonsPool.Clear();
        }
    }
}
