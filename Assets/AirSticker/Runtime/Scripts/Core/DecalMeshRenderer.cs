using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    internal sealed class DecalMeshRenderer
    {
        private readonly Renderer _renderer;
        public DecalMeshRenderer(Component receiverComponent, Material decalMaterial, Mesh mesh)
        {
            Owner = new GameObject("AirStickerRenderer");
            // Let the projector recognize this renderer without touching GameObject.name (see
            // DecalMeshRendererMarker).
            Owner.AddComponent<DecalMeshRendererMarker>();
            if (receiverComponent is MeshRenderer || receiverComponent is Terrain)
            {
                var meshRenderer = Owner.AddComponent<MeshRenderer>();
                meshRenderer.material = decalMaterial;
                var meshFilter = Owner.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                _renderer = meshRenderer;
            }
            else if (receiverComponent is SkinnedMeshRenderer s)
            {
                var skinnedMeshRenderer = Owner.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = decalMaterial;
                skinnedMeshRenderer.rootBone = s.rootBone;
                skinnedMeshRenderer.bones = s.bones;
                _renderer = skinnedMeshRenderer;
            }
            
            Owner.transform.parent = receiverComponent.transform;
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
            if (Owner) Object.Destroy(Owner);
        }
    }
}
