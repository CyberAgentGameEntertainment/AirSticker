using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
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
        public CyDecalMeshRenderer(Renderer receiverRenderer, Material decalMaterial, Mesh mesh)
        {
            Owner = new GameObject("CyDecalRenderer");
            if (receiverRenderer is MeshRenderer)
            {
                var meshRenderer = Owner.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = Owner.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                _renderer = meshRenderer;
            }
            else if (receiverRenderer is SkinnedMeshRenderer s)
            {
                var skinnedMeshRenderer = Owner.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = decalMaterial;
                skinnedMeshRenderer.rootBone = s.rootBone;
                skinnedMeshRenderer.bones = s.bones;
                _renderer = skinnedMeshRenderer;
            }

            Owner.transform.parent = receiverRenderer.transform;
            Owner.transform.localPosition = Vector3.zero;
            Owner.transform.localRotation = Quaternion.identity;
            Owner.transform.localScale = Vector3.one;
        }

        public GameObject Owner { get; }

        /// <summary>
        ///     デカールメッシュレンダラーを無効にする。
        /// </summary>
        public void DisableDecalMeshRenderer()
        {
            _renderer.gameObject.SetActive(false);
        }

        /// <summary>
        ///     デカールメッシュレンダラーを有効にする。
        /// </summary>
        public void EnableDecalMeshRenderer()
        {
            _renderer.gameObject.SetActive(true);
        }

        public void Destroy()
        {
            Object.Destroy(Owner);
        }
    }
}
