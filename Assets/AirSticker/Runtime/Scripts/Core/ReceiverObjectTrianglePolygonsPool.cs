using System.Collections.Generic;
using AirSticker.Runtime.Scripts.Core.Jobs;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    internal interface IReceiverObjectTrianglePolygonsPool
    {
        IReadOnlyDictionary<GameObject, ReceiverConvexPolygonsMesh> Pool { get; }

        bool Contains(GameObject receiverObject);
        void GarbageCollect();
        void DisposeAll();
    }

    /// <summary>
    ///     Triangle polygon pool of receiver objects.<br />
    ///     The source triangle polygons (<see cref="ReceiverConvexPolygonsMesh" />) are registered with the
    ///     receiver object as the key so repeat projections skip the mesh extraction.
    /// </summary>
    /// <remarks>
    ///     The pooled meshes own NativeArrays, so a dead entry (its receiver was destroyed) is disposed when
    ///     it is garbage collected, and everything is disposed when the AirStickerSystem is destroyed.
    /// </remarks>
    public sealed class ReceiverObjectTrianglePolygonsPool : IReceiverObjectTrianglePolygonsPool
    {
        private readonly Dictionary<GameObject, ReceiverConvexPolygonsMesh> _trianglePolygonsPool =
            new Dictionary<GameObject, ReceiverConvexPolygonsMesh>();

        // The keys to delete in the current GarbageCollect call. It is reused across calls because
        // GarbageCollect runs every frame, so allocating a list per call would produce constant GC garbage.
        private readonly List<GameObject> _deleteKeys = new List<GameObject>();

        IReadOnlyDictionary<GameObject, ReceiverConvexPolygonsMesh> IReceiverObjectTrianglePolygonsPool.Pool =>
            _trianglePolygonsPool;

        /// <summary>
        ///     Check to the triangle polygons of the receiver object is already registered.
        /// </summary>
        /// <returns>If receiver object is already registered, return true.</returns>
        public bool Contains(GameObject receiverObject)
        {
            return _trianglePolygonsPool.ContainsKey(receiverObject);
        }

        /// <summary>
        ///     If the receiver object that is registered is dead, it is removed from the pool and disposed.
        /// </summary>
        void IReceiverObjectTrianglePolygonsPool.GarbageCollect()
        {
            // A mesh whose jobs are still running (InUse) is kept even if its receiver died, so the jobs
            // never read freed NativeArrays; a later GarbageCollect disposes it once InUse is cleared.
            // The dictionary is enumerated directly (its enumerator is a struct) and the dead keys are
            // gathered into the reused list, because entries cannot be removed while enumerating. Doing
            // this with LINQ would allocate every frame even when there is nothing to delete.
            foreach (var item in _trianglePolygonsPool)
                if (item.Key == null && (item.Value == null || !item.Value.InUse))
                    _deleteKeys.Add(item.Key);

            for (var i = 0; i < _deleteKeys.Count; i++)
            {
                var key = _deleteKeys[i];
                if (_trianglePolygonsPool.TryGetValue(key, out var mesh)) mesh?.Dispose();
                _trianglePolygonsPool.Remove(key);
            }

            _deleteKeys.Clear();
        }

        void IReceiverObjectTrianglePolygonsPool.DisposeAll()
        {
            foreach (var item in _trianglePolygonsPool) item.Value?.Dispose();
            _trianglePolygonsPool.Clear();
        }

        internal void RegisterTrianglePolygons(GameObject receiverObject, ReceiverConvexPolygonsMesh trianglePolygons)
        {
            if (receiverObject && !Contains(receiverObject))
                _trianglePolygonsPool.Add(receiverObject, trianglePolygons);
        }

        internal ReceiverConvexPolygonsMesh Get(GameObject receiverObject)
        {
            return _trianglePolygonsPool.TryGetValue(receiverObject, out var mesh) ? mesh : null;
        }

        public int GetPoolSize()
        {
            return _trianglePolygonsPool.Count;
        }
    }
}
