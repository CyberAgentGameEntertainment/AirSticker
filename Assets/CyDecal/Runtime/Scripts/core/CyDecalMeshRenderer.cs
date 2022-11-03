using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカールメッシュのレンダラー
    /// </summary>
    public class CyDecalMeshRenderer
    {
        private readonly GameObject _gameObject;
        private readonly Renderer _renderer;

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="receiverRenderer">デカールメッシュのレシーバーオブジェクトのレンダラー</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <param name="mesh">デカールメッシュ</param>
        public CyDecalMeshRenderer(Renderer receiverRenderer, Material decalMaterial, Mesh mesh)
        {
            _gameObject = new GameObject("CyDecalRenderer");
            if (receiverRenderer is MeshRenderer)
            {
                var meshRenderer = _gameObject.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = _gameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                _renderer = meshRenderer;
            }
            else if (receiverRenderer is SkinnedMeshRenderer s)
            {
                var skinnedMeshRenderer = _gameObject.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = decalMaterial;
                skinnedMeshRenderer.rootBone = s.rootBone;
                skinnedMeshRenderer.bones = s.bones;
                _renderer = skinnedMeshRenderer;
            }

            _gameObject.transform.parent = receiverRenderer.transform;
            _gameObject.transform.localPosition = Vector3.zero;
            _gameObject.transform.localRotation = Quaternion.identity;
            _gameObject.transform.localScale = Vector3.one;
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
            Object.Destroy(_gameObject);
        }
    }
}
