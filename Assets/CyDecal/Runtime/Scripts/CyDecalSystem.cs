using System.Collections;
using System.Collections.Generic;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     Decal System.
    ///     Using design patterns.
    ///         1. Facade
    ///         2. Singleton
    /// </summary>
    public sealed class CyDecalSystem : MonoBehaviour
    {
        private readonly ICyDecalMeshPool _decalMeshPool = new CyDecalMeshPool();
        
        private readonly ICyDecalProjectorLauncher _decalProjectorLauncher =
            new CyDecalProjectorLauncher();
        
        private readonly ICyReceiverObjectTrianglePolygonsPool _receiverObjectTrianglePolygonsPool =
            new CyReceiverObjectTrianglePolygonsPool();

        private CyTrianglePolygonsFactory _trianglePolygonsFactory;

        public static CyDecalProjectorLauncher DecalProjectorLauncher
        {
            get
            {
                if (!Instance) return null;
                return (CyDecalProjectorLauncher)Instance._decalProjectorLauncher;
            }
        }

        public static CyDecalMeshPool DecalMeshPool
        {
            get
            {
                if (!Instance) return null;
                return (CyDecalMeshPool)Instance._decalMeshPool;
            }
        }

        public static CyReceiverObjectTrianglePolygonsPool ReceiverObjectTrianglePolygonsPool
        {
            get
            {
                if (!Instance) return null;
                return (CyReceiverObjectTrianglePolygonsPool)Instance._receiverObjectTrianglePolygonsPool;
            }
        }

        private static CyDecalSystem Instance { get; set; }

        private void Awake()
        {
            Debug.Assert(Instance == null,
                "CyDecalSystem can't be instantiated multiply, but but it has already been instantiated.");
            Instance = this;
            _trianglePolygonsFactory = new CyTrianglePolygonsFactory();
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
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            yield return Instance._trianglePolygonsFactory.BuildFromReceiverObject(
                meshFilters,
                meshRenderers,
                skinnedMeshRenderers,
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
            List<CyDecalMesh> results,
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
                var hash = CyDecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial);
                if (pool.Contains(hash))
                {
                    results.Add(pool.GetDecalMesh(hash));
                }
                else
                {
                    var newMesh = new CyDecalMesh(receiverObject, decalMaterial, renderer);
                    results.Add(newMesh);
                    pool.RegisterDecalMesh(hash, newMesh);
                }
            }
            
            // Restore the renderer of decal mesh was disabled.
            Instance._decalMeshPool.EnableDecalMeshRenderers();
        }
    }
}
