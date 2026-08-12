using System.Collections;
using System.Collections.Generic;
using AirSticker.Runtime.Scripts;
using AirSticker.Runtime.Scripts.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Tests.PlayMode
{
    /// <summary>
    ///     Locks in the zero-alloc idle-polling property PR #39 fixed (DecalProjectorLauncher.Update() and
    ///     both pools' GarbageCollect() must not allocate when there is nothing to do), and watches
    ///     steady-state per-launch allocation for unbounded growth. A per-launch start-frame check was
    ///     deliberately not added: a brand-new AirStickerProjector's first real suspension pays a one-time,
    ///     per-instance cost (Component.destroyCancellationToken's lazy init, the async state machine's
    ///     first box) that is not the kind of per-frame bookkeeping PR #39/#40 targeted and was never
    ///     actually zero even before the Awaitable migration (coroutines paid an equivalent cost via their
    ///     IEnumerator/Coroutine objects) -- RepeatedSteadyStateLaunchesDoNotIncreasePerLaunchAllocation
    ///     below is where that real per-launch cost is watched instead.
    /// </summary>
    public class TestGCAllocRegression : AirStickerPlayModeTestBase
    {
        // Calibrated once against a real editor PlayMode Test Runner run, then set to that measurement
        // times a safety margin (~2x). Placeholder until that calibration run happens; see the harness
        // plan's "GC.Alloc absolute threshold calibration" step. Update it if a deliberate, reviewed
        // change legitimately raises steady-state per-launch allocation.
        private const long PerLaunchByteCeiling = 200_000;

        [Test]
        public void IdleLauncherUpdateDoesNotAllocate()
        {
            var launcher = (IDecalProjectorLauncher)AirStickerSystem.DecalProjectorLauncher;
            launcher.Update(); // warm-up: never compare a first call (JIT etc.), per JobSystemMigrationPlan.md

            Assert.That(() => launcher.Update(), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void IdlePoolGarbageCollectDoesNotAllocate()
        {
            var decalMeshPool = (IDecalMeshPool)AirStickerSystem.DecalMeshPool;
            var trianglePool = (IReceiverObjectTrianglePolygonsPool)AirStickerSystem.ReceiverObjectTrianglePolygonsPool;
            decalMeshPool.GarbageCollect();
            trianglePool.GarbageCollect();

            Assert.That(() => decalMeshPool.GarbageCollect(), Is.Not.AllocatingGCMemory());
            Assert.That(() => trianglePool.GarbageCollect(), Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator RepeatedSteadyStateLaunchesDoNotIncreasePerLaunchAllocation()
        {
            const int warmupLaunches = 2; // JIT/pool warm-up; never compare launch #0/#1 (JobSystemMigrationPlan.md).
            const int sampledLaunches = 8;

            var receiver = Track(CreateStaticReceiver());
            var material = Track(CreateDecalMaterial());

            for (var i = 0; i < warmupLaunches; i++)
            {
                AirStickerProjector.RemoveDecalMeshes(0, receiver, material);
                var owner = Track(new GameObject($"WarmupOwner{i}"));
                var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);
                yield return WaitUntilFinished(projector);
            }

            var samples = new List<long>(sampledLaunches);
            for (var i = 0; i < sampledLaunches; i++)
            {
                // Reset like Demo_Benchmark does, so every sampled launch performs identical work.
                AirStickerProjector.RemoveDecalMeshes(0, receiver, material);
                var owner = Track(new GameObject($"SampleOwner{i}"));

                var before = System.GC.GetAllocatedBytesForCurrentThread();
                var projector = AirStickerProjector.CreateAndLaunch(owner, receiver, material, 1f, 1f, 1f, true, null);
                yield return WaitUntilFinished(projector);
                var after = System.GC.GetAllocatedBytesForCurrentThread();

                samples.Add(after - before);
            }

            TestContext.WriteLine($"Per-launch bytes allocated: {string.Join(", ", samples)}");

            var firstHalfAverage = Average(samples, 0, sampledLaunches / 2);
            var secondHalfAverage = Average(samples, sampledLaunches / 2, sampledLaunches - sampledLaunches / 2);

            // A generous margin: this guards against unbounded growth (a leak, or a collection that keeps
            // growing instead of being cleared between launches), not against launch-to-launch noise.
            Assert.LessOrEqual(secondHalfAverage, firstHalfAverage * 1.5,
                "Per-launch GC.Alloc is trending upward across repeated identical launches " +
                $"(first half avg {firstHalfAverage:F0} B, second half avg {secondHalfAverage:F0} B) -- " +
                "looks like a leak or a new source of unbounded allocation.");

            Assert.LessOrEqual(secondHalfAverage, PerLaunchByteCeiling,
                $"Steady-state per-launch GC.Alloc ({secondHalfAverage:F0} B) exceeds the calibrated " +
                $"ceiling ({PerLaunchByteCeiling} B). If this is an intentional trade-off, remeasure in " +
                "the editor and update PerLaunchByteCeiling.");
        }

        private static double Average(List<long> samples, int start, int count)
        {
            long sum = 0;
            for (var i = start; i < start + count; i++) sum += samples[i];
            return (double)sum / count;
        }
    }
}
