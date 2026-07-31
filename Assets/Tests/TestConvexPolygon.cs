using AirSticker.Runtime.Scripts.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class TestConvexPolygon
    {
        // A Test behaves as an ordinary method
        [Test]
        public void TestIntersectRayToTriangle()
        {
            var receiverObject = new GameObject("Receiver");
            try
            {
                var receiverComponent = receiverObject.AddComponent<MeshRenderer>();

                var verticesInModelSpace = new Vector3[3];
                verticesInModelSpace[0] = new Vector3(-0.5f, -0.5f, 0.0f);
                verticesInModelSpace[1] = new Vector3(0.0f, 0.5f, 0.0f);
                verticesInModelSpace[2] = new Vector3(0.5f, -0.5f, 0.0f);
                var normalsInModelSpace = new Vector3[3];
                normalsInModelSpace[0] = new Vector3(0.0f, 0.0f, -1.0f);
                normalsInModelSpace[1] = new Vector3(0.0f, 0.0f, -1.0f);
                normalsInModelSpace[2] = new Vector3(0.0f, 0.0f, -1.0f);

                var convexPolygon = new ConvexPolygon(
                    new Vector3[3],
                    new Vector3[3],
                    new BoneWeight[3],
                    new Line[3],
                    verticesInModelSpace,
                    normalsInModelSpace,
                    receiverComponent,
                    0,
                    3,
                    0,
                    3);
                // Calculate the face normal and the edges of the polygon.
                // The receiver object has the identity transform, so the world space
                // positions are the same as the model space positions.
                convexPolygon.PrepareToRunOnWorkerThread();
                convexPolygon.CalculatePositionsAndNormalsInWorldSpace(
                    null, new Matrix4x4[3], new BoneWeight[3]);

                var rayStart = new Vector3();
                rayStart.x = 0.0f;
                rayStart.y = 0.0f;
                rayStart.z = 2.0f;

                var rayEnd = new Vector3();
                rayEnd.x = 0.0f;
                rayEnd.y = 0.0f;
                rayEnd.z = -2.0f;

                Vector3 hitPoint;
                // Hit test.
                var isIntersect = convexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStart, rayEnd);
                Assert.IsTrue(isIntersect);
                Assert.AreEqual(0.0f, hitPoint.x, 1e-5f);
                Assert.AreEqual(0.0f, hitPoint.y, 1e-5f);
                Assert.AreEqual(0.0f, hitPoint.z, 1e-5f);

                rayStart.x = 1.0f;
                rayStart.y = 0.0f;
                rayStart.z = 2.0f;

                rayEnd.x = 1.0f;
                rayEnd.y = 0.0f;
                rayEnd.z = -2.0f;
                // Miss test.
                isIntersect = convexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStart, rayEnd);
                Assert.IsFalse(isIntersect);
            }
            finally
            {
                Object.DestroyImmediate(receiverObject);
            }
        }
    }
}
