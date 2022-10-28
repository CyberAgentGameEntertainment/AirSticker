using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CyDecal.Runtime.Scripts
{
    public class CyRenderDecalFeature : ScriptableRendererFeature
    {
        public static float splitMeshTotalTime = 0.0f;
        private readonly CyDecalMeshPool _decalMeshPool = new();

        private readonly CyTargetObjectTrianglePolygonsPool _targetObjectTrianglePolygonsPool = new();

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

        /// <summary>
        ///     デカールメッシュの編集開始
        /// </summary>
        /// <param name="projectorObject">プロジェクターオブジェクト</param>
        /// <param name="receiverObject">デカールを貼り付けるレシーバーオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <returns></returns>
        public static List<CyDecalMesh> BeginEditDecalMeshes(
            GameObject projectorObject,
            GameObject receiverObject,
            Material decalMaterial)
        {
            Instance._decalMeshPool.OnBeginEditDecalMesh();
            var decalMeshes = Instance._decalMeshPool.GetDecalMeshes(
                projectorObject,
                receiverObject,
                decalMaterial);
            return decalMeshes;
        }

        /// <summary>
        ///     デカールメッシュの編集完了。
        /// </summary>
        /// <param name="decalMeshes"></param>
        /// <returns></returns>
        public static void EndEditDecalMeshes(List<CyDecalMesh> decalMeshes)
        {
            Instance._decalMeshPool.OnEndEditDecalMesh();
        }
        /// <summary>
        /// デカールを貼り付けるレシーバーオブジェクトの三角形ポリゴン情報を取得。
        /// </summary>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <returns></returns>
        public static List<ConvexPolygonInfo> GetTrianglePolygons(GameObject receiverObject)
        {
            Instance._targetObjectTrianglePolygonsPool.RegisterConvexPolygons(receiverObject);
            return Instance._targetObjectTrianglePolygonsPool.ConvexPolygonsPool[receiverObject];
        }
    }
}
