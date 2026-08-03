using AirSticker.Runtime.Scripts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests
{
    public class TestCreateAndLaunch
    {
        private static GameObject[] GetSerializedReceiverObjects(AirStickerProjector projector)
        {
            var serializedObject = new SerializedObject(projector);
            var property = serializedObject.FindProperty("receiverObjects");
            var receiverObjects = new GameObject[property.arraySize];
            for (var i = 0; i < property.arraySize; i++)
                receiverObjects[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            return receiverObjects;
        }

        [Test]
        public void TestSingleReceiverOverload()
        {
            var owner = new GameObject("Owner");
            var receiverObject = new GameObject("ReceiverObject");
            var decalMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

            try
            {
                var projector = AirStickerProjector.CreateAndLaunch(
                    owner,
                    receiverObject,
                    decalMaterial,
                    1.0f,
                    1.0f,
                    1.0f,
                    false,
                    null);

                Assert.AreEqual(AirStickerProjector.State.NotLaunch, projector.NowState);
                var receiverObjects = GetSerializedReceiverObjects(projector);
                Assert.AreEqual(1, receiverObjects.Length);
                Assert.AreEqual(receiverObject, receiverObjects[0]);
            }
            finally
            {
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(receiverObject);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TestMultipleReceiversOverload()
        {
            var owner = new GameObject("Owner");
            var receiverObjectA = new GameObject("ReceiverObjectA");
            var receiverObjectB = new GameObject("ReceiverObjectB");
            var decalMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

            try
            {
                var projector = AirStickerProjector.CreateAndLaunch(
                    owner,
                    new[] { receiverObjectA, receiverObjectB },
                    decalMaterial,
                    1.0f,
                    1.0f,
                    1.0f,
                    false,
                    null);

                Assert.AreEqual(AirStickerProjector.State.NotLaunch, projector.NowState);
                var receiverObjects = GetSerializedReceiverObjects(projector);
                Assert.AreEqual(2, receiverObjects.Length);
                Assert.AreEqual(receiverObjectA, receiverObjects[0]);
                Assert.AreEqual(receiverObjectB, receiverObjects[1]);
            }
            finally
            {
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(receiverObjectB);
                Object.DestroyImmediate(receiverObjectA);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TestReceiverObjectsArrayIsCopied()
        {
            var owner = new GameObject("Owner");
            var receiverObjectA = new GameObject("ReceiverObjectA");
            var receiverObjectB = new GameObject("ReceiverObjectB");
            var decalMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

            try
            {
                var callerArray = new[] { receiverObjectA };
                var projector = AirStickerProjector.CreateAndLaunch(
                    owner,
                    callerArray,
                    decalMaterial,
                    1.0f,
                    1.0f,
                    1.0f,
                    false,
                    null);

                // Mutating the caller's array must not change the projection targets,
                // because the launch runs over multiple frames.
                callerArray[0] = receiverObjectB;

                var receiverObjects = GetSerializedReceiverObjects(projector);
                Assert.AreEqual(1, receiverObjects.Length);
                Assert.AreEqual(receiverObjectA, receiverObjects[0]);
            }
            finally
            {
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(receiverObjectB);
                Object.DestroyImmediate(receiverObjectA);
                Object.DestroyImmediate(owner);
            }
        }
    }
}
