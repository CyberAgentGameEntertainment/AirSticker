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
        private CyRenderDecalPass _renderPass;
        private readonly CyDecalMeshPool _decalMeshPool = new CyDecalMeshPool();
        private readonly CyTargetObjectTrianglePolygonsPool _targetObjectTrianglePolygonsPool =
            new CyTargetObjectTrianglePolygonsPool(); 
        public CyRenderDecalFeature()
        {
            Instance = this;

        }
        public override void Create()
        {
            _renderPass = new CyRenderDecalPass();
            _renderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }
    
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null)
            {
                return;
            }
            renderer.EnqueuePass(_renderPass);
        }

        public CyDecalMesh GetDecalMesh(
            GameObject targetObject,
            Material decalMaterial,
            out bool isNew)
        {
            return _decalMeshPool.GetDecalMesh(targetObject, decalMaterial, out isNew);
        }

        public void RegisterDecalTargetObject(GameObject targetObject)
        {
            _targetObjectTrianglePolygonsPool.RegisterConvexPolygons(targetObject);
        }

        public List<ConvexPolygonInfo> GetTrianglePolygons(GameObject targetObject)
        {
            return _targetObjectTrianglePolygonsPool.ConvexPolygonsPool[targetObject];
        }

        public void AddDecalMesh(CyDecalMesh cyDecalMesh)
        {
            _renderPass.AddDecalMesh(cyDecalMesh);
        }
    }
}
