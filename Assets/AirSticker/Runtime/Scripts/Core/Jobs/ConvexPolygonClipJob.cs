using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Clips every surviving triangle's convex polygon by the six decal box planes, in parallel.
    /// </summary>
    /// <remarks>
    ///     Runs once per source triangle. Non-survivors (rejected by the broad phase) return immediately with
    ///     a vertex count of 0. A survivor is seeded into its fixed-stride clip slot from the skinned world
    ///     positions and the model-space source data, then split by the six planes in the same order as the
    ///     old pipeline (Left, Right, Bottom, Top, Front, Back). A polygon that is fully clipped away also
    ///     ends with a vertex count of 0.
    ///
    ///     Each iteration only touches its own stride slot in the clip buffers, so the parallel-for
    ///     restriction is disabled on them; the slots are disjoint across iterations.
    ///
    ///     [BurstCompile] is added in the final sub-step, after the no-Burst parallelization is measured.
    /// </remarks>
    // [BurstCompile]
    internal struct ConvexPolygonClipJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> SurviveFlags;
        [ReadOnly] public NativeArray<float3> WorldPositions;
        [ReadOnly] public NativeArray<float3> SourcePositionsMs;
        [ReadOnly] public NativeArray<float3> SourceNormalsMs;
        [ReadOnly] public NativeArray<BoneWeight> SourceBoneWeights;

        // The six clip planes (Left, Right, Bottom, Top, Front, Back).
        [ReadOnly] public NativeArray<float4> ClipPlanes;

        [NativeDisableParallelForRestriction] public NativeArray<float3> ClipWorldPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float3> ClipModelPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float3> ClipModelNormals;
        [NativeDisableParallelForRestriction] public NativeArray<BoneWeight> ClipBoneWeights;

        [WriteOnly] public NativeArray<int> ClipVertexCounts;

        public void Execute(int triangleIndex)
        {
            if (SurviveFlags[triangleIndex] == 0)
            {
                ClipVertexCounts[triangleIndex] = 0;
                return;
            }

            var baseOffset = triangleIndex * DecalMeshJobBuffers.MaxVertexCountPerConvexPolygon;
            var vertexBase = triangleIndex * ReceiverConvexPolygonsMesh.VerticesPerTriangle;

            // Seed the clip slot with the triangle's three vertices.
            for (var k = 0; k < ReceiverConvexPolygonsMesh.VerticesPerTriangle; k++)
            {
                ClipWorldPositions[baseOffset + k] = WorldPositions[vertexBase + k];
                ClipModelPositions[baseOffset + k] = SourcePositionsMs[vertexBase + k];
                ClipModelNormals[baseOffset + k] = SourceNormalsMs[vertexBase + k];
                ClipBoneWeights[baseOffset + k] = SourceBoneWeights[vertexBase + k];
            }

            var buffers = new ClipVertexBuffers
            {
                WorldPositions = ClipWorldPositions,
                ModelPositions = ClipModelPositions,
                ModelNormals = ClipModelNormals,
                BoneWeights = ClipBoneWeights
            };

            var vertexCount = ReceiverConvexPolygonsMesh.VerticesPerTriangle;
            for (var planeNo = 0; planeNo < ClipPlanes.Length; planeNo++)
            {
                ConvexPolygonClipping.ClipByPlane(
                    buffers, baseOffset, ref vertexCount, ClipPlanes[planeNo], out var allVertexIsOutside);
                if (allVertexIsOutside)
                {
                    vertexCount = 0;
                    break;
                }
            }

            ClipVertexCounts[triangleIndex] = vertexCount;
        }
    }
}
