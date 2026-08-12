using System;
using System.Collections.Generic;
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

        // The hashes to remove in the current call. They are reused across calls because GarbageCollect
        // runs every frame, so allocating a list per call would produce constant GC garbage. The two
        // methods keep their own list so a removal requested by the user cannot disturb a running
        // garbage collection.
        private readonly List<int> _garbageCollectHashes = new List<int>();
        private readonly List<int> _removeHashes = new List<int>();

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
            // Gather the deletable hashes into the reused list. The dictionary is enumerated directly (its
            // enumerator is a struct) because entries cannot be removed while enumerating. Doing this with
            // LINQ would allocate every frame even when there is nothing to remove.
            foreach (var item in _decalMeshes)
                if (item.Value.CanRemoveFromPool())
                    _garbageCollectHashes.Add(item.Key);

            for (var i = 0; i < _garbageCollectHashes.Count; i++)
            {
                var hash = _garbageCollectHashes[i];
                if (_decalMeshes.TryGetValue(hash, out var decalMesh)) decalMesh.Dispose();
                _decalMeshes.Remove(hash);
            }

            _garbageCollectHashes.Clear();
        }

        /// <summary>
        ///     Remove the decal meshes that belong to the specified group from the pool.
        /// </summary>
        /// <remarks>
        ///     The removed decal meshes are disposed and their renderers are destroyed. <br />
        ///     Call this method after the projectors of the target group have finished launching.
        /// </remarks>
        /// <param name="groupId">The group ID of the decal meshes to be removed.</param>
        /// <param name="receiverObject">
        ///     If it is not null, only the decal meshes projected to this receiver object are removed.
        /// </param>
        /// <param name="decalMaterial">
        ///     If it is not null, only the decal meshes using this decal material are removed.
        /// </param>
        public void RemoveDecalMeshes(
            int groupId,
            GameObject receiverObject = null,
            Material decalMaterial = null)
        {
            foreach (var item in _decalMeshes)
                if (item.Value.GroupId == groupId
                    && (receiverObject == null || item.Value.ReceiverObject == receiverObject)
                    && (decalMaterial == null || item.Value.DecalMaterial == decalMaterial))
                    _removeHashes.Add(item.Key);

            for (var i = 0; i < _removeHashes.Count; i++)
            {
                var hash = _removeHashes[i];
                if (_decalMeshes.TryGetValue(hash, out var decalMesh))
                {
                    decalMesh.DestroyDecalMeshRenderer();
                    decalMesh.Dispose();
                }

                _decalMeshes.Remove(hash);
            }

            _removeHashes.Clear();
        }

        /// <summary>
        ///     Calculate the hash value to be registered in the pool
        /// </summary>
        public static int CalculateHash(GameObject receiverObject, Component component, Material decalMaterial,
            int groupId = 0)
        {
            // Use instance IDs instead of names, because different objects can have the same name
            // (e.g. clones of the same prefab) and must not share a decal mesh.
            // The IDs are combined with HashCode instead of being formatted into a string, because this is
            // called once per renderer of the receiver object on every launch and the string interpolation
            // allocated for each of them.
            return HashCode.Combine(
                receiverObject.GetInstanceID(),
                decalMaterial.GetInstanceID(),
                component.GetInstanceID(),
                groupId);
        }
    }
}
