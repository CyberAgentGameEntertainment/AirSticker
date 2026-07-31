using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Fused skinning + broad-phase job. Runs once per source triangle in parallel.
    /// </summary>
    /// <remarks>
    ///     For each triangle it applies the skinning (or the plain local-to-world matrix) to obtain the
    ///     three world-space vertex positions, computes the face normal, and then runs the broad-phase
    ///     rejection (face-normal orientation + plane distance + sphere/triangle distance) to decide whether
    ///     the triangle can be clipped by the decal box. The per-vertex world normal is intentionally not
    ///     computed: it is never used by the output mesh (which uses the model-space normal) nor by any later
    ///     stage, so computing it would only waste bandwidth.
    ///
    /// </remarks>
    [BurstCompile]
    internal struct SkinningBroadPhaseJob : IJobParallelFor
    {
        // --- Source geometry (per triangle vertex / per triangle) ---
        [ReadOnly] public NativeArray<float3> SourcePositionsMs;
        [ReadOnly] public NativeArray<BoneWeight> SourceBoneWeights;
        [ReadOnly] public NativeArray<int> TriangleComponentIndices;

        // --- Per component palette ---
        [ReadOnly] public NativeArray<bool> ComponentIsSkinned;
        [ReadOnly] public NativeArray<bool> ComponentExistsRootBone;
        [ReadOnly] public NativeArray<float4x4> ComponentLocalToWorld;
        [ReadOnly] public NativeArray<int> ComponentBoneMatrixOffset;
        [ReadOnly] public NativeArray<float4x4> BoneMatrices;

        // --- Decal box parameters (broad phase) ---
        public float3 CenterPositionOfDecalBox;
        public float3 DecalSpaceNormalWs;
        public float Radius;
        public float SqrRadius;
        public bool ProjectionBackside;

        // --- Outputs ---
        // World positions of every triangle vertex. Each iteration writes a 3-element range, so the parallel
        // restriction is disabled; the ranges are disjoint across iterations.
        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float3> WorldPositions;

        [WriteOnly] public NativeArray<int> SurviveFlags;

        public void Execute(int triangleIndex)
        {
            var vertexBase = triangleIndex * ReceiverConvexPolygonsMesh.VerticesPerTriangle;
            var componentIndex = TriangleComponentIndices[triangleIndex];

            var useSkinning = ComponentIsSkinned[componentIndex] && ComponentExistsRootBone[componentIndex];

            float3 p0, p1, p2;
            if (useSkinning)
            {
                var boneOffset = ComponentBoneMatrixOffset[componentIndex];
                p0 = SkinPosition(vertexBase + 0, boneOffset);
                p1 = SkinPosition(vertexBase + 1, boneOffset);
                p2 = SkinPosition(vertexBase + 2, boneOffset);
            }
            else
            {
                var localToWorld = ComponentLocalToWorld[componentIndex];
                p0 = math.transform(localToWorld, SourcePositionsMs[vertexBase + 0]);
                p1 = math.transform(localToWorld, SourcePositionsMs[vertexBase + 1]);
                p2 = math.transform(localToWorld, SourcePositionsMs[vertexBase + 2]);
            }

            WorldPositions[vertexBase + 0] = p0;
            WorldPositions[vertexBase + 1] = p1;
            WorldPositions[vertexBase + 2] = p2;

            // The face normal is only needed by the broad phase, so it is not stored.
            var faceNormal = DecalGeometryMath.NormalizeSafe(math.cross(p1 - p0, p2 - p0));

            SurviveFlags[triangleIndex] = SurvivesBroadPhase(faceNormal, p0, p1, p2) ? 1 : 0;
        }

        private float3 SkinPosition(int vertexIndex, int boneOffset)
        {
            var boneWeight = SourceBoneWeights[vertexIndex];
            // Weighted blend of the four influencing bone matrices, matching the old
            // Multiply/MultiplyAdd accumulation order so the result stays bit-identical.
            var m = BoneMatrices[boneOffset + boneWeight.boneIndex0] * boneWeight.weight0;
            m += BoneMatrices[boneOffset + boneWeight.boneIndex1] * boneWeight.weight1;
            m += BoneMatrices[boneOffset + boneWeight.boneIndex2] * boneWeight.weight2;
            m += BoneMatrices[boneOffset + boneWeight.boneIndex3] * boneWeight.weight3;
            return math.transform(m, SourcePositionsMs[vertexIndex]);
        }

        private bool SurvivesBroadPhase(float3 faceNormal, float3 v0, float3 v1, float3 v2)
        {
            if (!ProjectionBackside && math.dot(DecalSpaceNormalWs, faceNormal) < 0.0f) return false;

            // If the plane of the polygon doesn't intersect the sphere, the polygon can be rejected cheaply.
            var distToPlane = math.dot(faceNormal, CenterPositionOfDecalBox - v0);
            if (distToPlane > Radius || distToPlane < -Radius) return false;

            if (DecalGeometryMath.CalculateSqrDistancePointToTriangle(CenterPositionOfDecalBox, v0, v1, v2) > SqrRadius)
                return false;

            return true;
        }
    }
}
