using System.Collections.Generic;
using System.Linq;
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
            var deleteList = _trianglePolygonsPool
                .Where(item => item.Key == null && (item.Value == null || !item.Value.InUse)).ToList();
            foreach (var item in deleteList)
            {
                item.Value?.Dispose();
                _trianglePolygonsPool.Remove(item.Key);
            }
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
