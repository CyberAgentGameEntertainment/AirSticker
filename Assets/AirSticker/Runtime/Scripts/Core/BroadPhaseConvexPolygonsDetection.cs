using System.Collections.Generic;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     This class run broad phase convex polygons detection.
    /// </summary>
    internal static class BroadPhaseConvexPolygonsDetection
    {
        // Buffers below are reused across Execute() calls instead of being reallocated every time.
        // This is safe because DecalProjectorLauncher only ever runs one launch (and therefore one
        // Execute() call) at a time, and the buffers are fully consumed (copied out into DecalMesh)
        // before the next Execute() call can start. Buffers only grow, they never shrink.
        private static Vector3[] _pooledPositionBuffer;
        private static Vector3[] _pooledNormalBuffer;
        private static Vector3[] _pooledLocalPositionBuffer;
        private static Vector3[] _pooledLocalNormalBuffer;
        private static BoneWeight[] _pooledBoneWeightBuffer;
        private static Line[] _pooledLineBuffer;

        private static void EnsureCapacity<T>(ref T[] buffer, int requiredLength)
        {
            if (buffer != null && buffer.Length >= requiredLength) return;
            var newLength = buffer == null ? requiredLength : Mathf.Max(requiredLength, buffer.Length * 2);
            buffer = new T[newLength];
        }

        /// <summary>
        ///     Execute broad phase.
        /// </summary>
        /// <remarks>
        ///     Remove polygons that don't intersect the sphere encompassing the decal box.<br />
        ///     Also, remove polygons whose mesh orientation is opposite the decal box.<br />
        /// </remarks>
        public static List<ConvexPolygonInfo> Execute(
            Vector3 centerPosOfDecalBox,
            Vector3 decalSpaceNormalWs,
            float decalBoxWidth,
            float decalBoxHeight,
            float decalBoxDepth,
            List<ConvexPolygonInfo> convexPolygonInfos,
            bool projectionBackside)
        {
            // Calculate the radius of the sphere that encompasses the decal box.
            var radius = Mathf.Sqrt(decalBoxWidth * decalBoxWidth
                                    + decalBoxHeight * decalBoxHeight
                                    + decalBoxDepth * decalBoxDepth) * 0.5f;
            var sqrRadius = radius * radius;

            var broadPhaseConvexPolygonCount = 0;
            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                var convexPolygon = convexPolygonInfo.ConvexPolygon;
                if (!projectionBackside && Vector3.Dot(decalSpaceNormalWs, convexPolygon.FaceNormal) < 0)
                {
                    // Set the flag of outside the clip space.
                    convexPolygonInfo.IsOutsideClipSpace = true;
                    continue;
                }

                var v0 = convexPolygon.GetVertexPositionInWorldSpace(
                    convexPolygon.GetRealVertexNo(0));

                // If the plane of the polygon doesn't intersect the sphere,
                // the polygon doesn't intersect the sphere either, so it can be rejected cheaply.
                var distToPlane = Vector3.Dot(convexPolygon.FaceNormal, centerPosOfDecalBox - v0);
                if (distToPlane > radius || distToPlane < -radius)
                {
                    // Set the flag of outside the clip space.
                    convexPolygonInfo.IsOutsideClipSpace = true;
                    continue;
                }

                var v1 = convexPolygon.GetVertexPositionInWorldSpace(
                    convexPolygon.GetRealVertexNo(1));
                var v2 = convexPolygon.GetVertexPositionInWorldSpace(
                    convexPolygon.GetRealVertexNo(2));
                if (CalculateSqrDistancePointToTriangle(centerPosOfDecalBox, v0, v1, v2) > sqrRadius)
                {
                    // Set the flag of outside the clip space.
                    convexPolygonInfo.IsOutsideClipSpace = true;
                    continue;
                }

                broadPhaseConvexPolygonCount++;
            }

            System.Diagnostics.Stopwatch swAlloc = null;
            if (AirStickerPerformanceLog.Enabled) swAlloc = System.Diagnostics.Stopwatch.StartNew();

            var broadPhaseConvexPolygonInfos = new List<ConvexPolygonInfo>(broadPhaseConvexPolygonCount);
            var requiredBufferLength = ConvexPolygon.DefaultMaxVertex * broadPhaseConvexPolygonCount;
            EnsureCapacity(ref _pooledPositionBuffer, requiredBufferLength);
            EnsureCapacity(ref _pooledNormalBuffer, requiredBufferLength);
            EnsureCapacity(ref _pooledLocalPositionBuffer, requiredBufferLength);
            EnsureCapacity(ref _pooledLocalNormalBuffer, requiredBufferLength);
            EnsureCapacity(ref _pooledBoneWeightBuffer, requiredBufferLength);
            EnsureCapacity(ref _pooledLineBuffer, requiredBufferLength);
            var positionBuffer = _pooledPositionBuffer;
            var normalBuffer = _pooledNormalBuffer;
            var localPositionBuffer = _pooledLocalPositionBuffer;
            var localNormalBuffer = _pooledLocalNormalBuffer;
            var boneWeightBuffer = _pooledBoneWeightBuffer;
            var lineBuffer = _pooledLineBuffer;
            var startOffsetInBuffer = 0;

            if (swAlloc != null)
            {
                swAlloc.Stop();
                Debug.Log($"[AirSticker][Perf] BroadPhase buffer alloc: {swAlloc.Elapsed.TotalMilliseconds:F2} ms (survivors={broadPhaseConvexPolygonCount})");
            }

            for (var i = 0; i < convexPolygonInfos.Count; i++)
            {
                var convexPolygonInfo = convexPolygonInfos[i];
                if (!convexPolygonInfo.IsOutsideClipSpace)
                {
                    broadPhaseConvexPolygonInfos.Add(new ConvexPolygonInfo
                    {
                        ConvexPolygon = new ConvexPolygon(convexPolygonInfo.ConvexPolygon, positionBuffer,
                            normalBuffer, boneWeightBuffer, lineBuffer, localPositionBuffer, localNormalBuffer,
                            startOffsetInBuffer),
                        IsOutsideClipSpace = convexPolygonInfo.IsOutsideClipSpace
                    });

                    startOffsetInBuffer += ConvexPolygon.DefaultMaxVertex;
                }

                convexPolygonInfo.IsOutsideClipSpace = false;
            }

            return broadPhaseConvexPolygonInfos;
        }

        /// <summary>
        ///     Calculate the squared distance from the point to the triangle.
        /// </summary>
        /// <remarks>
        ///     The closest point on the triangle is searched by determining which Voronoi region
        ///     of the triangle (the vertices, the edges or the face) the point is in.<br />
        ///     See "5.1.5 Closest Point on Triangle to Point" in "Real-Time Collision Detection" for details.
        /// </remarks>
        public static float CalculateSqrDistancePointToTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;

            // Check if the point is in the vertex region of a.
            var d1 = Vector3.Dot(ab, ap);
            var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0.0f && d2 <= 0.0f) return ap.sqrMagnitude;

            // Check if the point is in the vertex region of b.
            var bp = p - b;
            var d3 = Vector3.Dot(ab, bp);
            var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0.0f && d4 <= d3) return bp.sqrMagnitude;

            // Check if the point is in the edge region of ab.
            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
            {
                var v = d1 / (d1 - d3);
                return (ap - ab * v).sqrMagnitude;
            }

            // Check if the point is in the vertex region of c.
            var cp = p - c;
            var d5 = Vector3.Dot(ab, cp);
            var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0.0f && d5 <= d6) return cp.sqrMagnitude;

            // Check if the point is in the edge region of ac.
            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
            {
                var w = d2 / (d2 - d6);
                return (ap - ac * w).sqrMagnitude;
            }

            // Check if the point is in the edge region of bc.
            var va = d3 * d6 - d5 * d4;
            if (va <= 0.0f && d4 - d3 >= 0.0f && d5 - d6 >= 0.0f)
            {
                var w = (d4 - d3) / (d4 - d3 + (d5 - d6));
                return (bp - (c - b) * w).sqrMagnitude;
            }

            // The point is in the face region.
            var denom = 1.0f / (va + vb + vc);
            return (ap - ab * (vb * denom) - ac * (vc * denom)).sqrMagnitude;
        }
    }
}
