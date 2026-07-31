using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Struct-of-arrays view of the clip working buffers for one convex polygon.
    /// </summary>
    /// <remarks>
    ///     Holds only NativeArray handles, so it is cheap to pass by value and is Burst-compatible.
    ///     The per-vertex world normal is intentionally absent: it is dead data in the pipeline.
    /// </remarks>
    internal struct ClipVertexBuffers
    {
        public NativeArray<float3> WorldPositions;
        public NativeArray<float3> ModelPositions;
        public NativeArray<float3> ModelNormals;
        public NativeArray<BoneWeight> BoneWeights;
    }

    /// <summary>
    ///     Splits a convex polygon by a plane in place, discarding the vertices on the negative side.
    /// </summary>
    /// <remarks>
    ///     Faithful port of <c>ConvexPolygon.SplitAndRemoveByPlane</c> onto fixed-stride NativeArray slices.
    ///     Two differences from the original, both behavior-preserving:
    ///     <list type="number">
    ///         <item>The <c>Line</c> buffer is gone. The two edges that straddle the plane are captured from
    ///         the vertex ring before the ring is mutated; every later plane recomputes its edges from the
    ///         ring on the fly. Because the ring is maintained exactly as before, this is identical to the
    ///         old cached lines.</item>
    ///         <item>The world-space per-vertex normal is not carried, because nothing downstream reads it.</item>
    ///     </list>
    /// </remarks>
    internal static class ConvexPolygonClipping
    {
        private struct EdgeEndpoints
        {
            public float3 StartWorldPos;
            public float3 EndWorldPos;
            public float3 StartModelPos;
            public float3 EndModelPos;
            public float3 StartModelNormal;
            public float3 EndModelNormal;
            public BoneWeight StartWeight;
            public BoneWeight EndWeight;
        }

        private struct SplitVertex
        {
            public float3 WorldPos;
            public float3 ModelPos;
            public float3 ModelNormal;
            public BoneWeight Weight;
        }

        public static void ClipByPlane(
            ClipVertexBuffers buffers,
            int baseOffset,
            ref int vertexCount,
            float4 clipPlane,
            out bool allVertexIsOutside)
        {
            allVertexIsOutside = false;

            var numOutsideVertex = 0;
            var removeVertStartNo = -1;
            var removeVertEndNo = 0;
            var remainVertStartNo = -1;
            var remainVertEndNo = 0;
            for (var no = 0; no < vertexCount; no++)
            {
                var t = SignedDistance(clipPlane, buffers.WorldPositions[baseOffset + no]);
                if (t < 0)
                {
                    if (removeVertStartNo == -1) removeVertStartNo = no;
                    removeVertEndNo = no;
                    numOutsideVertex++;
                }
                else
                {
                    if (remainVertStartNo == -1) remainVertStartNo = no;
                    remainVertEndNo = no;
                }
            }

            if (numOutsideVertex == vertexCount)
            {
                allVertexIsOutside = true;
                return;
            }

            if (numOutsideVertex == 0) return;

            // The polygon's vertex count changes by 2 - numOutsideVertex.
            var deltaVerticesSize = 2 - numOutsideVertex;

            if (removeVertStartNo == 0)
            {
                // The 0th vertex is outside. The remaining (inside) vertices are packed to the front and the
                // two intersection vertices are appended.
                var enterEdge = CaptureEdge(buffers, baseOffset, vertexCount, remainVertStartNo - 1);
                var leaveEdge = CaptureEdge(buffers, baseOffset, vertexCount, remainVertEndNo);

                var vertNo = 0;
                for (var i = remainVertStartNo; i < remainVertEndNo + 1; i++)
                {
                    CopyVertex(buffers, baseOffset, vertNo, i);
                    vertNo++;
                }

                // Matches the original's CalculateNewVertexDataBySplitPlane(l1, l0): new0 from the leave edge,
                // new1 from the enter edge.
                CalculateNewVertices(leaveEdge, enterEdge, clipPlane, out var new0, out var new1);

                var newVertNo0Local = vertNo;
                var newVertNo1Local = vertNo + 1;
                WriteVertex(buffers, baseOffset, newVertNo0Local, new0);
                WriteVertex(buffers, baseOffset, newVertNo1Local, new1);

                vertexCount += deltaVerticesSize;
            }
            else
            {
                var enterEdge = CaptureEdge(buffers, baseOffset, vertexCount, removeVertStartNo - 1);
                var leaveEdge = CaptureEdge(buffers, baseOffset, vertexCount, removeVertEndNo);

                if (deltaVerticesSize > 0)
                    // The vertex count increases, so shift the tail up to make room.
                    for (var i = vertexCount - 1; i > removeVertEndNo; i--)
                        CopyVertex(buffers, baseOffset, i + deltaVerticesSize, i);
                else
                    // The vertex count decreases or stays the same, so shift the tail down.
                    for (var i = removeVertEndNo + 1; i < vertexCount; i++)
                        CopyVertex(buffers, baseOffset, i + deltaVerticesSize, i);

                CalculateNewVertices(enterEdge, leaveEdge, clipPlane, out var new0, out var new1);

                var newVertNo0Local = removeVertStartNo;
                var newVertNo1Local = removeVertStartNo + 1;
                WriteVertex(buffers, baseOffset, newVertNo0Local, new0);
                WriteVertex(buffers, baseOffset, newVertNo1Local, new1);

                vertexCount += deltaVerticesSize;
            }
        }

        private static EdgeEndpoints CaptureEdge(ClipVertexBuffers buffers, int baseOffset, int vertexCount,
            int startVertNo)
        {
            var s = baseOffset + startVertNo;
            var e = baseOffset + (startVertNo + 1) % vertexCount;
            return new EdgeEndpoints
            {
                StartWorldPos = buffers.WorldPositions[s],
                EndWorldPos = buffers.WorldPositions[e],
                StartModelPos = buffers.ModelPositions[s],
                EndModelPos = buffers.ModelPositions[e],
                StartModelNormal = buffers.ModelNormals[s],
                EndModelNormal = buffers.ModelNormals[e],
                StartWeight = buffers.BoneWeights[s],
                EndWeight = buffers.BoneWeights[e]
            };
        }

        private static void CalculateNewVertices(EdgeEndpoints e0, EdgeEndpoints e1, float4 clipPlane,
            out SplitVertex new0, out SplitVertex new1)
        {
            // new0 is the intersection on edge e0, interpolated from its End toward its Start.
            var t = SignedDistance(clipPlane, e0.EndWorldPos)
                    / math.dot(clipPlane.xyz, e0.EndWorldPos - e0.StartWorldPos);
            new0 = new SplitVertex
            {
                WorldPos = ClampedLerp(e0.EndWorldPos, e0.StartWorldPos, t),
                ModelPos = ClampedLerp(e0.EndModelPos, e0.StartModelPos, t),
                ModelNormal = DecalGeometryMath.NormalizeSafe(ClampedLerp(e0.EndModelNormal, e0.StartModelNormal, t))
            };

            // new1 is the intersection on edge e1, interpolated from its Start toward its End.
            t = SignedDistance(clipPlane, e1.StartWorldPos)
                / math.dot(clipPlane.xyz, e1.StartWorldPos - e1.EndWorldPos);
            new1 = new SplitVertex
            {
                WorldPos = ClampedLerp(e1.StartWorldPos, e1.EndWorldPos, t),
                ModelPos = ClampedLerp(e1.StartModelPos, e1.EndModelPos, t),
                ModelNormal = DecalGeometryMath.NormalizeSafe(ClampedLerp(e1.StartModelNormal, e1.EndModelNormal, t))
            };

            // NOTE: the original reuses `t` (= e1's t) for BOTH bone-weight interpolations, not e0's t for
            // new0. That is a quirk of the original code; it is replicated here to keep the output identical.
            new0.Weight = InterpolateWeightFromStart(e0.StartWeight, e0.EndWeight, t);
            new1.Weight = InterpolateWeightFromEnd(e1.StartWeight, e1.EndWeight, t);
        }

        private static BoneWeight InterpolateWeightFromStart(BoneWeight start, BoneWeight end, float t)
        {
            var result = start;
            result.weight0 = start.boneIndex0 == end.boneIndex0 ? ClampedLerp(end.weight0, start.weight0, t) : start.weight0;
            result.weight1 = start.boneIndex1 == end.boneIndex1 ? ClampedLerp(end.weight1, start.weight1, t) : start.weight1;
            result.weight2 = start.boneIndex2 == end.boneIndex2 ? ClampedLerp(end.weight2, start.weight2, t) : start.weight2;
            result.weight3 = start.boneIndex3 == end.boneIndex3 ? ClampedLerp(end.weight3, start.weight3, t) : start.weight3;
            result.boneIndex0 = start.boneIndex0;
            result.boneIndex1 = start.boneIndex1;
            result.boneIndex2 = start.boneIndex2;
            result.boneIndex3 = start.boneIndex3;
            return NormalizeWeight(result);
        }

        private static BoneWeight InterpolateWeightFromEnd(BoneWeight start, BoneWeight end, float t)
        {
            var result = end;
            result.weight0 = start.boneIndex0 == end.boneIndex0 ? ClampedLerp(start.weight0, end.weight0, t) : end.weight0;
            result.weight1 = start.boneIndex1 == end.boneIndex1 ? ClampedLerp(start.weight1, end.weight1, t) : end.weight1;
            result.weight2 = start.boneIndex2 == end.boneIndex2 ? ClampedLerp(start.weight2, end.weight2, t) : end.weight2;
            result.weight3 = start.boneIndex3 == end.boneIndex3 ? ClampedLerp(start.weight3, end.weight3, t) : end.weight3;
            result.boneIndex0 = end.boneIndex0;
            result.boneIndex1 = end.boneIndex1;
            result.boneIndex2 = end.boneIndex2;
            result.boneIndex3 = end.boneIndex3;
            return NormalizeWeight(result);
        }

        private static BoneWeight NormalizeWeight(BoneWeight w)
        {
            var total = w.weight0 + w.weight1 + w.weight2 + w.weight3;
            if (total > 0.0f)
            {
                w.weight0 /= total;
                w.weight1 /= total;
                w.weight2 /= total;
                w.weight3 /= total;
            }

            return w;
        }

        private static void CopyVertex(ClipVertexBuffers buffers, int baseOffset, int dstLocal, int srcLocal)
        {
            var d = baseOffset + dstLocal;
            var s = baseOffset + srcLocal;
            buffers.WorldPositions[d] = buffers.WorldPositions[s];
            buffers.ModelPositions[d] = buffers.ModelPositions[s];
            buffers.ModelNormals[d] = buffers.ModelNormals[s];
            buffers.BoneWeights[d] = buffers.BoneWeights[s];
        }

        private static void WriteVertex(ClipVertexBuffers buffers, int baseOffset, int localVertNo, SplitVertex v)
        {
            var i = baseOffset + localVertNo;
            buffers.WorldPositions[i] = v.WorldPos;
            buffers.ModelPositions[i] = v.ModelPos;
            buffers.ModelNormals[i] = v.ModelNormal;
            buffers.BoneWeights[i] = v.Weight;
        }

        // Vector4.Dot(plane, (pos, 1)) — the signed distance of pos from the plane (scaled by |plane.xyz|).
        private static float SignedDistance(float4 plane, float3 pos)
        {
            return math.dot(plane.xyz, pos) + plane.w;
        }

        // Matches UnityEngine.Vector3.Lerp / Mathf.Lerp, which clamp t to [0, 1].
        private static float3 ClampedLerp(float3 a, float3 b, float t)
        {
            return math.lerp(a, b, math.saturate(t));
        }

        private static float ClampedLerp(float a, float b, float t)
        {
            return math.lerp(a, b, math.saturate(t));
        }
    }
}
