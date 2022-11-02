using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールメッシュのレンダラー
    /// </summary>
    public class CyDecalMeshRenderer
    {
        private readonly Renderer _renderer;
        private SkinnedMeshRenderer _skinnedMeshRenderer;
        private MeshRenderer _meshRenderer;
        private GameObject _gameObject;
        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="receiverRenderer">デカールメッシュのレシーバーオブジェクトのレンダラー</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <param name="mesh">デカールメッシュ</param>
        /// <param name="isStatic">静的オブジェクトフラグ</param>
        public CyDecalMeshRenderer(Renderer receiverRenderer, Material decalMaterial, Mesh mesh, bool isStatic)
        {
            _gameObject = new GameObject("CyDecalRenderer");
            if (receiverRenderer is MeshRenderer)
            {
                _meshRenderer = _gameObject.AddComponent<MeshRenderer>();
                _meshRenderer.material = decalMaterial;
                var meshFilter = _gameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                _renderer = _meshRenderer;
            }
            else if (receiverRenderer is SkinnedMeshRenderer s)
            {
                _skinnedMeshRenderer = _gameObject.AddComponent<SkinnedMeshRenderer>();
                _skinnedMeshRenderer.sharedMesh = mesh;
                _skinnedMeshRenderer.material = decalMaterial;
                _skinnedMeshRenderer.rootBone = s.rootBone;
                _skinnedMeshRenderer.bones = s.bones;
                _renderer = _skinnedMeshRenderer;
            }

            if (!isStatic) _gameObject.transform.parent = receiverRenderer.transform;

            _gameObject.transform.localPosition = Vector3.zero;
            _gameObject.transform.localRotation = Quaternion.identity;
            _gameObject.transform.localScale = Vector3.one;
            _gameObject.SetActive(false);
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
