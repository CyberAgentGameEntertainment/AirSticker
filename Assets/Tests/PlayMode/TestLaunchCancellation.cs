using System.Collections;
using AirSticker.Runtime.Scripts;
using AirSticker.Runtime.Scripts.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>
    ///     Reproduces the destruction races that AirStickerProjector.ExecuteLaunchAsync,
    ///     TrianglePolygonsFactory and DecalProjectorLauncher's comments call out as hazards specific to
    ///     the coroutine-to-Awaitable migration: an async launch body is not stopped by the projector's
    ///     destruction, so every case here checks that the state machine still reaches a terminal state,
    ///     that DecalProjectorLauncher's FIFO queue is not left stuck, and (via the base class's forced-GC
    ///     TearDown) that no NativeArray is leaked.
    /// </summary>
    public class TestLaunchCancellation : AirStickerPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator DestroyRightAfterEnqueue_QueueStillProcessesNextRequest()
        {
            var receiver = Track(CreateStaticReceiver());
            var material = Track(CreateDecalMaterial());

            var deadOwner = Track(new GameObject("DeadOwner"));
            var deadProjector = AirStickerProjector.CreateAndLaunch(
                deadOwner, receiver, material, 1f, 1f, 1f, true, null);
            Assert.AreEqual(AirStickerProjector.State.Launching, deadProjector.NowState);

            // DecalProjectorLauncher has not dequeued this request yet -- that happens on the next
            // AirStickerSystem.Update(). Destroying it now hits DecalProjectorLauncher.ProcessNextRequest's
            // "this request was dead, so skipped" branch.
            Object.Destroy(deadOwner);

            yield return AssertLauncherRecovers(receiver, material);
        }

        [UnityTest]
        public IEnumerator DestroyProjectorDuringTriangleExtraction_CancelsCleanly()
        {
            TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = 1;
            var receiver = Track(CreateStaticReceiver(4));
            var material = Track(CreateDecalMaterial());
            var owner = Track(new GameObject("Owner"));

            var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);

            // With MaxGeneratedPolygonPerFrame=1 the frame-sliced extraction yields after every triangle,
            // so a few frames in it is still definitely mid-flight.
            for (var i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(AirStickerProjector.State.Launching, projector.NowState,
                "Extraction should still be mid-flight for this test to exercise the intended race.");

            Object.Destroy(owner);

            yield return WaitUntilFinished(projector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCanceled, projector.NowState);
            Assert.IsFalse(AirStickerSystem.ReceiverObjectTrianglePolygonsPool.Contains(receiver),
                "A partially-extracted receiver must not be published into the triangle-polygon pool.");

            yield return AssertLauncherRecovers(receiver, material);
        }

        [UnityTest]
        public IEnumerator DestroyReceiverDuringTriangleExtraction_CancelsCleanly()
        {
            TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = 1;
            var receiver = Track(CreateStaticReceiver(4));
            var material = Track(CreateDecalMaterial());
            var owner = Track(new GameObject("Owner"));

            var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);

            for (var i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(AirStickerProjector.State.Launching, projector.NowState,
                "Extraction should still be mid-flight for this test to exercise the intended race.");

            // Unlike the projector-destroyed case, the projector's own destroyCancellationToken never
            // fires here -- this exercises TrianglePolygonsFactory's separate "a source was destroyed
            // during it" completeness check instead.
            Object.Destroy(receiver);

            yield return WaitUntilFinished(projector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCanceled, projector.NowState);

            var freshReceiver = Track(CreateStaticReceiver());
            yield return AssertLauncherRecovers(freshReceiver, material);
        }

        [UnityTest]
        public IEnumerator DestroyProjectorWhileJobRunning_CompletesJobAndUnpinsSource()
        {
            var receiver = Track(CreateStaticReceiver(6));
            var material = Track(CreateDecalMaterial());

            // Warm-up: complete one launch fully first so the triangle-polygon pool is already populated.
            // The second launch below then schedules its clip-stage job synchronously within the very
            // frame it is dequeued, so destroying its owner right after that frame reliably lands while
            // the job is still scheduled (JobHandle.IsCompleted cannot realistically flip true within the
            // handful of CPU instructions between scheduling it and checking it).
            var warmupOwner = Track(new GameObject("WarmupOwner"));
            var warmupProjector =
                AirStickerProjector.CreateAndLaunch(warmupOwner, receiver, material, 1f, 1f, 1f, true, null);
            yield return WaitUntilFinished(warmupProjector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCompleted, warmupProjector.NowState);

            var owner = Track(new GameObject("Owner"));
            var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);

            yield return null;
            AssertWorkerThreadRunningOrInconclusive(projector);

            Object.Destroy(owner);

            yield return WaitUntilFinished(projector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCanceled, projector.NowState);
            Assert.IsFalse(projector.IsWorkerThreadRunning);
            Assert.IsFalse(projector.IsExecutingLaunch);

            yield return AssertLauncherRecovers(receiver, material);
        }

        [UnityTest]
        public IEnumerator DestroyReceiverWhileJobRunning_CancelsCleanly()
        {
            var receiver = Track(CreateStaticReceiver(6));
            var material = Track(CreateDecalMaterial());

            var warmupOwner = Track(new GameObject("WarmupOwner"));
            var warmupProjector =
                AirStickerProjector.CreateAndLaunch(warmupOwner, receiver, material, 1f, 1f, 1f, true, null);
            yield return WaitUntilFinished(warmupProjector);

            var owner = Track(new GameObject("Owner"));
            var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);

            yield return null;
            AssertWorkerThreadRunningOrInconclusive(projector);

            // The receiver's ReceiverConvexPolygonsMesh is pinned (InUse) while the job reads it, so this
            // must not crash or leak even though the pool's GarbageCollect would otherwise have disposed
            // it as soon as it notices the receiver is dead.
            Object.Destroy(receiver);

            yield return WaitUntilFinished(projector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCanceled, projector.NowState);

            var freshReceiver = Track(CreateStaticReceiver());
            yield return AssertLauncherRecovers(freshReceiver, material);
        }

        [UnityTest]
        public IEnumerator DestroyProjectorAndSystemTogetherWhileJobRunning_DisposesWithoutError()
        {
            var receiver = Track(CreateStaticReceiver(6));
            var material = Track(CreateDecalMaterial());

            var warmupOwner = Track(new GameObject("WarmupOwner"));
            var warmupProjector =
                AirStickerProjector.CreateAndLaunch(warmupOwner, receiver, material, 1f, 1f, 1f, true, null);
            yield return WaitUntilFinished(warmupProjector);

            var owner = Track(new GameObject("Owner"));
            var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);

            yield return null;
            AssertWorkerThreadRunningOrInconclusive(projector);

            // Simulates a scene unload, where the AirStickerSystem singleton and an in-flight projector are
            // torn down together in an order Unity does not guarantee -- see DecalMeshJobPipeline.Dispose's
            // remark that AirStickerProjector.OnDestroy is not guaranteed to run before AirStickerSystem's.
            Object.Destroy(owner);
            Object.Destroy(SystemGameObject);

            for (var i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(AirStickerProjector.State.LaunchingCanceled, projector.NowState);
        }

        // Mirrors DestroyProjectorDuringTriangleExtraction_CancelsCleanly / DestroyProjectorWhileJobRunning
        // above but with a skinned receiver, so TrianglePolygonsFactory.FillFromSkinnedMeshRenderersAsync's
        // resume/cancel checks and DecalMeshJobPipeline's SkinnedMeshRenderer/bone-matrix path are covered
        // too, not just the unskinned MeshRenderer path.

        [UnityTest]
        public IEnumerator DestroyProjectorDuringSkinnedTriangleExtraction_CancelsCleanly()
        {
            TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame = 1;
            var receiver = Track(CreateSkinnedReceiver(4));
            var material = Track(CreateDecalMaterial());
            var owner = Track(new GameObject("Owner"));

            var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);

            for (var i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(AirStickerProjector.State.Launching, projector.NowState,
                "Extraction should still be mid-flight for this test to exercise the intended race.");

            Object.Destroy(owner);

            yield return WaitUntilFinished(projector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCanceled, projector.NowState);
            Assert.IsFalse(AirStickerSystem.ReceiverObjectTrianglePolygonsPool.Contains(receiver),
                "A partially-extracted receiver must not be published into the triangle-polygon pool.");

            yield return AssertLauncherRecovers(receiver, material);
        }

        [UnityTest]
        public IEnumerator DestroySkinnedProjectorWhileJobRunning_CompletesJobAndUnpinsSource()
        {
            var receiver = Track(CreateSkinnedReceiver(6));
            var material = Track(CreateDecalMaterial());

            var warmupOwner = Track(new GameObject("WarmupOwner"));
            var warmupProjector =
                AirStickerProjector.CreateAndLaunch(warmupOwner, receiver, material, 1f, 1f, 1f, true, null);
            yield return WaitUntilFinished(warmupProjector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCompleted, warmupProjector.NowState);

            var owner = Track(new GameObject("Owner"));
            var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);

            yield return null;
            AssertWorkerThreadRunningOrInconclusive(projector);

            Object.Destroy(owner);

            yield return WaitUntilFinished(projector);
            Assert.AreEqual(AirStickerProjector.State.LaunchingCanceled, projector.NowState);
            Assert.IsFalse(projector.IsWorkerThreadRunning);
            Assert.IsFalse(projector.IsExecutingLaunch);

            yield return AssertLauncherRecovers(receiver, material);
        }
    }
}
