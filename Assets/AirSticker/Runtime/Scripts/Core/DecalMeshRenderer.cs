using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    internal sealed class DecalMeshRenderer
    {
        private readonly Component _receiverComponent;

        public DecalMeshRenderer(Component receiverComponent, Material decalMaterial, Mesh mesh)
        {
            Owner = new GameObject("AirStickerRenderer");
            if (receiverComponent is MeshRenderer || receiverComponent is Terrain)
            {
                var meshRenderer = Owner.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = Owner.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
            }
            else if (receiverComponent is SkinnedMeshRenderer s)
            {
                var skinnedMeshRenderer = Owner.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = decalMaterial;
                skinnedMeshRenderer.rootBone = s.rootBone;
                skinnedMeshRenderer.bones = s.bones;
            }

            _receiverComponent = receiverComponent;
            Owner.transform.parent = _receiverComponent.transform;
            Owner.transform.localPosition = Vector3.zero;
            Owner.transform.localRotation = Quaternion.identity;
            Owner.transform.localScale = Vector3.one;
        }

        private GameObject Owner { get; }

        public void DisableDecalMeshRenderer()
        {
            _receiverComponent.gameObject.SetActive(false);
        }

        public void EnableDecalMeshRenderer()
        {
            _receiverComponent.gameObject.SetActive(true);
        }

        public void Destroy()
        {
            Object.Destroy(Owner);
        }
    }
}
