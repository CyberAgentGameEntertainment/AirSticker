using System;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     CPU implementation of the tangent calculation that replaces Mesh.RecalculateTangents().
    /// </summary>
    /// <remarks>
    ///     It uses only plain arrays and math (no Unity API objects), so it can run on the worker
    ///     thread and can be converted to a Burst job in Step 3 of the Job System migration.
    ///     The tangents are computed with the per-triangle accumulation method
    ///     (E. Lengyel, "Mathematics for 3D Game Programming and Computer Graphics"),
    ///     which is the same family of algorithm as Unity's built-in RecalculateTangents().
    /// </remarks>
    internal static class DecalMeshTangentCalculator
    {
        // Accumulation buffers pooled across launches to avoid per-launch managed allocations.
        // Sharing static buffers is safe because DecalProjectorLauncher runs only one launch
        // (= one worker thread) at a time and the decal meshes are processed sequentially in it.
        private static Vector3[] _tangentAccumulation = Array.Empty<Vector3>();
        private static Vector3[] _bitangentAccumulation = Array.Empty<Vector3>();

        /// <summary>
        ///     Calculate the tangents of the vertex range appended by the current launch.
        /// </summary>
        /// <remarks>
        ///     The appended geometry is self-contained (its triangles never reference vertices
        ///     outside the appended range), so calculating only the appended range yields the
        ///     same result as recalculating the whole mesh.
        /// </remarks>
        public static void CalculateTangents(
            Vector3[] positions,
            Vector3[] normals,
            Vector2[] uvs,
            int[] indices,
            Vector4[] tangents,
            int vertexStart,
            int vertexCount,
            int indexStart,
            int indexCount)
        {
            if (vertexCount <= 0) return;

            EnsureCapacity(vertexCount);
            Array.Clear(_tangentAccumulation, 0, vertexCount);
            Array.Clear(_bitangentAccumulation, 0, vertexCount);

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
                // Degenerate UVs (e.g. a polygon almost perpendicular to the decal plane)
                // have no meaningful tangent direction, so skip the accumulation.
                if (det > -1e-8f && det < 1e-8f) continue;

                var r = 1.0f / det;
                var tangentDir = new Vector3(
                    (t2 * x1 - t1 * x2) * r,
                    (t2 * y1 - t1 * y2) * r,
                    (t2 * z1 - t1 * z2) * r);
                var bitangentDir = new Vector3(
                    (s1 * x2 - s2 * x1) * r,
                    (s1 * y2 - s2 * y1) * r,
                    (s1 * z2 - s2 * z1) * r);

                _tangentAccumulation[i0 - vertexStart] += tangentDir;
                _tangentAccumulation[i1 - vertexStart] += tangentDir;
                _tangentAccumulation[i2 - vertexStart] += tangentDir;
                _bitangentAccumulation[i0 - vertexStart] += bitangentDir;
                _bitangentAccumulation[i1 - vertexStart] += bitangentDir;
                _bitangentAccumulation[i2 - vertexStart] += bitangentDir;
            }

            for (var i = 0; i < vertexCount; i++)
            {
                var normal = normals[vertexStart + i];
                var tangent = _tangentAccumulation[i];

                // Gram-Schmidt orthogonalization against the normal.
                tangent -= normal * Vector3.Dot(normal, tangent);
                var magnitude = tangent.magnitude;
                if (magnitude > 1e-8f)
                    tangent /= magnitude;
                else
                    // No valid accumulation (degenerate UVs), so use any direction orthogonal to the normal.
                    tangent = BuildOrthogonalUnitVector(normal);

                var w = Vector3.Dot(Vector3.Cross(normal, tangent), _bitangentAccumulation[i]) < 0.0f
                    ? -1.0f
                    : 1.0f;
                tangents[vertexStart + i] = new Vector4(tangent.x, tangent.y, tangent.z, w);
            }
        }

        private static Vector3 BuildOrthogonalUnitVector(Vector3 normal)
        {
            var axis = Mathf.Abs(normal.x) < 0.9f ? Vector3.right : Vector3.up;
            var orthogonal = axis - normal * Vector3.Dot(normal, axis);
            var magnitude = orthogonal.magnitude;
            return magnitude > 1e-8f ? orthogonal / magnitude : Vector3.right;
        }

        private static void EnsureCapacity(int vertexCount)
        {
            if (_tangentAccumulation.Length >= vertexCount) return;

            var capacity = Mathf.Max(_tangentAccumulation.Length * 2, vertexCount);
            _tangentAccumulation = new Vector3[capacity];
            _bitangentAccumulation = new Vector3[capacity];
        }
    }
}
