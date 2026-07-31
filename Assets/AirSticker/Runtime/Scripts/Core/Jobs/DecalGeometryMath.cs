using Unity.Mathematics;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Pure geometry helpers shared by the decal mesh jobs. No Unity API objects, so the whole class is
    ///     Burst-compatible and can be unit-tested directly.
    /// </summary>
    internal static class DecalGeometryMath
    {
        /// <summary>
        ///     Normalize with the same semantics as <c>UnityEngine.Vector3.normalized</c>: return the zero
        ///     vector when the length is below Unity's epsilon, instead of producing NaN like math.normalize.
        /// </summary>
        /// <remarks>
        ///     Matching this behavior keeps degenerate (zero-area) triangles from diverging between the old
        ///     CPU path and the job path.
        /// </remarks>
        public static float3 NormalizeSafe(float3 v)
        {
            var length = math.length(v);
            return length > 1e-5f ? v / length : float3.zero;
        }

        /// <summary>
        ///     Squared distance from the point to the triangle (a, b, c).
        /// </summary>
        /// <remarks>
        ///     Direct port of BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle.
        ///     See "5.1.5 Closest Point on Triangle to Point" in "Real-Time Collision Detection".
        /// </remarks>
        public static float CalculateSqrDistancePointToTriangle(float3 p, float3 a, float3 b, float3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;

            var d1 = math.dot(ab, ap);
            var d2 = math.dot(ac, ap);
            if (d1 <= 0.0f && d2 <= 0.0f) return math.lengthsq(ap);

            var bp = p - b;
            var d3 = math.dot(ab, bp);
            var d4 = math.dot(ac, bp);
            if (d3 >= 0.0f && d4 <= d3) return math.lengthsq(bp);

            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
            {
                var v = d1 / (d1 - d3);
                return math.lengthsq(ap - ab * v);
            }

            var cp = p - c;
            var d5 = math.dot(ab, cp);
            var d6 = math.dot(ac, cp);
            if (d6 >= 0.0f && d5 <= d6) return math.lengthsq(cp);

            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
            {
                var w = d2 / (d2 - d6);
                return math.lengthsq(ap - ac * w);
            }

            var va = d3 * d6 - d5 * d4;
            if (va <= 0.0f && d4 - d3 >= 0.0f && d5 - d6 >= 0.0f)
            {
                var w = (d4 - d3) / (d4 - d3 + (d5 - d6));
                return math.lengthsq(bp - (c - b) * w);
            }

            var denom = 1.0f / (va + vb + vc);
            return math.lengthsq(ap - ab * (vb * denom) - ac * (vc * denom));
        }
    }
}
