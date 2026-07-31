using System.Collections.Generic;
using AirSticker.Runtime.Scripts.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class TestBroadPhaseConvexPolygonsDetection
    {
        /// <summary>
        ///     Create the convex polygon info of the triangle from vertex positions in model space.
        /// </summary>
        private static ConvexPolygonInfo CreateTrianglePolygonInfo(
            Component receiverComponent, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            var polygon = new ConvexPolygon(
                new Vector3[3],
                new Vector3[3],
                new BoneWeight[3],
                new Line[3],
                new[] { v0, v1, v2 },
                new Vector3[3],
                receiverComponent,
                0,
                3,
                0,
                3);
            polygon.PrepareToRunOnWorkerThread();
            polygon.CalculatePositionsAndNormalsInWorldSpace(null, new Matrix4x4[3], new BoneWeight[3]);
            return new ConvexPolygonInfo { ConvexPolygon = polygon };
        }

        [Test]
        public void TestLargePolygonIsNotCulled()
        {
            var receiverObject = new GameObject("Receiver");
            try
            {
                var receiverComponent = receiverObject.AddComponent<MeshRenderer>();
                // The large triangle polygon on the plane of z = 0.
                // Its face normal is (0, 0, -1).
                var polygonInfos = new List<ConvexPolygonInfo>
                {
                    CreateTrianglePolygonInfo(
                        receiverComponent,
                        new Vector3(-10.0f, -10.0f, 0.0f),
                        new Vector3(0.0f, 10.0f, 0.0f),
                        new Vector3(10.0f, -10.0f, 0.0f))
                };

                // The small decal box on the center of the triangle.
                // All vertices of the triangle are outside the sphere that encompasses the decal box,
                // but the triangle intersects the sphere.
                var result = BroadPhaseConvexPolygonsDetection.Execute(
                    new Vector3(0.0f, 0.0f, -0.25f),
                    new Vector3(0.0f, 0.0f, -1.0f),
                    0.3f,
                    0.3f,
                    1.0f,
                    polygonInfos,
                    false);

                Assert.AreEqual(1, result.Count);
            }
            finally
            {
                Object.DestroyImmediate(receiverObject);
            }
        }

        [Test]
        public void TestPolygonOnFarPlaneIsCulled()
        {
            var receiverObject = new GameObject("Receiver");
            try
            {
                var receiverComponent = receiverObject.AddComponent<MeshRenderer>();
                // The large triangle polygon on the plane of z = 5.
                // The plane of the polygon is far from the decal box,
                // so it should be culled by the pre-rejection of the plane distance.
                var polygonInfos = new List<ConvexPolygonInfo>
                {
                    CreateTrianglePolygonInfo(
                        receiverComponent,
                        new Vector3(-10.0f, -10.0f, 5.0f),
                        new Vector3(0.0f, 10.0f, 5.0f),
                        new Vector3(10.0f, -10.0f, 5.0f))
                };

                var result = BroadPhaseConvexPolygonsDetection.Execute(
                    new Vector3(0.0f, 0.0f, -0.25f),
                    new Vector3(0.0f, 0.0f, -1.0f),
                    0.3f,
                    0.3f,
                    1.0f,
                    polygonInfos,
                    false);

                Assert.AreEqual(0, result.Count);
            }
            finally
            {
                Object.DestroyImmediate(receiverObject);
            }
        }

        [Test]
        public void TestCoplanarFarPolygonIsCulled()
        {
            var receiverObject = new GameObject("Receiver");
            try
            {
                var receiverComponent = receiverObject.AddComponent<MeshRenderer>();
                // The triangle polygon on the same plane as the decal box, but far to the side.
                // The plane distance can't cull it, so it should be culled by
                // the distance from the sphere center to the triangle.
                var polygonInfos = new List<ConvexPolygonInfo>
                {
                    CreateTrianglePolygonInfo(
                        receiverComponent,
                        new Vector3(40.0f, -10.0f, 0.0f),
                        new Vector3(50.0f, 10.0f, 0.0f),
                        new Vector3(60.0f, -10.0f, 0.0f))
                };

                var result = BroadPhaseConvexPolygonsDetection.Execute(
                    new Vector3(0.0f, 0.0f, -0.25f),
                    new Vector3(0.0f, 0.0f, -1.0f),
                    0.3f,
                    0.3f,
                    1.0f,
                    polygonInfos,
                    false);

                Assert.AreEqual(0, result.Count);
            }
            finally
            {
                Object.DestroyImmediate(receiverObject);
            }
        }

        [Test]
        public void TestBacksidePolygonCulling()
        {
            var receiverObject = new GameObject("Receiver");
            try
            {
                var receiverComponent = receiverObject.AddComponent<MeshRenderer>();
                // The reversed winding triangle polygon.
                // Its face normal is (0, 0, 1), that is opposite the decal box.
                var polygonInfos = new List<ConvexPolygonInfo>
                {
                    CreateTrianglePolygonInfo(
                        receiverComponent,
                        new Vector3(-10.0f, -10.0f, 0.0f),
                        new Vector3(10.0f, -10.0f, 0.0f),
                        new Vector3(0.0f, 10.0f, 0.0f))
                };

                // The polygon is culled when the projection to the backside is not allowed.
                var result = BroadPhaseConvexPolygonsDetection.Execute(
                    new Vector3(0.0f, 0.0f, -0.25f),
                    new Vector3(0.0f, 0.0f, -1.0f),
                    0.3f,
                    0.3f,
                    1.0f,
                    polygonInfos,
                    false);
                Assert.AreEqual(0, result.Count);

                // The polygon is not culled when the projection to the backside is allowed.
                result = BroadPhaseConvexPolygonsDetection.Execute(
                    new Vector3(0.0f, 0.0f, -0.25f),
                    new Vector3(0.0f, 0.0f, -1.0f),
                    0.3f,
                    0.3f,
                    1.0f,
                    polygonInfos,
                    true);
                Assert.AreEqual(1, result.Count);
            }
            finally
            {
                Object.DestroyImmediate(receiverObject);
            }
        }

        [Test]
        public void TestCalculateSqrDistancePointToTriangle()
        {
            var a = new Vector3(0.0f, 0.0f, 0.0f);
            var b = new Vector3(1.0f, 0.0f, 0.0f);
            var c = new Vector3(0.0f, 1.0f, 0.0f);

            // The vertex region of a.
            Assert.AreEqual(2.0f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(-1.0f, -1.0f, 0.0f), a, b, c), 1e-5f);
            // The vertex region of b.
            Assert.AreEqual(1.0f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(2.0f, 0.0f, 0.0f), a, b, c), 1e-5f);
            // The vertex region of c.
            Assert.AreEqual(1.0f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(0.0f, 2.0f, 0.0f), a, b, c), 1e-5f);
            // The edge region of ab.
            Assert.AreEqual(1.0f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(0.5f, -1.0f, 0.0f), a, b, c), 1e-5f);
            // The edge region of ac.
            Assert.AreEqual(1.0f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(-1.0f, 0.5f, 0.0f), a, b, c), 1e-5f);
            // The edge region of bc. The closest point is (0.5, 0.5, 0).
            Assert.AreEqual(0.5f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(1.0f, 1.0f, 0.0f), a, b, c), 1e-5f);
            // The face region.
            Assert.AreEqual(1.0f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(0.25f, 0.25f, 1.0f), a, b, c), 1e-5f);
            // The point on the triangle.
            Assert.AreEqual(0.0f,
                BroadPhaseConvexPolygonsDetection.CalculateSqrDistancePointToTriangle(
                    new Vector3(0.25f, 0.25f, 0.0f), a, b, c), 1e-5f);
        }
    }
}
