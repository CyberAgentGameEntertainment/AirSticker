using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.XR;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// デカールメッシュのレンダラー
    /// </summary>
    public class CyDecalMeshRenderer
    {
        private Renderer _renderer;
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="recieverRenderer">デカールメッシュのレシーバーオブジェクトのレンダラー</param>
        public CyDecalMeshRenderer(Renderer recieverRenderer, Material decalMaterial, Mesh mesh, bool isStatic)
        {
            GameObject decalRenderer = new GameObject("CyDecalRenderer");
            if (recieverRenderer is MeshRenderer)
            {
                var meshRenderer = decalRenderer.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = decalRenderer.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                _renderer = meshRenderer;
            }else if (recieverRenderer is SkinnedMeshRenderer s)
            {
                var skinnedMeshRenderer = decalRenderer.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = decalMaterial;
                skinnedMeshRenderer.rootBone = s.rootBone;
                skinnedMeshRenderer.bones = s.bones;
                _renderer = skinnedMeshRenderer;
            }
                
            if (!isStatic)
            {
                decalRenderer.transform.parent = recieverRenderer.transform;
            }

            decalRenderer.transform.localPosition = Vector3.zero;
            decalRenderer.transform.localRotation = Quaternion.identity;
            decalRenderer.transform.localScale = Vector3.one;
            decalRenderer.SetActive(false);
        }
        /// <summary>
        /// デカールメッシュの編集が開始されたときに呼ばれる処理。
        /// </summary>
        public void OnBeginEditDecalMesh()
        {
            _renderer.gameObject.SetActive(false);
        }
        /// <summary>
        /// デカールメッシュの編集が終了されたときに呼ばれる処理。
        /// </summary>
        public void OnEndEditDecalMesh()
        {
            _renderer.gameObject.SetActive(true);
        }
    }
}
