using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AirSticker.Runtime.Scripts.Core.Jobs
{
    /// <summary>
    ///     Builds the appended decal geometry for every decal mesh from the clipped convex polygons.
    /// </summary>
    /// <remarks>
    ///     A single serial job (not parallel) so the triangle-fan output offsets can be accumulated naturally,
    ///     the same way the old <c>DecalMesh.AddTrianglePolygonsToDecalMesh</c> did on the worker thread. It
    ///     runs off the main thread to keep the fan expansion, UV projection and tangent calculation off the
    ///     main thread (this is the property Step 2 established and must be preserved).
    ///
    ///     Output layout: the arrays hold only this launch's appended geometry for all decal meshes,
    ///     concatenated, with decal mesh <c>dm</c> occupying [VertexOffsets[dm], VertexOffsets[dm]+count).
    ///     Indices are written in this output space (i.e. they index Out* directly), so the tangent
    ///     calculation can run on the output arrays. When the main thread merges the output into a decal
    ///     mesh's persistent buffer it shifts the indices by (existing vertex count - VertexOffsets[dm]).
    ///
    /// </remarks>
    [BurstCompile]
    internal struct DecalMeshBuildJob : IJob
    {
        public int TriangleCount;
        public int DecalMeshCount;

        // Source / clip data.
        [ReadOnly] public NativeArray<int> TriangleComponentIndices;
        [ReadOnly] public NativeArray<float3> ClipWorldPositions;
        [ReadOnly] public NativeArray<float3> ClipModelPositions;
        [ReadOnly] public NativeArray<float3> ClipModelNormals;
        [ReadOnly] public NativeArray<BoneWeight> ClipBoneWeights;
        [ReadOnly] public NativeArray<int> ClipVertexCounts;

        // Per decal mesh (length == DecalMeshCount).
        [ReadOnly] public NativeArray<int> DecalMeshComponentIndices;
        [ReadOnly] public NativeArray<int> DecalMeshVertexOffsets;
        [ReadOnly] public NativeArray<int> DecalMeshIndexOffsets;

        // Decal space projection parameters.
        public float3 DecalSpaceOriginWs;
        public float3 DecalSpaceTangentWs;
        public float3 DecalSpaceBiNormalWs;
        public float DecalWidth;
        public float DecalHeight;
        public float ZOffsetInDecalSpace;

        // Appended geometry outputs. Not WriteOnly because the tangent stage reads positions/normals/uvs/indices.
        public NativeArray<float3> OutPositions;
        public NativeArray<float3> OutNormals;
        public NativeArray<float2> OutUvs;
        public NativeArray<float4> OutTangents;
        public NativeArray<BoneWeight> OutBoneWeights;
        public NativeArray<int> OutIndices;

        // Tangent accumulation scratch, length >= the largest decal mesh's appended vertex count.
        public NativeArray<float3> TangentAccumulation;
        public NativeArray<float3> BitangentAccumulation;

        public void Execute()
        {
            for (var dm = 0; dm < DecalMeshCount; dm++)
            {
                var componentIndex = DecalMeshComponentIndices[dm];
                var appendedVertexStart = DecalMeshVertexOffsets[dm];
                var appendedIndexStart = DecalMeshIndexOffsets[dm];

                var vertWrite = appendedVertexStart;
                var indexWrite = appendedIndexStart;
                // Index base in the output space: indices reference Out* directly (see the class remarks).
                var indexBase = appendedVertexStart;

                for (var tri = 0; tri < TriangleCount; tri++)
                {
                    var vertexCount = ClipVertexCounts[tri];
                    if (vertexCount < 3) continue;
                    if (TriangleComponentIndices[tri] != componentIndex) continue;

                    var slotBase = tri * DecalMeshJobBuffers.MaxVertexCountPerConvexPolygon;
                    for (var k = 0; k < vertexCount; k++)
                    {
                        var src = slotBase + k;

                        var toVertWs = ClipWorldPositions[src] - DecalSpaceOriginWs;
                        OutUvs[vertWrite] = new float2(
                            math.dot(DecalSpaceTangentWs, toVertWs) / DecalWidth + 0.5f,
                            math.dot(DecalSpaceBiNormalWs, toVertWs) / DecalHeight + 0.5f);

                        var normal = ClipModelNormals[src];
                        // Push the vertex slightly against the projection direction to avoid Z-fighting.
                        OutPositions[vertWrite] = ClipModelPositions[src] + normal * ZOffsetInDecalSpace;
                        OutNormals[vertWrite] = normal;
                        OutBoneWeights[vertWrite] = ClipBoneWeights[src];
                        vertWrite++;
                    }

                    var numTriangle = vertexCount - 2;
                    for (var t = 0; t < numTriangle; t++)
                    {
                        OutIndices[indexWrite++] = indexBase;
                        OutIndices[indexWrite++] = indexBase + t + 1;
                        OutIndices[indexWrite++] = indexBase + t + 2;
                    }

                    indexBase += vertexCount;
                }

                var appendedVertexCount = vertWrite - appendedVertexStart;
                var appendedIndexCount = indexWrite - appendedIndexStart;
                DecalMeshTangents.ComputeTangents(
                    OutPositions, OutNormals, OutUvs, OutIndices, OutTangents,
                    TangentAccumulation, BitangentAccumulation,
                    appendedVertexStart, appendedVertexCount, appendedIndexStart, appendedIndexCount);
            }
        }
    }
}
