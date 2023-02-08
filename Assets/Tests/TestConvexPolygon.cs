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
            var vertices = new Vector3[3];
            vertices[0] = new Vector3(-0.5f, -0.5f, 0.0f);
            vertices[1] = new Vector3(0.0f, 0.5f, 0.0f);
            vertices[2] = new Vector3(0.5f, -0.5f, 0.0f);
            var normals = new Vector3[3];
            normals[0] = new Vector3(0.0f, 0.0f, -1.0f);
            normals[1] = new Vector3(0.0f, 0.0f, -1.0f);
            normals[2] = new Vector3(0.0f, 0.0f, -1.0f);
            
            var verticesInModelSpace = new Vector3[3];
            verticesInModelSpace[0] = new Vector3(-0.5f, -0.5f, 0.0f);
            verticesInModelSpace[1] = new Vector3(0.0f, 0.5f, 0.0f);
            verticesInModelSpace[2] = new Vector3(0.5f, -0.5f, 0.0f);
            var normalsInModelSpace = new Vector3[3];
            normalsInModelSpace[0] = new Vector3(0.0f, 0.0f, -1.0f);
            normalsInModelSpace[1] = new Vector3(0.0f, 0.0f, -1.0f);
            normalsInModelSpace[2] = new Vector3(0.0f, 0.0f, -1.0f);
            
            var boneWeights = new BoneWeight[3];
            boneWeights[0] = new BoneWeight();
            boneWeights[1] = new BoneWeight();
            boneWeights[2] = new BoneWeight();
            var lines = new Line[3];

            var rayStart = new Vector3();
            rayStart.x = 0.0f;
            rayStart.y = 0.0f;
            rayStart.z = 2.0f;

            var rayEnd = new Vector3();
            rayEnd.x = 0.0f;
            rayEnd.y = 0.0f;
            rayEnd.z = -2.0f;

            var convexPolygon = new ConvexPolygon(
                vertices,
                normals,
                boneWeights,
                lines,
                verticesInModelSpace,
                normalsInModelSpace,
                null,
                0,
                3,
                0,
                3);
            Vector3 hitPoint;
            // Hit test.
            var isIntersect = convexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStart, rayEnd);
            Assert.AreEqual(isIntersect, true);

            rayStart.x = 1.0f;
            rayStart.y = 0.0f;
            rayStart.z = 2.0f;

            rayEnd.x = 1.0f;
            rayEnd.y = 0.0f;
            rayEnd.z = -2.0f;
            // Miss test.
            isIntersect = convexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStart, rayEnd);
            Assert.AreEqual(isIntersect, false);
        }
    }
}
