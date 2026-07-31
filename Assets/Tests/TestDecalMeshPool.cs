using AirSticker.Runtime.Scripts.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class TestDecalMeshPool
    {
        [Test]
        public void TestCalculateHashWithGroupId()
        {
            var receiverObject = new GameObject("ReceiverObject");
            var renderer = receiverObject.AddComponent<MeshRenderer>();
            var decalMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            decalMaterial.name = "DecalMaterial";

            try
            {
                // The hash value of the unspecified group is the same as the hash value of the group 0.
                var hashDefault = DecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial);
                var hashGroup0 = DecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial, 0);
                Assert.AreEqual(hashDefault, hashGroup0);

                // The hash value of the same group is the same.
                var hashGroup1 = DecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial, 1);
                Assert.AreEqual(hashGroup1, DecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial, 1));

                // The hash values of different groups are different,
                // even if the receiver object, the renderer and the decal material are the same.
                Assert.AreNotEqual(hashGroup0, hashGroup1);
            }
            finally
            {
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(receiverObject);
            }
        }

        [Test]
        public void TestCalculateHashWithSameNameObjects()
        {
            // Different objects that have the same name (e.g. clones of the same prefab)
            // must not share a decal mesh.
            var receiverObjectA = new GameObject("ReceiverObject");
            var receiverObjectB = new GameObject("ReceiverObject");
            var rendererA = receiverObjectA.AddComponent<MeshRenderer>();
            var rendererB = receiverObjectB.AddComponent<MeshRenderer>();
            var decalMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            decalMaterial.name = "DecalMaterial";

            try
            {
                var hashA = DecalMeshPool.CalculateHash(receiverObjectA, rendererA, decalMaterial);
                var hashB = DecalMeshPool.CalculateHash(receiverObjectB, rendererB, decalMaterial);
                Assert.AreNotEqual(hashA, hashB);
            }
            finally
            {
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(receiverObjectA);
                Object.DestroyImmediate(receiverObjectB);
            }
        }
    }
}
