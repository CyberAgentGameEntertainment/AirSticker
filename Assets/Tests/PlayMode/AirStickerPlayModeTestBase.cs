using System.Collections;
using System.Collections.Generic;
using AirSticker.Runtime.Scripts;
using AirSticker.Runtime.Scripts.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>
    ///     Shared SetUp/TearDown and procedural fixtures for the AirSticker PlayMode regression harness
    ///     (launch-cancellation safety and GC.Alloc regression). AirStickerSystem.Update() is a plain
    ///     MonoBehaviour.Update with no [ExecuteAlways], so it only runs in PlayMode -- these tests cannot
    ///     be EditMode tests if they are to drive the real async launch pipeline.
    /// </summary>
    public abstract class AirStickerPlayModeTestBase
    {
        protected const int MaxWaitFrames = 300;

        private readonly List<Object> _spawned = new List<Object>();
        private int _defaultMaxGeneratedPolygonPerFrame;

        protected GameObject SystemGameObject { get; private set; }

        [SetUp]
        public void BaseSetUp()
        {
            _defaultMaxGeneratedPolygonPerFrame = TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame;
            SystemGameObject = Track(new GameObject("AirStickerSystem", typeof(AirStickerSystem)));
        }

        [TearDown]
        public void BaseTearDown()
        {
            for (var i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i])
                    Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();

            TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = _defaultMaxGeneratedPolygonPerFrame;
            AirStickerPerformanceLog.Enabled = false;

            // Force any leaked Allocator.Persistent/TempJob NativeArray's safety-handle finalizer to run
            // now, so a leak from this test is reported here instead of during an unrelated later test.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            LogAssert.NoUnexpectedReceived();
        }

        protected T Track<T>(T obj) where T : Object
        {
            _spawned.Add(obj);
            return obj;
        }

        protected static Material CreateDecalMaterial()
        {
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        /// <summary>
        ///     A small procedural grid receiver (MeshFilter + MeshRenderer), gridSize*gridSize*2 triangles.
        /// </summary>
        protected GameObject CreateStaticReceiver(int gridSize = 6)
        {
            var go = new GameObject("StaticReceiver");
            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();
            // Tracked here (not just the GameObject the caller tracks): DestroyImmediate on a GameObject
            // does not destroy the Mesh/Material assets its components merely reference.
            meshFilter.sharedMesh = Track(BuildGridMesh(gridSize));
            meshRenderer.sharedMaterial = Track(CreateDecalMaterial());
            return go;
        }

        /// <summary>
        ///     A small procedural skinned receiver: a chain of segments+1 bones driving a strip mesh, so
        ///     the skinning job path (DecalMeshJobPipeline's SkinningBroadPhaseJob) is exercised too.
        /// </summary>
        protected GameObject CreateSkinnedReceiver(int segments = 4)
        {
            var root = new GameObject("SkinnedReceiverRoot");
            var boneCount = segments + 1;
            var bones = new Transform[boneCount];
            for (var i = 0; i < boneCount; i++)
            {
                var bone = new GameObject($"Bone{i}").transform;
                bone.SetParent(root.transform);
                bone.localPosition = new Vector3(i, 0f, 0f);
                bones[i] = bone;
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var boneWeights = new List<BoneWeight>();
            var triangles = new List<int>();
            const float halfWidth = 0.5f;
            for (var i = 0; i <= segments; i++)
            {
                vertices.Add(new Vector3(i, 0f, -halfWidth));
                vertices.Add(new Vector3(i, 0f, halfWidth));
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                var weight = new BoneWeight { boneIndex0 = i, weight0 = 1f };
                boneWeights.Add(weight);
                boneWeights.Add(weight);
            }

            for (var i = 0; i < segments; i++)
            {
                var i00 = i * 2;
                var i01 = i00 + 1;
                var i10 = i00 + 2;
                var i11 = i00 + 3;
                triangles.Add(i00);
                triangles.Add(i01);
                triangles.Add(i10);
                triangles.Add(i10);
                triangles.Add(i01);
                triangles.Add(i11);
            }

            var bindPoses = new Matrix4x4[boneCount];
            for (var i = 0; i < boneCount; i++)
                bindPoses[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.boneWeights = boneWeights.ToArray();
            mesh.bindposes = bindPoses;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var smr = root.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = Track(mesh);
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterial = Track(CreateDecalMaterial());
            return root;
        }

        private static Mesh BuildGridMesh(int gridSize)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            for (var y = 0; y <= gridSize; y++)
            for (var x = 0; x <= gridSize; x++)
            {
                vertices.Add(new Vector3(x, 0f, y));
                normals.Add(Vector3.up);
            }

            var triangles = new List<int>();
            for (var y = 0; y < gridSize; y++)
            for (var x = 0; x < gridSize; x++)
            {
                var i00 = y * (gridSize + 1) + x;
                var i10 = i00 + 1;
                var i01 = i00 + gridSize + 1;
                var i11 = i01 + 1;
                triangles.Add(i00);
                triangles.Add(i01);
                triangles.Add(i10);
                triangles.Add(i10);
                triangles.Add(i01);
                triangles.Add(i11);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        ///     Launches a fresh decal on the given receiver and waits for it to reach LaunchingCompleted.
        ///     Used after a cancellation scenario to confirm DecalProjectorLauncher's FIFO queue was not
        ///     left stuck behind the canceled request.
        /// </summary>
        protected IEnumerator AssertLauncherRecovers(GameObject receiver, Material material)
        {
            var owner = Track(new GameObject("RecoveryOwner"));
            var projector = AirStickerProjector.CreateAndLaunch(
                owner, receiver, material, 1f, 1f, 1f, true, null);

            yield return WaitUntilFinished(projector);

            Assert.AreEqual(AirStickerProjector.State.LaunchingCompleted, projector.NowState,
                "Launcher queue appears stuck: a request queued after the cancellation never completed.");
        }

        /// <summary>
        ///     Waits for the projector to leave the Launching state. Failing to leave it within maxFrames
        ///     is exactly the failure mode (a stuck state machine) this harness exists to catch.
        /// </summary>
        protected static IEnumerator WaitUntilFinished(AirStickerProjector projector, int maxFrames = MaxWaitFrames)
        {
            var frames = 0;
            while (projector.NowState == AirStickerProjector.State.Launching && frames < maxFrames)
            {
                yield return null;
                frames++;
            }

            Assert.AreNotEqual(AirStickerProjector.State.Launching, projector.NowState,
                "Projector never left the Launching state within the frame budget.");
        }

        /// <summary>
        ///     Marks the test Inconclusive (not a failure) if the scheduled job already completed by this
        ///     frame boundary, instead of letting the test either silently stop exercising the intended
        ///     "destroy while the job is running" race or flakily fail when a small test mesh's job happens
        ///     to finish before the coroutine resumes -- both real risks on faster machines/hardware.
        /// </summary>
        protected static void AssertWorkerThreadRunningOrInconclusive(AirStickerProjector projector)
        {
            if (!projector.IsWorkerThreadRunning)
                Assert.Inconclusive(
                    "The scheduled job already completed before this frame boundary, so this run did not " +
                    "exercise the intended \"destroy while the job is running\" race.");
        }
    }
}
