using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカールメッシュのレンダラー
    /// </summary>
    public class CyDecalMeshRenderer
    {
        private readonly GameObject _owner;
        private readonly Renderer _renderer;
        public GameObject Owner => _owner;
        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="receiverRenderer">デカールメッシュのレシーバーオブジェクトのレンダラー</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <param name="mesh">デカールメッシュ</param>
        public CyDecalMeshRenderer(Renderer receiverRenderer, Material decalMaterial, Mesh mesh)
        {
            _owner = new GameObject("CyDecalRenderer");
            if (receiverRenderer is MeshRenderer)
            {
                var meshRenderer = _owner.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = _owner.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                _renderer = meshRenderer;
            }
            else if (receiverRenderer is SkinnedMeshRenderer s)
            {
                var skinnedMeshRenderer = _owner.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = decalMaterial;
                skinnedMeshRenderer.rootBone = s.rootBone;
                skinnedMeshRenderer.bones = s.bones;
                _renderer = skinnedMeshRenderer;
            }

            _owner.transform.parent = receiverRenderer.transform;
            _owner.transform.localPosition = Vector3.zero;
            _owner.transform.localRotation = Quaternion.identity;
            _owner.transform.localScale = Vector3.one;
        }

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
            Object.Destroy(_owner);
        }
    }
}
