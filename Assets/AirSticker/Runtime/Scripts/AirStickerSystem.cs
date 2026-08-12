using System.Collections.Generic;
using System.Threading;
using AirSticker.Runtime.Scripts.Core;
using AirSticker.Runtime.Scripts.Core.Jobs;
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

        // The working lists of CollectEditDecalMeshes, reused so that gathering the receiver's renderers does
        // not allocate an array per launch. The method does not yield and only one launch runs at a time, so
        // the lists are never read across calls.
        private readonly List<Renderer> _collectedRenderers = new List<Renderer>();
        private readonly List<Terrain> _collectedTerrains = new List<Terrain>();

        private TrianglePolygonsFactory _trianglePolygonsFactory;
        private DecalMeshJobPipeline _jobPipeline;

        /// <summary>
        ///     The job pipeline that runs skinning / broad phase / clip / build for a launch.
        /// </summary>
        internal static DecalMeshJobPipeline JobPipeline => Instance ? Instance._jobPipeline : null;

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
            _jobPipeline = new DecalMeshJobPipeline();
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
            _jobPipeline.Dispose();
            _receiverObjectTrianglePolygonsPool.DisposeAll();
            Instance = null;
        }

        // Returns the factory's Awaitable directly instead of awaiting it in an async method of its own, so
        // the call does not build a second state machine per launch.
        internal static Awaitable BuildTrianglePolygonsFromReceiverObjectAsync(
            IReadOnlyList<MeshRenderer> meshRenderers,
            IReadOnlyList<SkinnedMeshRenderer> skinnedMeshRenderers,
            IReadOnlyList<Terrain> terrains,
            ReceiverConvexPolygonsMesh[] resultHolder,
            CancellationToken cancellation)
        {
            return Instance._trianglePolygonsFactory.BuildFromReceiverObjectAsync(
                meshRenderers,
                skinnedMeshRenderers,
                terrains,
                resultHolder,
                cancellation);
        }

        internal static ReceiverConvexPolygonsMesh GetTrianglePolygonsFromPool(GameObject receiverObject)
        {
            if (Instance == null) return null;

            return ReceiverObjectTrianglePolygonsPool.Get(receiverObject);
        }

        internal static void CollectEditDecalMeshes(
            List<DecalMesh> results,
            GameObject receiverObject,
            Material decalMaterial,
            int groupId)
        {
            // We want to collect only the renderer of receiver objects,
            // But the renderer of decal mesh hanging from receiver object.
            // Therefore, temporarily disable to the renderer of decal mesh.
            Instance._decalMeshPool.DisableDecalMeshRenderers();
            var renderers = Instance._collectedRenderers;
            receiverObject.GetComponentsInChildren(false, renderers);
            foreach (var renderer in renderers)
            {
                if (!renderer) return;
                var pool = Instance._decalMeshPool;
                var hash = DecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial, groupId);

                if (pool.Contains(hash))
                {
                    results.Add(pool.GetDecalMesh(hash));
                }
                else
                {
                    var newMesh = new DecalMesh(receiverObject, decalMaterial, renderer, groupId);
                    results.Add(newMesh);
                    pool.RegisterDecalMesh(hash, newMesh);
                }
            }

            var terrains = Instance._collectedTerrains;
            receiverObject.GetComponentsInChildren(false, terrains);
            foreach (var terrain in terrains)
            {
                if (!terrain) return;
                var pool = Instance._decalMeshPool;
                var hash = DecalMeshPool.CalculateHash(receiverObject, terrain, decalMaterial, groupId);

                if (pool.Contains(hash))
                {
                    results.Add(pool.GetDecalMesh(hash));
                }
                else
                {
                    var newMesh = new DecalMesh(receiverObject, decalMaterial, terrain, groupId);
                    results.Add(newMesh);
                    pool.RegisterDecalMesh(hash, newMesh);
                }
            }

            // Restore the renderer of decal mesh was disabled.
            Instance._decalMeshPool.EnableDecalMeshRenderers();
        }
    }
}
