using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     The information of convex polygon.
    /// </summary>
    public class ConvexPolygonInfo
    {
        public ConvexPolygon ConvexPolygon { get; set; }
        /// <summary>
        ///     This flag indicates whether the convex polygon is outside the clip space defined by the decal box.
        /// </summary>
        public bool IsOutsideClipSpace { get; set; } 
    }

    internal interface IReceiverObjectTrianglePolygonsPool
    {
        IReadOnlyDictionary<GameObject, List<ConvexPolygonInfo>> ConvexPolygonsPool { get; }

        bool Contains(GameObject receiverObject);
        void GarbageCollect();
    }

    /// <summary>
    ///     Triangle polygon pool of receiver object.<br />
    ///     Triangle polygons are registered in the pool with the receiver object as the key.<br />
    /// </summary>
    public sealed class ReceiverObjectTrianglePolygonsPool : IReceiverObjectTrianglePolygonsPool
    {
        private readonly Dictionary<GameObject, List<ConvexPolygonInfo>> _trianglePolygonsPool =
            new Dictionary<GameObject, List<ConvexPolygonInfo>>();

        IReadOnlyDictionary<GameObject, List<ConvexPolygonInfo>> IReceiverObjectTrianglePolygonsPool.
            ConvexPolygonsPool => _trianglePolygonsPool;

        /// <summary>
        ///     Check to the triangle polygons of the receiver object is already registered.
        /// </summary>
        /// <returns>If receiver object is already registered, return true.</returns>
        public bool Contains(GameObject receiverObject)
        {
            return _trianglePolygonsPool.ContainsKey(receiverObject);
        }

        /// <summary>
        ///     If the receiver object that is registered is dead, it is removed from pool.  
        /// </summary>
        void IReceiverObjectTrianglePolygonsPool.GarbageCollect()
        {
            var deleteList = _trianglePolygonsPool.Where(item => item.Key == null).ToList();
            foreach (var item in deleteList) _trianglePolygonsPool.Remove(item.Key);
        }
        
        public void RegisterTrianglePolygons(GameObject receiverObject, List<ConvexPolygonInfo> trianglePolygonInfos)
        {
            if (receiverObject
                && !this.Contains(receiverObject)) 
                _trianglePolygonsPool.Add(receiverObject, trianglePolygonInfos);
        }
        
        public int GetPoolSize()
        {
            return _trianglePolygonsPool.Count;
        }
    }
}
