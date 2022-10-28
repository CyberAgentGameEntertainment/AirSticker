using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// デカールメッシュのプール
    /// </summary>
    public class CyDecalMeshPool
    {
        private Dictionary<int, CyDecalMesh> _decalMeshes = new Dictionary<int, CyDecalMesh>();

        /// <summary>
        /// デカールメッシュを取得
        /// </summary>
        /// <remarks>
        /// デカールメッシュは貼り付けるターゲットオブジェクトとデカールマテリアルが同じ場合に共有されます。
        /// また、全く新規のターゲットオブジェクトとマテリアルであれば、
        /// 新規のデカールメッシュを作成します。
        /// </remarks>
        /// <param name="_projectorObject">デカールプロジェクター</param>
        /// <param name="receiverObject">デカールを貼り付けるターゲットオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <returns></returns>
        public List<CyDecalMesh> GetDecalMeshes( 
            GameObject _projectorObject,
            GameObject receiverObject,
            Material decalMaterial)
        {
            var decalMeshes = new List<CyDecalMesh>();
            var renderers = receiverObject.GetComponentsInChildren<Renderer>();
            foreach( var renderer in renderers){
                int hash = receiverObject.GetHashCode()
                           + decalMaterial.name.GetHashCode()
                           + renderer.GetHashCode();
                if (_decalMeshes.ContainsKey(hash))
                {
                    decalMeshes.Add(_decalMeshes[hash]);
                }

                var newMesh = new CyDecalMesh(_projectorObject, receiverObject, decalMaterial, renderer);
                GameObject decalRenderer = new GameObject("CyDecalRenderer");
                if (renderer is MeshRenderer)
                {
                    var meshRenderer = decalRenderer.AddComponent<MeshRenderer>();
                    meshRenderer.material = decalMaterial;
                    var meshFilter = decalRenderer.AddComponent<MeshFilter>();
                    meshFilter.mesh = newMesh.Mesh;
                }else if (renderer is SkinnedMeshRenderer s)
                {
                    var skinnedMeshRenderer = decalRenderer.AddComponent<SkinnedMeshRenderer>();
                    skinnedMeshRenderer.sharedMesh = newMesh.Mesh;
                    skinnedMeshRenderer.material = decalMaterial;
                    skinnedMeshRenderer.rootBone = s.rootBone;
                    skinnedMeshRenderer.bones = s.bones;
                }
                
                if (!_projectorObject.isStatic)
                {
                    decalRenderer.transform.parent = renderer.transform;
                }

                decalRenderer.transform.localPosition = Vector3.zero;
                decalRenderer.transform.localRotation = Quaternion.identity;
                decalRenderer.transform.localScale = Vector3.one;
                decalRenderer.SetActive(false);
                _decalMeshes.Add(hash, newMesh);
                decalMeshes.Add(newMesh);
            }
            return decalMeshes;
        }
    }
}
