using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    public class CyRenderDecalFeature : ScriptableRendererFeature
    {
        public static float splitMeshTotalTime = 0.0f; 
        public static CyRenderDecalFeature Instance { get; private set; }
        private readonly CyDecalMeshPool _decalMeshPool = new CyDecalMeshPool();
        private readonly CyTargetObjectTrianglePolygonsPool _targetObjectTrianglePolygonsPool =
            new CyTargetObjectTrianglePolygonsPool(); 
        public CyRenderDecalFeature()
        {
            Instance = this;

        }
        public override void Create()
        {
        }
    
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
        }
        /// <summary>
        /// デカールメッシュの編集開始
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
            Instance._decalMeshPool.BeginEditDecalMesh();
            var decalMeshes = Instance._decalMeshPool.GetDecalMeshes(
                projectorObject,
                receiverObject,
                decalMaterial);
            return decalMeshes;
        }
        /// <summary>
        /// デカールメッシュの編集完了。
        /// </summary>
        /// <param name="decalMeshes"></param>
        /// <returns></returns>
        public static void EndEditDecalMeshes(List<CyDecalMesh> decalMeshes)
        {
            Instance._decalMeshPool.EndEditDecalMesh();
        }
        public static List<ConvexPolygonInfo> GetTrianglePolygons(GameObject receiverObject)
        {
            Instance._targetObjectTrianglePolygonsPool.RegisterConvexPolygons(receiverObject);
            return Instance._targetObjectTrianglePolygonsPool.ConvexPolygonsPool[receiverObject];
        }
    }
}
