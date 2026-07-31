using System.Collections;
using AirSticker.Runtime.Scripts;
using AirSticker.Runtime.Scripts.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Demo.Benchmark
{
    /// <summary>
    ///     Deterministic Air Sticker benchmark for a clean Burst on/off A/B measurement.
    /// </summary>
    /// <remarks>
    ///     Unlike Demo03's aging test (random position / material / receiver every launch), this projects the
    ///     SAME decal box onto the SAME receiver at a FIXED (frozen) pose, and resets the decal meshes before
    ///     every launch, so each launch performs identical clip/build work. That makes the launches comparable
    ///     across a Burst-ON and a Burst-OFF run.
    ///
    ///     How to use for the A/B:
    ///     - Open this scene and enter play mode (it self-sets-up: spawns the AirStickerSystem, a camera, a
    ///       light, and the receiver prefab, then runs automatically).
    ///     - Read the [AirSticker][Perf] "clip stage" / "build stage" logs. Use launch #2+ as the steady state
    ///       (launch #0/#1 include triangle extraction / warm-up).
    ///     - Toggle Jobs &gt; Burst &gt; Enable Compilation (editor) or make a Burst-ON vs Burst-OFF build
    ///       (device), and compare the same launch numbers. The workload is identical, so the difference is
    ///       purely Burst.
    /// </remarks>
    public class AirStickerBenchmark : MonoBehaviour
    {
        [Header("Receiver")]
        [Tooltip("Receiver prefab (a skinned mesh). Instantiated at runtime. If null, an existing scene receiver is used.")]
        [SerializeField] private GameObject receiverPrefab;

        [Tooltip("Freeze the receiver at its bind pose (disable Animators) so every launch skins identically.")]
        [SerializeField] private bool freezePose = true;

        [Header("Decal box")]
        [Tooltip("Box width/height as a fraction of the receiver bounds (bigger = more surviving polygons = heavier clip stage).")]
        [SerializeField] private float widthHeightCoverage = 0.8f;

        [SerializeField] private float depthMargin = 0.3f;

        [Tooltip("World-space projection direction (the decal box points this way).")]
        [SerializeField] private Vector3 aimDirection = Vector3.forward;

        [Header("Run")]
        [SerializeField] private int launchCount = 6;
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private KeyCode startKey = KeyCode.Space;

        private const int BenchmarkGroupId = 987654;
        private Material _material;
        private GameObject _receiver;
        private bool _running;

        private IEnumerator Start()
        {
            AirStickerPerformanceLog.Enabled = true;
            LogBurstStatus();

            EnsureSystem();
            EnsureCameraAndLight();

            _receiver = ResolveReceiver();
            if (_receiver == null)
            {
                Debug.LogError("[AirSticker][Bench] No receiver found. Assign a receiver prefab in the inspector.");
                yield break;
            }

            if (freezePose)
                foreach (var animator in _receiver.GetComponentsInChildren<Animator>())
                    animator.enabled = false;

            _material = CreateDecalMaterial();

            // Let the skinning settle to the (frozen) pose before measuring.
            yield return null;
            yield return null;

            if (runOnStart) yield return RunBenchmark();
        }

        private void Update()
        {
            if (!_running && Input.GetKeyDown(startKey)) StartCoroutine(RunBenchmark());
        }

        private IEnumerator RunBenchmark()
        {
            _running = true;

            var bounds = ComputeBounds(_receiver);
            var dir = aimDirection.sqrMagnitude > 1e-6f ? aimDirection.normalized : Vector3.forward;
            var widthHeight = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.y) * widthHeightCoverage);
            // Span the receiver along the aim direction so the box reaches the surface.
            var depth = Vector3.Scale(bounds.size,
                new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z))).magnitude + depthMargin;
            var projectorPos = bounds.center - dir * (depth * 0.5f);
            var projectorRot = Quaternion.LookRotation(dir);

            Debug.Log($"[AirSticker][Bench] start: {launchCount} launches, " +
                      $"box=({widthHeight:F2} x {widthHeight:F2} x {depth:F2}), reset each launch (identical work). " +
                      "Use launch #2+ as the steady state.");

            for (var i = 0; i < launchCount; i++)
            {
                // Reset so every launch performs identical clip/build work.
                AirStickerProjector.RemoveDecalMeshes(BenchmarkGroupId);

                var owner = new GameObject("Bench_Projector");
                owner.transform.SetPositionAndRotation(projectorPos, projectorRot);

                var finished = false;
                AirStickerProjector.CreateAndLaunch(
                    owner, _receiver, _material, widthHeight, widthHeight, depth, true,
                    _ => finished = true, 0.005f, BenchmarkGroupId);

                while (!finished) yield return null;
                Debug.Log($"[AirSticker][Bench] launch #{i} done");
                Destroy(owner);
                yield return null;
            }

            AirStickerProjector.RemoveDecalMeshes(BenchmarkGroupId);
            Debug.Log("[AirSticker][Bench] finished. Compare launch #2+ between Burst ON and Burst OFF.");
            _running = false;
        }

        private GameObject ResolveReceiver()
        {
            if (receiverPrefab != null)
            {
                var instance = Instantiate(receiverPrefab);
                instance.name = receiverPrefab.name;
                return instance;
            }

            foreach (var skinnedMeshRenderer in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
                if (skinnedMeshRenderer.name != "AirStickerRenderer")
                    return skinnedMeshRenderer.transform.root.gameObject;
            foreach (var meshFilter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                return meshFilter.transform.root.gameObject;
            return null;
        }

        private static void EnsureSystem()
        {
            if (FindObjectsByType<AirStickerSystem>(FindObjectsSortMode.None).Length == 0)
                new GameObject("AirStickerSystem").AddComponent<AirStickerSystem>();
        }

        private static void EnsureCameraAndLight()
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Benchmark Camera") { tag = "MainCamera" };
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0f, 1f, -3f), Quaternion.Euler(10f, 0f, 0f));
            }

            if (FindObjectsByType<Light>(FindObjectsSortMode.None).Length == 0)
            {
                var lightObject = new GameObject("Benchmark Light");
                var directionalLight = lightObject.AddComponent<Light>();
                directionalLight.type = LightType.Directional;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private static Bounds ComputeBounds(GameObject receiver)
        {
            var renderers = receiver.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(receiver.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material CreateDecalMaterial()
        {
            // The material only affects rendering, not the geometry-generation timing being measured.
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }

        /// <summary>
        ///     Definitively reports whether Burst is actually compiling the jobs in this build, so a device
        ///     A/B can be trusted. If this logs NO for a "Burst ON" build, Burst is silently falling back to
        ///     managed (check Project Settings &gt; Burst AOT Settings, not just the editor Jobs menu).
        /// </summary>
        private static void LogBurstStatus()
        {
            using var probe = new NativeArray<int>(1, Allocator.TempJob);
            new BurstProbeJob { Result = probe }.Schedule().Complete();
            var bursted = probe[0] == 0;
            Debug.Log($"[AirSticker][Bench] Burst active for jobs: {(bursted ? "YES" : "NO (managed fallback)")}");
        }

        // The [BurstDiscard] method is stripped when Burst compiles Execute, so Result stays 0 under Burst
        // and becomes 1 when the job runs as plain managed code.
        [BurstCompile]
        private struct BurstProbeJob : IJob
        {
            public NativeArray<int> Result;

            public void Execute()
            {
                Result[0] = 0;
                MarkManaged(Result);
            }

            [BurstDiscard]
            private static void MarkManaged(NativeArray<int> result)
            {
                result[0] = 1;
            }
        }
    }
}
