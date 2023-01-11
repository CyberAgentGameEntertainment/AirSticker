using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    internal interface ICyDecalMeshPool
    {
        int GetPoolSize();
        void DisableDecalMeshRenderers();
        void EnableDecalMeshRenderers();
        bool Contains(int hash);
        void RegisterDecalMesh(int hash, CyDecalMesh decalMesh);
        CyDecalMesh GetDecalMesh(int hash);
        void Dispose();
        void GarbageCollect();
    }
    /// <summary>
    ///     Decal mesh pool.
    /// </summary>
    public sealed class CyDecalMeshPool : ICyDecalMeshPool
    {
        private readonly Dictionary<int, CyDecalMesh> _decalMeshes = new Dictionary<int, CyDecalMesh>();
        
        public int GetPoolSize()
        {
            return _decalMeshes.Count;
        }
        
        void ICyDecalMeshPool.DisableDecalMeshRenderers()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.DisableDecalMeshRenderer();
        }
        
        void ICyDecalMeshPool.EnableDecalMeshRenderers()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.EnableDecalMeshRenderer();
        }

        /// <summary>
        ///     Calculate the hash value to be registered in the pool
        /// </summary>
        public static int CalculateHash(GameObject receiverObject, Renderer renderer, Material decalMaterial)
        {
            return receiverObject.GetInstanceID()
                   + decalMaterial.name.GetHashCode()
                   + renderer.GetInstanceID();
        }

        /// <summary>
        ///     Determines if a decal mesh of the specified hash value is registered in the pool.
        /// 
        /// </summary>
        /// <param name="hash">
        ///     The hash value.
        ///     It should be calculated by the CalculateHash method.
        /// </param>
        /// <returns>Returns true if the pool contains it.</returns>
        bool ICyDecalMeshPool.Contains(int hash)
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
        void ICyDecalMeshPool.RegisterDecalMesh(int hash, CyDecalMesh decalMesh)
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
        CyDecalMesh ICyDecalMeshPool.GetDecalMesh(int hash)
        {
            return _decalMeshes[hash];
        }

        void ICyDecalMeshPool.Dispose()
        {
            foreach (var item in _decalMeshes)
            {
                item.Value?.Dispose();
            }
        }
        /// <summary>
        ///     Garbage collect unreferenced decal mesh
        /// </summary>
        void ICyDecalMeshPool.GarbageCollect()
        {
            // Create deletable list.
            var removeList = _decalMeshes.Where(item => item.Value.CanRemoveFromPool()).ToList();
            foreach (var item in removeList)
            {
                item.Value.Dispose();
                _decalMeshes.Remove(item.Key);
            }
        }
    }
}
