using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールメッシュのレンダラー
    /// </summary>
    public class CyDecalMeshRenderer
    {
        private readonly Renderer _renderer;

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="receiverRenderer">デカールメッシュのレシーバーオブジェクトのレンダラー</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <param name="mesh">デカールメッシュ</param>
        /// <param name="isStatic">静的オブジェクトフラグ</param>
        public CyDecalMeshRenderer(Renderer receiverRenderer, Material decalMaterial, Mesh mesh, bool isStatic)
        {
            var decalRenderer = new GameObject("CyDecalRenderer");
            if (receiverRenderer is MeshRenderer)
            {
                var meshRenderer = decalRenderer.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = decalRenderer.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                _renderer = meshRenderer;
            }
            else if (receiverRenderer is SkinnedMeshRenderer s)
            {
                var skinnedMeshRenderer = decalRenderer.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = decalMaterial;
                skinnedMeshRenderer.rootBone = s.rootBone;
                skinnedMeshRenderer.bones = s.bones;
                _renderer = skinnedMeshRenderer;
            }

            if (!isStatic) decalRenderer.transform.parent = receiverRenderer.transform;

            decalRenderer.transform.localPosition = Vector3.zero;
            decalRenderer.transform.localRotation = Quaternion.identity;
            decalRenderer.transform.localScale = Vector3.one;
            decalRenderer.SetActive(false);
        }

        /// <summary>
        ///     デカールメッシュの編集が開始されたときに呼ばれる処理。
        /// </summary>
        public void OnBeginEditDecalMesh()
        {
            _renderer.gameObject.SetActive(false);
        }

        /// <summary>
        ///     デカールメッシュの編集が終了されたときに呼ばれる処理。
        /// </summary>
        public void OnEndEditDecalMesh()
        {
            _renderer.gameObject.SetActive(true);
        }
    }
}
