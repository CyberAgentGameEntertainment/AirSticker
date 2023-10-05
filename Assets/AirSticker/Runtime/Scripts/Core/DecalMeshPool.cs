using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    internal interface IDecalMeshPool
    {
        int GetPoolSize();
        void DisableDecalMeshRenderers();
        void EnableDecalMeshRenderers();
        bool Contains(int hash);
        void RegisterDecalMesh(int hash, DecalMesh decalMesh);
        DecalMesh GetDecalMesh(int hash);
        void Dispose();
        void GarbageCollect();
    }

    /// <summary>
    ///     Decal mesh pool.
    /// </summary>
    public sealed class DecalMeshPool : IDecalMeshPool
    {
        private readonly Dictionary<int, DecalMesh> _decalMeshes = new Dictionary<int, DecalMesh>();

        public int GetPoolSize()
        {
            return _decalMeshes.Count;
        }

        void IDecalMeshPool.DisableDecalMeshRenderers()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.DisableDecalMeshRenderer();
        }

        void IDecalMeshPool.EnableDecalMeshRenderers()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.EnableDecalMeshRenderer();
        }

        /// <summary>
        ///     Determines if a decal mesh of the specified hash value is registered in the pool.
        /// </summary>
        /// <param name="hash">
        ///     The hash value.
        ///     It should be calculated by the CalculateHash method.
        /// </param>
        /// <returns>Returns true if the pool contains it.</returns>
        bool IDecalMeshPool.Contains(int hash)
        {
            return _decalMeshes.ContainsKey(hash);
        }

        /// <summary>
        ///     Register the decal mesh.
        /// </summary>
        /// <param name="hash">
        ///     The hash value.
        ///     It should be calculated by the CalculateHash method.
        /// </param>
        /// <param name="decalMesh">Decal mesh to be registered.</param>
        void IDecalMeshPool.RegisterDecalMesh(int hash, DecalMesh decalMesh)
        {
            _decalMeshes.Add(hash, decalMesh);
        }

        /// <summary>
        ///     Get the decal mesh from pool.
        /// </summary>
        /// <param name="hash">
        ///     The hash value.
        ///     It should be calculated by the CalculateHash method.
        /// </param>
        DecalMesh IDecalMeshPool.GetDecalMesh(int hash)
        {
            return _decalMeshes[hash];
        }

        void IDecalMeshPool.Dispose()
        {
            foreach (var item in _decalMeshes) item.Value?.Dispose();
        }

        /// <summary>
        ///     Garbage collect unreferenced decal mesh
        /// </summary>
        void IDecalMeshPool.GarbageCollect()
        {
            // Create deletable list.
            var removeList = _decalMeshes.Where(item => item.Value.CanRemoveFromPool()).ToList();
            foreach (var item in removeList)
            {
                item.Value.Dispose();
                _decalMeshes.Remove(item.Key);
            }
        }

        /// <summary>
        ///     Calculate the hash value to be registered in the pool
        /// </summary>
        public static int CalculateHash(GameObject receiverObject, Component component, Material decalMaterial)
        {
            var nameKey = $"{receiverObject.name}_{decalMaterial.name}_{component.name}";
            return nameKey.GetHashCode();
        }
    }
}
