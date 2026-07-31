using Unity.Collections;
using Unity.Mathematics;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     NativeArray port of <see cref="DecalMeshTangentCalculator" />, used by the mesh build job.
    /// </summary>
    /// <remarks>
    ///     Same per-triangle accumulation + Gram-Schmidt orthogonalization (Lengyel) as the managed version,
    ///     rewritten on NativeArray / Unity.Mathematics types so it runs inside a job (and is Burst-ready).
    ///     The accumulation scratch is passed in by the caller so the job can reuse it across decal meshes.
    /// </remarks>
    internal static class DecalMeshTangents
    {
        /// <summary>
        ///     Compute the tangents of the appended vertex range [vertexStart, vertexStart + vertexCount).
        /// </summary>
        public static void ComputeTangents(
            NativeArray<float3> positions,
            NativeArray<float3> normals,
            NativeArray<float2> uvs,
            NativeArray<int> indices,
            NativeArray<float4> tangents,
            NativeArray<float3> tangentAccumulation,
            NativeArray<float3> bitangentAccumulation,
            int vertexStart,
            int vertexCount,
            int indexStart,
            int indexCount)
        {
            if (vertexCount <= 0) return;

            for (var i = 0; i < vertexCount; i++)
            {
                tangentAccumulation[i] = float3.zero;
                bitangentAccumulation[i] = float3.zero;
            }

            var indexEnd = indexStart + indexCount;
            for (var i = indexStart; i < indexEnd; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var p0 = positions[i0];
                var p1 = positions[i1];
                var p2 = positions[i2];
                var w0 = uvs[i0];
                var w1 = uvs[i1];
                var w2 = uvs[i2];

                var x1 = p1.x - p0.x;
                var x2 = p2.x - p0.x;
                var y1 = p1.y - p0.y;
                var y2 = p2.y - p0.y;
                var z1 = p1.z - p0.z;
                var z2 = p2.z - p0.z;

                var s1 = w1.x - w0.x;
                var s2 = w2.x - w0.x;
                var t1 = w1.y - w0.y;
                var t2 = w2.y - w0.y;

                var det = s1 * t2 - s2 * t1;
                // Degenerate UVs (e.g. a polygon almost perpendicular to the decal plane) have no meaningful
                // tangent direction, so skip the accumulation.
                if (det > -1e-8f && det < 1e-8f) continue;

                var r = 1.0f / det;
                var tangentDir = new float3(
                    (t2 * x1 - t1 * x2) * r,
                    (t2 * y1 - t1 * y2) * r,
                    (t2 * z1 - t1 * z2) * r);
                var bitangentDir = new float3(
                    (s1 * x2 - s2 * x1) * r,
                    (s1 * y2 - s2 * y1) * r,
                    (s1 * z2 - s2 * z1) * r);

                tangentAccumulation[i0 - vertexStart] += tangentDir;
                tangentAccumulation[i1 - vertexStart] += tangentDir;
                tangentAccumulation[i2 - vertexStart] += tangentDir;
                bitangentAccumulation[i0 - vertexStart] += bitangentDir;
                bitangentAccumulation[i1 - vertexStart] += bitangentDir;
                bitangentAccumulation[i2 - vertexStart] += bitangentDir;
            }

            for (var i = 0; i < vertexCount; i++)
            {
                var normal = normals[vertexStart + i];
                var tangent = tangentAccumulation[i];

                // Gram-Schmidt orthogonalization against the normal.
                tangent -= normal * math.dot(normal, tangent);
                var magnitude = math.length(tangent);
                if (magnitude > 1e-8f)
                    tangent /= magnitude;
                else
                    // No valid accumulation (degenerate UVs), so use any direction orthogonal to the normal.
                    tangent = BuildOrthogonalUnitVector(normal);

                var w = math.dot(math.cross(normal, tangent), bitangentAccumulation[i]) < 0.0f ? -1.0f : 1.0f;
                tangents[vertexStart + i] = new float4(tangent, w);
            }
        }

        private static float3 BuildOrthogonalUnitVector(float3 normal)
        {
            var axis = math.abs(normal.x) < 0.9f ? new float3(1.0f, 0.0f, 0.0f) : new float3(0.0f, 1.0f, 0.0f);
            var orthogonal = axis - normal * math.dot(normal, axis);
            var magnitude = math.length(orthogonal);
            return magnitude > 1e-8f ? orthogonal / magnitude : new float3(1.0f, 0.0f, 0.0f);
        }
    }
}
