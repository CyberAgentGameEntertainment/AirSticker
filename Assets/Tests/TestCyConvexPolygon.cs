using CyDecal.Runtime.Scripts.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class TestCyConvexPolygon
    {
        // A Test behaves as an ordinary method
        [Test]
        public void TesetIntesectRayToTriangle()
        {
            var vertices = new Vector3[3];
            vertices[0] = new Vector3(-0.5f, -0.5f, 0.0f);
            vertices[1] = new Vector3(0.0f, 0.5f, 0.0f);
            vertices[2] = new Vector3(0.5f, -0.5f, 0.0f);
            var normals = new Vector3[3];
            normals[0] = new Vector3(0.0f, 0.0f, -1.0f);
            normals[1] = new Vector3(0.0f, 0.0f, -1.0f);
            normals[2] = new Vector3(0.0f, 0.0f, -1.0f);
            var boneWeights = new BoneWeight[3];
            boneWeights[0] = new BoneWeight();
            boneWeights[1] = new BoneWeight();
            boneWeights[2] = new BoneWeight();
            var lines = new CyLine[3];

            var rayStart = new Vector3();
            rayStart.x = 0.0f;
            rayStart.y = 0.0f;
            rayStart.z = 2.0f;

            var rayEnd = new Vector3();
            rayEnd.x = 0.0f;
            rayEnd.y = 0.0f;
            rayEnd.z = -2.0f;

            var convexPolygon = new CyConvexPolygon(
                vertices,
                normals,
                boneWeights,
                lines,
                null,
                0,
                3,
                3);
            Vector3 hitPoint;
            // 当たるかテスト
            var isIntersect = convexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStart, rayEnd);
            Assert.AreEqual(isIntersect, true);

            rayStart.x = 1.0f;
            rayStart.y = 0.0f;
            rayStart.z = 2.0f;

            rayEnd.x = 1.0f;
            rayEnd.y = 0.0f;
            rayEnd.z = -2.0f;
            // 外れるかテスト。
            isIntersect = convexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStart, rayEnd);
            Assert.AreEqual(isIntersect, false);
        }
    }
}
