using System.Collections;
using System.Collections.Generic;
using AirSticker.Runtime.Scripts.Core;
using UnityEngine;

namespace AirSticker.Runtime.Scripts
{
    /// <summary>
    ///     Decal System.
    ///     Using design patterns.
    ///         1. Facade
    ///         2. Singleton
    /// </summary>
    public sealed class AirStickerSystem : MonoBehaviour
    {
        private readonly IDecalMeshPool _decalMeshPool = new DecalMeshPool();

        private readonly IDecalProjectorLauncher _decalProjectorLauncher =
            new DecalProjectorLauncher();

        private readonly IReceiverObjectTrianglePolygonsPool _receiverObjectTrianglePolygonsPool =
            new ReceiverObjectTrianglePolygonsPool();

        private TrianglePolygonsFactory _trianglePolygonsFactory;

        public static DecalProjectorLauncher DecalProjectorLauncher
        {
            get
            {
                if (!Instance) return null;
                return (DecalProjectorLauncher)Instance._decalProjectorLauncher;
            }
        }

        public static DecalMeshPool DecalMeshPool
        {
            get
            {
                if (!Instance) return null;
                return (DecalMeshPool)Instance._decalMeshPool;
            }
        }

        public static ReceiverObjectTrianglePolygonsPool ReceiverObjectTrianglePolygonsPool
        {
            get
            {
                if (!Instance) return null;
                return (ReceiverObjectTrianglePolygonsPool)Instance._receiverObjectTrianglePolygonsPool;
            }
        }

        private static AirStickerSystem Instance { get; set; }

        private void Awake()
        {
            Debug.Assert(Instance == null,
                "AirStickerSystem can't be instantiated multiply, but but it has already been instantiated.");
            Instance = this;
            _trianglePolygonsFactory = new TrianglePolygonsFactory();
        }

        private void Update()
        {
            _receiverObjectTrianglePolygonsPool.GarbageCollect();
            _decalMeshPool.GarbageCollect();
            _decalProjectorLauncher.Update();
        }

        private void OnDestroy()
        {
            _decalMeshPool.Dispose();
            _trianglePolygonsFactory.Dispose();
            Instance = null;
        }

        internal static IEnumerator BuildTrianglePolygonsFromReceiverObject(
            MeshFilter[] meshFilters,
            MeshRenderer[] meshRenderers,
            SkinnedMeshRenderer[] skinnedMeshRenderers,
            Terrain[] terrains,
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            yield return Instance._trianglePolygonsFactory.BuildFromReceiverObject(
                meshFilters,
                meshRenderers,
                skinnedMeshRenderers,
                terrains,
                convexPolygonInfos);
        }

        internal static List<ConvexPolygonInfo> GetTrianglePolygonsFromPool(GameObject receiverObject)
        {
            if (Instance == null) return null;

            var convexPolygonInfos = Instance._receiverObjectTrianglePolygonsPool.ConvexPolygonsPool[receiverObject];
            foreach (var info in convexPolygonInfos) info.IsOutsideClipSpace = false;
            return convexPolygonInfos;
        }

        internal static void CollectEditDecalMeshes(
            List<DecalMesh> results,
            GameObject receiverObject,
            Material decalMaterial)
        {
            // We want to collect only the renderer of receiver objects,
            // But the renderer of decal mesh hanging from receiver object.
            // Therefore, temporarily disable to the renderer of decal mesh.
            Instance._decalMeshPool.DisableDecalMeshRenderers();
            var renderers = receiverObject.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!renderer) return;
                var pool = Instance._decalMeshPool;
                var hash = DecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial);

                if (pool.Contains(hash))
                {
                    results.Add(pool.GetDecalMesh(hash));
                }
                else
                {
                    var newMesh = new DecalMesh(receiverObject, decalMaterial, renderer);
                    results.Add(newMesh);
                    pool.RegisterDecalMesh(hash, newMesh);
                }
            }

            var terrains = receiverObject.GetComponentsInChildren<Terrain>();
            foreach (var terrain in terrains)
            {
                if (!terrain) return;
                var pool = Instance._decalMeshPool;
                var hash = DecalMeshPool.CalculateHash(receiverObject, terrain, decalMaterial);

                if (pool.Contains(hash))
                {
                    results.Add(pool.GetDecalMesh(hash));
                }
                else
                {
                    var newMesh = new DecalMesh(receiverObject, decalMaterial, terrain);
                    results.Add(newMesh);
                    pool.RegisterDecalMesh(hash, newMesh);
                }
            }

            // Restore the renderer of decal mesh was disabled.
            Instance._decalMeshPool.EnableDecalMeshRenderers();
        }
    }
}
