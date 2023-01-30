using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    internal sealed class DecalMeshRenderer
    {
        private readonly Renderer _renderer;

        public DecalMeshRenderer(Renderer receiverRenderer, Material decalMaterial, Mesh mesh)
        {
            Owner = new GameObject("AirStickerRenderer");
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

        private GameObject Owner { get; }

        public void DisableDecalMeshRenderer()
        {
            _renderer.gameObject.SetActive(false);
        }

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
