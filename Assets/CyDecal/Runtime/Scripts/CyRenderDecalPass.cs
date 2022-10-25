using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    public class CyRenderDecalPass : ScriptableRenderPass
    {
        private List<CyDecalMesh> _decalMeshes = new List<CyDecalMesh>();
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(nameof(CyRenderDecalPass));
            foreach (var decalMesh in _decalMeshes)
            {
                cmd.DrawMesh(
                    decalMesh.Mesh, 
                    Matrix4x4.identity, 
                    decalMesh.Material,
                    0,
                    0);
            }
            context.ExecuteCommandBuffer(cmd);
            _decalMeshes.Clear();
        }

        public void AddDecalMesh(CyDecalMesh decalMesh)
        {
            _decalMeshes.Add(decalMesh);
        }
    }
}
