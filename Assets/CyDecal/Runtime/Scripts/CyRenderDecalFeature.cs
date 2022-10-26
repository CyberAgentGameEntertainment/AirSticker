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

        public CyDecalMesh GetDecalMesh(
            GameObject receiverObject,
            Material decalMaterial,
            out bool isNew)
        {
            return _decalMeshPool.GetDecalMesh(receiverObject, decalMaterial, out isNew);
        }

        public void RegisterDecalReceiverObject(GameObject receiverObject)
        {
            _targetObjectTrianglePolygonsPool.RegisterConvexPolygons(receiverObject);
        }

        public List<ConvexPolygonInfo> GetTrianglePolygons(GameObject receiverObject)
        {
            return _targetObjectTrianglePolygonsPool.ConvexPolygonsPool[receiverObject];
        }
    }
}
