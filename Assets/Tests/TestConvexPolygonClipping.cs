using AirSticker.Runtime.Scripts.Core.Jobs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests
{
    /// <summary>
    ///     Standalone regression tests for the job-pipeline geometry (DecalGeometryMath /
    ///     ConvexPolygonClipping). Equivalence to the old ConvexPolygon / BroadPhase code was validated by
    ///     differential tests while both coexisted; after the old code was removed these assert the properties
    ///     and known values the new code must keep.
    /// </summary>
    public class TestConvexPolygonClipping
    {
        private const int MaxVertex = DecalMeshJobBuffers.MaxVertexCountPerConvexPolygon;

        [Test]
        public void SqrDistancePointToTriangleKnownValues()
        {
            var a = new float3(0.0f, 0.0f, 0.0f);
            var b = new float3(1.0f, 0.0f, 0.0f);
            var c = new float3(0.0f, 1.0f, 0.0f);

            AssertSqrDistance(2.0f, new float3(-1.0f, -1.0f, 0.0f), a, b, c); // vertex region of a
            AssertSqrDistance(1.0f, new float3(2.0f, 0.0f, 0.0f), a, b, c); // vertex region of b
            AssertSqrDistance(1.0f, new float3(0.0f, 2.0f, 0.0f), a, b, c); // vertex region of c
            AssertSqrDistance(1.0f, new float3(0.5f, -1.0f, 0.0f), a, b, c); // edge region of ab
            AssertSqrDistance(1.0f, new float3(-1.0f, 0.5f, 0.0f), a, b, c); // edge region of ac
            AssertSqrDistance(0.5f, new float3(1.0f, 1.0f, 0.0f), a, b, c); // edge region of bc
            AssertSqrDistance(1.0f, new float3(0.25f, 0.25f, 1.0f), a, b, c); // face region
            AssertSqrDistance(0.0f, new float3(0.25f, 0.25f, 0.0f), a, b, c); // on the triangle
        }

        [Test]
        public void ClipKeepsGeometryInsideDecalBox()
        {
            // Triangle spanning [-1, 1] on the z = 0 plane, and a smaller decal box that cuts its edges.
            var triangle = new[]
            {
                new float3(-1.0f, -1.0f, 0.0f),
                new float3(1.0f, -1.0f, 0.0f),
                new float3(0.0f, 1.0f, 0.0f)
            };
            var planes = BuildDecalBoxPlanes(new float3(0.0f, 0.0f, 0.0f), 1.0f, 1.0f, 2.0f);

            var count = Clip(triangle, planes, out var vertices);

            Assert.GreaterOrEqual(count, 3, "the triangle overlaps the box, so it should not be removed");
            for (var i = 0; i < count; i++)
                for (var p = 0; p < planes.Length; p++)
                {
                    var signedDistance = math.dot(planes[p].xyz, vertices[i]) + planes[p].w;
                    Assert.GreaterOrEqual(signedDistance, -1e-4f,
                        $"vertex {i} is outside plane {p} (signed distance {signedDistance})");
                }
        }

        [Test]
        public void ClipRemovesFullyOutsideTriangle()
        {
            var triangle = new[]
            {
                new float3(100.0f, 100.0f, 0.0f),
                new float3(101.0f, 100.0f, 0.0f),
                new float3(100.0f, 101.0f, 0.0f)
            };
            var planes = BuildDecalBoxPlanes(new float3(0.0f, 0.0f, 0.0f), 1.0f, 1.0f, 2.0f);

            var count = Clip(triangle, planes, out _);

            Assert.AreEqual(0, count, "a triangle fully outside the box should be removed");
        }

        [Test]
        public void ClipKeepsFullyInsideTriangleUnchanged()
        {
            var triangle = new[]
            {
                new float3(-0.2f, -0.2f, 0.0f),
                new float3(0.2f, -0.2f, 0.0f),
                new float3(0.0f, 0.2f, 0.0f)
            };
            var planes = BuildDecalBoxPlanes(new float3(0.0f, 0.0f, 0.0f), 2.0f, 2.0f, 2.0f);

            var count = Clip(triangle, planes, out var vertices);

            Assert.AreEqual(3, count, "a triangle fully inside the box should be unchanged");
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(triangle[i].x, vertices[i].x, 1e-4f, $"vertex {i}.x");
                Assert.AreEqual(triangle[i].y, vertices[i].y, 1e-4f, $"vertex {i}.y");
                Assert.AreEqual(triangle[i].z, vertices[i].z, 1e-4f, $"vertex {i}.z");
            }
        }

        // Clip the world-space triangle by the planes and return the resulting vertex count and positions.
        // World == model space here, which is enough to exercise the clip math.
        private static int Clip(float3[] triangle, float4[] planes, out float3[] worldVertices)
        {
            var clipWorld = new NativeArray<float3>(MaxVertex, Allocator.Temp);
            var clipModel = new NativeArray<float3>(MaxVertex, Allocator.Temp);
            var clipNormal = new NativeArray<float3>(MaxVertex, Allocator.Temp);
            var clipBoneWeight = new NativeArray<BoneWeight>(MaxVertex, Allocator.Temp);
            try
            {
                for (var i = 0; i < 3; i++)
                {
                    clipWorld[i] = triangle[i];
                    clipModel[i] = triangle[i];
                    clipNormal[i] = new float3(0.0f, 0.0f, -1.0f);
                    clipBoneWeight[i] = default;
                }

                var buffers = new ClipVertexBuffers
                {
                    WorldPositions = clipWorld,
                    ModelPositions = clipModel,
                    ModelNormals = clipNormal,
                    BoneWeights = clipBoneWeight
                };

                var vertexCount = 3;
                for (var p = 0; p < planes.Length; p++)
                {
                    ConvexPolygonClipping.ClipByPlane(buffers, 0, ref vertexCount, planes[p], out var allOutside);
                    if (allOutside)
                    {
                        vertexCount = 0;
                        break;
                    }
                }

                worldVertices = new float3[vertexCount];
                for (var i = 0; i < vertexCount; i++) worldVertices[i] = clipWorld[i];
                return vertexCount;
            }
            finally
            {
                clipWorld.Dispose();
                clipModel.Dispose();
                clipNormal.Dispose();
                clipBoneWeight.Dispose();
            }
        }

        // Six decal-box planes (Left, Right, Bottom, Top, Front, Back), built like DecalMeshJobPipeline.
        private static float4[] BuildDecalBoxPlanes(float3 basePoint, float width, float height, float depth)
        {
            var ex = new float3(1.0f, 0.0f, 0.0f);
            var ey = new float3(0.0f, 1.0f, 0.0f);
            var ez = new float3(0.0f, 0.0f, 1.0f);
            var halfDepth = depth * 0.5f;
            return new[]
            {
                new float4(ex, width / 2.0f - math.dot(ex, basePoint)),
                new float4(-ex, width / 2.0f + math.dot(ex, basePoint)),
                new float4(ey, height / 2.0f - math.dot(ey, basePoint)),
                new float4(-ey, height / 2.0f + math.dot(ey, basePoint)),
                new float4(-ez, halfDepth + math.dot(ez, basePoint)),
                new float4(ez, halfDepth - math.dot(ez, basePoint))
            };
        }

        private static void AssertSqrDistance(float expected, float3 p, float3 a, float3 b, float3 c)
        {
            Assert.AreEqual(expected, DecalGeometryMath.CalculateSqrDistancePointToTriangle(p, a, b, c), 1e-5f);
        }
    }
}
