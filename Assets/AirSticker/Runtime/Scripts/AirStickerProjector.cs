using System;
using System.Collections.Generic;
using System.Threading;
using AirSticker.Runtime.Scripts.Core;
using AirSticker.Runtime.Scripts.Core.Jobs;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Events;

namespace AirSticker.Runtime.Scripts
{
    /// <summary>
    ///     Mesh decal projector. <br />
    ///     This decal projector has the following characteristics. <br />
    ///         1. Fast, spike-free projection of mesh decals. <br />
    ///         2. Draw mesh decals with fewer draw calls. <br />
    ///         3. It works in both URP and BRP. <br />
    ///         4. All user-defined materials can be used. <br />
    ///         5. Mesh decals can be skin animated. <br />
    /// </summary>
    public sealed class AirStickerProjector : MonoBehaviour
    {
        public enum State
        {
            NotLaunch,
            Launching,
            LaunchingCompleted,
            LaunchingCanceled
        }

        [SerializeField] private float width; // Width of the decal box.
        [SerializeField] private float height; // Height of the decal box.
        [SerializeField] private float depth; // Depth of the decal box.
        [SerializeField] private GameObject[] receiverObjects; // The receiver object that will be pasted decal.
        [SerializeField] private float zOffsetInDecalSpace = 0.005f ; // The Z offset of the decal space from the receiver surface.
        [SerializeField]
        private Material decalMaterial; // The decal material that will be pasted to the receiver object.

        [Tooltip("Decal meshes are shared within the same group. Split groups to remove decals separately.")]
        [SerializeField]
        private int groupId; // The group ID that is used as the unit of the decal mesh sharing and removing.

        [SerializeField]
        private bool projectionBackside; // This flag indicates whether it is possible to project onto the backside.

        [Tooltip("When this is checked, the decal projection process is started at the instance is created.")]
        [SerializeField]
        private bool
            launchOnAwake; // When an instance is created, the decal projection process also starts automatically.。

        [SerializeField]
        private UnityEvent<State> onFinishedLaunch; //　The event is called when decal projection is finished.

        private DecalSpace _decalSpace;

        // The handle of the currently scheduled job stage. The main thread polls it (or completes it
        // synchronously when measuring). See IsWorkerThreadRunning for why the launcher needs it.
        private JobHandle _currentJobHandle;
        private bool _jobsScheduled;
        // The pooled source mesh the current launch's jobs are reading, pinned so the pool does not dispose
        // its NativeArrays while the jobs run.
        private ReceiverConvexPolygonsMesh _currentSource;
        // Receives a source mesh built by the frame-sliced extraction, before it is registered into the pool.
        // An async method cannot have an out parameter, so the result is handed over through this reused array
        // rather than a per-launch one. The extraction owns the mesh until it returns and only publishes a
        // complete one (see TrianglePolygonsFactory.BuildFromReceiverObjectAsync), so whatever is found here
        // afterwards belongs to this launch: it is either registered into the pool or disposed below.
        // Only one extraction is in flight at a time.
        private readonly ReceiverConvexPolygonsMesh[] _pendingSourceHolder = new ReceiverConvexPolygonsMesh[1];

        // True from the moment the launcher starts the launch body until the body has returned. The body is an
        // async method, so unlike the coroutine it used to be it keeps running after the projector is
        // destroyed, and it is not finished just because NowState says so. DecalProjectorLauncher waits for
        // this before starting the next launch, because the extraction and the job pipeline are shared between
        // launches (see IsExecutingLaunch).
        private bool _executingLaunch;

        // The working lists of ExecuteLaunch, kept as fields so the launch does not allocate a list (or an
        // array from GetComponentsInChildren) per receiver object. The renderer/terrain lists are held across
        // the frame-sliced extraction, so they belong to the projector instance rather than being shared by
        // AirStickerSystem: a list shared between projectors would be refilled by the next launch while this
        // one is still reading it.
        private readonly List<DecalMesh> _receiverDecalMeshes = new List<DecalMesh>();
        private readonly List<MeshRenderer> _meshRenderers = new List<MeshRenderer>();
        private readonly List<SkinnedMeshRenderer> _skinnedMeshRenderers = new List<SkinnedMeshRenderer>();
        private readonly List<Terrain> _terrains = new List<Terrain>();

        /// <summary>
        ///     True while this projector's jobs are scheduled and not yet completed.
        /// </summary>
        /// <remarks>
        ///     Destroying the projector does not stop the scheduled jobs, which write to the pipeline's pooled
        ///     buffers. Starting the next launch in that state would corrupt those buffers, so
        ///     DecalProjectorLauncher must wait until the jobs finish. The C# instance is still readable after
        ///     the Unity object is destroyed. OnDestroy also completes the jobs to release the pool.
        /// </remarks>
        internal bool IsWorkerThreadRunning => _jobsScheduled && !_currentJobHandle.IsCompleted;

        /// <summary>
        ///     True while the launch body has not returned yet, even if the projector was destroyed.
        /// </summary>
        /// <remarks>
        ///     The launch body observes the destruction and unwinds at its next resume point, which is a frame
        ///     later at the earliest. Until then it must not overlap the next launch: the triangle extraction
        ///     shares its working buffers and write cursor across launches, as does the job pipeline. The body
        ///     always clears this, including on cancellation and on an exception, so the launcher cannot stall.
        /// </remarks>
        internal bool IsExecutingLaunch => _executingLaunch;

        /// <summary>
        ///     State of decal projector.
        /// </summary>
        public State NowState { get; private set; } = State.NotLaunch;

        /// <summary>
        ///     The list of decal mesh that has been generated by the projector.
        /// </summary>
        public List<DecalMesh> DecalMeshes { get; } = new List<DecalMesh>();

        private void Start()
        {
            if (launchOnAwake) Launch(null);
        }

        private void OnDestroy()
        {
            // The scheduled jobs are not stopped by the destruction, and they write to the pipeline's pooled
            // buffers. Complete them here so the next launch can reuse the pool safely, and unpin the source
            // so the pool can dispose it.
            ReleaseJobResources();

            // _pendingSourceHolder is deliberately not touched. The extraction may still be running (it only
            // notices the destruction at its next resume point) and it owns the mesh it is filling, so
            // disposing it here would free NativeArrays that are still being written. The extraction disposes
            // its own result when it observes the cancellation, and the launch body drops anything that was
            // handed over just before the destruction.

            // It may be deleted without completing the projection, so we finish it here too.
            // No geometry rollback is needed: the new pipeline appends to the decal meshes only on the main
            // thread after the jobs complete, so a canceled launch never leaves half-appended geometry.
            OnFinished(State.LaunchingCanceled);
        }

        private void OnDrawGizmosSelected()
        {
            var cache = Gizmos.matrix;
            Vector3 originPos = transform.position + transform.forward * depth * 0.5f;
            Gizmos.matrix = Matrix4x4.TRS(originPos, transform.rotation, Vector3.one);
            // Draw the decal box.
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, height, depth));
            Gizmos.matrix = cache;
            Gizmos.color = Color.white;
            // Draw the arrow of the projection's direction.
            var arrowLength = depth * 2.0f;
            var arrowStart = transform.position;
            var arrowEnd = transform.position + transform.forward * arrowLength;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            Vector3 arrowTangent;
            if (Mathf.Abs(transform.forward.y) > 0.999f)
                arrowTangent = Vector3.Cross(transform.forward, Vector3.right);
            else
                arrowTangent = Vector3.Cross(transform.forward, Vector3.up);
            var rotAxis = Vector3.Cross(transform.forward, arrowTangent);
            var rotQuat = Quaternion.AngleAxis(45.0f, rotAxis.normalized);
            var arrowLeft = rotQuat * transform.forward * arrowLength * -0.2f;
            Gizmos.DrawLine(arrowEnd, arrowEnd + arrowLeft);
            rotQuat = Quaternion.AngleAxis(-45.0f, rotAxis.normalized);
            var arrowRight = rotQuat * transform.forward * arrowLength * -0.2f;
            Gizmos.DrawLine(arrowEnd, arrowEnd + arrowRight);
            Gizmos.matrix = cache;
        }

        private void OnFinished(State finishedState)
        {
            if (onFinishedLaunch == null) return;

            // The state is published before the callback runs, so that a listener throwing cannot leave
            // NowState at Launching -- DecalProjectorLauncher would then wait for this projector forever.
            // Listeners get the state as their argument anyway.
            NowState = finishedState;
            var callback = onFinishedLaunch;
            onFinishedLaunch = null;
            callback.Invoke(finishedState);
        }

        /// <summary>
        ///     Execute projection decal to mesh.
        /// </summary>
        /// <remarks>
        ///     This process is performed over multiple frames.
        ///     Projection completion can be monitored using callback functions or by checking the NowState property.
        /// </remarks>
        /// <remarks>
        ///     Nothing awaits the returned Awaitable, so this method must never let an exception escape: it
        ///     would go unobserved, NowState would stay Launching and the launcher's queue would wait for a
        ///     state that never comes. Hence the try/catch/finally around the whole body.
        ///     <br />
        ///     Every resume point is followed immediately by a cancellation check, because unlike the coroutine
        ///     this replaces, an async method is not stopped by the projector's destruction: OnDestroy has
        ///     already completed the jobs and unpinned the source by then, so none of that state may be touched
        ///     afterwards. Those paths deliberately do not call OnFinished -- OnDestroy did.
        /// </remarks>
        private async Awaitable ExecuteLaunchAsync()
        {
            try
            {
                // Cached so the checks below never read the property off an already destroyed MonoBehaviour. The
                // token is only polled and never handed to Awaitable.NextFrameAsync: a cancelable await
                // registers a callback on the token's source, which would allocate on every frame this launch
                // waits. Read inside the try so that nothing at all can throw past the finally below.
                var cancellation = destroyCancellationToken;

                InitializeOriginAxisInDecalSpace();

                foreach (var receiverObject in receiverObjects)
                {
                    if (receiverObject == null || !receiverObject) continue;

                    _receiverDecalMeshes.Clear();
                    AirStickerSystem.CollectEditDecalMeshes(_receiverDecalMeshes, receiverObject, decalMaterial,
                        groupId);
                    DecalMeshes.AddRange(_receiverDecalMeshes);

                    receiverObject.GetComponentsInChildren(false, _skinnedMeshRenderers);
                    ExcludeDecalMeshRenderers(_skinnedMeshRenderers);
                    receiverObject.GetComponentsInChildren(false, _terrains);

                    if (AirStickerSystem.ReceiverObjectTrianglePolygonsPool.Contains(receiverObject) == false)
                    {
                        // New receiver object: extract its source triangle polygons (frame-sliced). The
                        // extraction owns the result until it returns and hands over only a complete one.
                        receiverObject.GetComponentsInChildren(false, _meshRenderers);
                        await AirStickerSystem.BuildTrianglePolygonsFromReceiverObjectAsync(
                            _meshRenderers,
                            _skinnedMeshRenderers,
                            _terrains,
                            _pendingSourceHolder,
                            cancellation);

                        if (cancellation.IsCancellationRequested)
                        {
                            // The extraction disposes its result when it observes the cancellation, but it may
                            // also have completed just before the destruction, so drop whatever it handed over.
                            DisposePendingSource();
                            return;
                        }

                        if (_pendingSourceHolder[0] == null)
                        {
                            // A receiver mesh is not Read/Write enabled, so there is nothing to build.
                            OnFinished(State.LaunchingCanceled);
                            return;
                        }

                        // The pool takes ownership of the source; stop tracking it here so it is never disposed
                        // as a pending one while it is pooled (and possibly pinned by a later launch).
                        AirStickerSystem.ReceiverObjectTrianglePolygonsPool.RegisterTrianglePolygons(
                            receiverObject, _pendingSourceHolder[0]);
                        _pendingSourceHolder[0] = null;
                    }

                    if (!receiverObject)
                    {
                        OnFinished(State.LaunchingCanceled);
                        return;
                    }

                    var source = AirStickerSystem.GetTrianglePolygonsFromPool(receiverObject);
                    if (source == null)
                    {
                        OnFinished(State.LaunchingCanceled);
                        return;
                    }

                    var pipeline = AirStickerSystem.JobPipeline;
                    var trans = transform;
                    // basePosition is the center of the decal box.
                    var centerPositionOfDecalBox = trans.position + trans.forward * (depth * 0.5f);

                    // Segment 1: skinning + broad phase + clip (parallel jobs).
                    source.InUse = true;
                    _currentSource = source;
                    _currentJobHandle = pipeline.ScheduleClipStage(
                        source, centerPositionOfDecalBox,
                        _decalSpace.Ex, _decalSpace.Ey, _decalSpace.Ez,
                        width, height, depth, projectionBackside);
                    _jobsScheduled = true;
                    JobHandle.ScheduleBatchedJobs();
                    await CompleteJobHandleAsync("clip stage (skinning + broad phase + clip)", cancellation);
                    if (cancellation.IsCancellationRequested) return;

                    if (!receiverObject)
                    {
                        ReleaseJobResources();
                        OnFinished(State.LaunchingCanceled);
                        return;
                    }

                    // Main-thread step between the segments: size the output from the clip result.
                    pipeline.CountBuild(source, _receiverDecalMeshes);

                    // Segment 2: build the appended geometry (serial job, off the main thread).
                    _currentJobHandle = pipeline.ScheduleBuildStage(
                        source, centerPositionOfDecalBox, _decalSpace.Ex, _decalSpace.Ey,
                        width, height, zOffsetInDecalSpace);
                    JobHandle.ScheduleBatchedJobs();
                    await CompleteJobHandleAsync("build stage (fan + uv + tangent)", cancellation);
                    if (cancellation.IsCancellationRequested) return;

                    _jobsScheduled = false;
                    source.InUse = false;
                    _currentSource = null;

                    // Merge the built geometry into the decal meshes and upload them (main thread).
                    pipeline.ApplyToDecalMeshes(_receiverDecalMeshes);
                    foreach (var decalMesh in _receiverDecalMeshes)
                        decalMesh.ExecutePostProcessingAfterWorkerThread();
                }

                OnFinished(State.LaunchingCompleted);
            }
            catch (Exception e)
            {
                // Ordered so the least fragile work happens first: a throw from here on would escape into the
                // unobserved Awaitable and leave the launcher waiting. Releasing what this launch owns keeps a
                // failure from pinning the source (the pool could never dispose it) or leaving jobs
                // uncompleted, and the log takes no context object so it cannot touch a destroyed projector.
                ReleaseJobResources();
                DisposePendingSource();
                Debug.LogException(e);
                OnFinished(State.LaunchingCanceled);
            }
            finally
            {
                _executingLaunch = false;
                // Released here rather than in OnFinished: OnDestroy calls that while the extraction may still
                // be iterating these very lists, and it unwinds only at its next resume point.
                ClearWorkingLists();
            }
        }

        /// <summary>
        ///     Complete the scheduled jobs and unpin the source mesh this launch was reading.
        /// </summary>
        private void ReleaseJobResources()
        {
            if (_jobsScheduled)
            {
                _currentJobHandle.Complete();
                _jobsScheduled = false;
            }

            if (_currentSource != null)
            {
                _currentSource.InUse = false;
                _currentSource = null;
            }
        }

        /// <summary>
        ///     Dispose a source mesh that the extraction handed over but that was never registered into the
        ///     pool, so its Persistent NativeArrays are not leaked. No job reads it yet, because jobs are
        ///     scheduled only after registration.
        /// </summary>
        private void DisposePendingSource()
        {
            if (_pendingSourceHolder[0] == null) return;

            _pendingSourceHolder[0].Dispose();
            _pendingSourceHolder[0] = null;
        }

        /// <summary>
        ///     Drop the decal renderers that hang under the receiver object, so they are not gathered as
        ///     receivers themselves. They are identified by <see cref="DecalMeshRendererMarker" />; see that
        ///     class for why the GameObject's name is not used.
        /// </summary>
        private static void ExcludeDecalMeshRenderers<T>(List<T> renderers) where T : Component
        {
            // Compacted in place so that neither the filtering nor the removal allocates.
            var writeNo = 0;
            for (var readNo = 0; readNo < renderers.Count; readNo++)
            {
                var renderer = renderers[readNo];
                if (renderer.TryGetComponent<DecalMeshRendererMarker>(out _)) continue;
                renderers[writeNo++] = renderer;
            }

            renderers.RemoveRange(writeNo, renderers.Count - writeNo);
        }

        /// <summary>
        ///     Release the receivers held by the reused working lists once the launch no longer needs them.
        /// </summary>
        private void ClearWorkingLists()
        {
            _receiverDecalMeshes.Clear();
            _meshRenderers.Clear();
            _skinnedMeshRenderers.Clear();
            _terrains.Clear();
        }

        /// <summary>
        ///     Wait for the current job stage. When performance logging is enabled the handle is completed
        ///     synchronously so the actual job compute time is measured; otherwise the main thread polls the
        ///     handle across frames without blocking.
        /// </summary>
        private async Awaitable CompleteJobHandleAsync(string label, CancellationToken cancellation)
        {
            if (AirStickerPerformanceLog.Enabled)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _currentJobHandle.Complete();
                sw.Stop();
                Debug.Log($"[AirSticker][Perf] {label}: {sw.Elapsed.TotalMilliseconds:F2} ms");
                return;
            }

            while (!_currentJobHandle.IsCompleted)
            {
                await Awaitable.NextFrameAsync();
                // Destroyed while waiting: OnDestroy has already completed the handle, so leave it alone. The
                // caller checks the same token and stops.
                if (cancellation.IsCancellationRequested) return;
            }

            _currentJobHandle.Complete();
        }

        /// <summary>
        ///     Create and add AirStickerProjector to the GameObject.
        /// </summary>
        /// <param name="owner">Game object to which the component will be added.</param>
        /// <param name="receiverObject">Receiver object to which decal is applied.</param>
        /// <param name="decalMaterial">Decal material applied to receiver object.</param>
        /// <param name="width">Width of projector. It means to projection range.</param>
        /// <param name="height">Height of projector. It means to projection range.</param>
        /// <param name="depth">Depth of projector. It means to projection range.</param>
        /// <param name="launchOnAwake">
        ///     If it is true, the decal projection is started at same time as additional component.
        ///     If it is false, the decal projection is started by explicitly calling the Launch method.
        /// </param>
        /// <param name="onCompletedLaunch">Callback function called when decal projection is complete.</param>
        /// <param name="zOffsetInDecalSpace">The Z offset of the decal space from the receiver surface.</param>
        /// <param name="groupId">
        ///     The group ID of the decal mesh. <br />
        ///     Decal meshes are shared only within the same group,
        ///     so decals can be removed by group using the RemoveDecalMeshes method. <br />
        ///     If it is not specified, the decal belongs to the group 0.
        /// </param>
        public static AirStickerProjector CreateAndLaunch(
            GameObject owner,
            GameObject receiverObject,
            Material decalMaterial,
            float width,
            float height,
            float depth,
            bool launchOnAwake,
            UnityAction<State> onCompletedLaunch,
            float zOffsetInDecalSpace = 0.005f,
            int groupId = 0)
        {
            return CreateAndLaunch(
                owner,
                new[] { receiverObject },
                decalMaterial,
                width,
                height,
                depth,
                launchOnAwake,
                onCompletedLaunch,
                zOffsetInDecalSpace,
                groupId);
        }

        /// <summary>
        ///     Create and add AirStickerProjector to the GameObject.
        /// </summary>
        /// <remarks>
        ///     This overload projects the decal onto multiple receiver objects at once,
        ///     so a decal can be pasted across the boundary of the receivers. <br />
        ///     One decal mesh is built per (receiver object, renderer, decal material, group ID),
        ///     so the more receivers the decal spans, the more draw calls are made. <br />
        ///     If any receiver's mesh is not Read/Write enabled, the launch is canceled at that receiver
        ///     and the remaining receivers are not processed.
        /// </remarks>
        /// <param name="owner">Game object to which the component will be added.</param>
        /// <param name="receiverObjects">Receiver objects to which decal is applied.</param>
        /// <param name="decalMaterial">Decal material applied to receiver objects.</param>
        /// <param name="width">Width of projector. It means to projection range.</param>
        /// <param name="height">Height of projector. It means to projection range.</param>
        /// <param name="depth">Depth of projector. It means to projection range.</param>
        /// <param name="launchOnAwake">
        ///     If it is true, the decal projection is started at same time as additional component.
        ///     If it is false, the decal projection is started by explicitly calling the Launch method.
        /// </param>
        /// <param name="onCompletedLaunch">Callback function called when decal projection is complete.</param>
        /// <param name="zOffsetInDecalSpace">The Z offset of the decal space from the receiver surface.</param>
        /// <param name="groupId">
        ///     The group ID of the decal mesh. <br />
        ///     Decal meshes are shared only within the same group,
        ///     so decals can be removed by group using the RemoveDecalMeshes method. <br />
        ///     If it is not specified, the decal belongs to the group 0.
        /// </param>
        public static AirStickerProjector CreateAndLaunch(
            GameObject owner,
            GameObject[] receiverObjects,
            Material decalMaterial,
            float width,
            float height,
            float depth,
            bool launchOnAwake,
            UnityAction<State> onCompletedLaunch,
            float zOffsetInDecalSpace = 0.005f,
            int groupId = 0)
        {
            var projector = owner.AddComponent<AirStickerProjector>();
            projector.width = width;
            projector.height = height;
            projector.depth = depth;
            projector.zOffsetInDecalSpace = zOffsetInDecalSpace;
            // Copy the array so that the caller mutating it during the multi-frame launch
            // does not change the projection targets.
            projector.receiverObjects = receiverObjects != null
                ? (GameObject[])receiverObjects.Clone()
                : null;
            projector.decalMaterial = decalMaterial;
            projector.groupId = groupId;
            projector.launchOnAwake = false;
            projector.onFinishedLaunch = new UnityEvent<State>();

            if (launchOnAwake)
                projector.Launch(onCompletedLaunch);
            else if (onCompletedLaunch != null) projector.onFinishedLaunch.AddListener(onCompletedLaunch);

            return projector;
        }

        /// <summary>
        ///     Remove the decal meshes that belong to the specified group.
        /// </summary>
        /// <remarks>
        ///     The decal meshes are removed from the pool and their renderers are destroyed. <br />
        ///     This method should be called after the projectors of the target group
        ///     have finished launching (LaunchingCompleted). <br />
        ///     If it is called while a projector of the target group is launching,
        ///     the launching result is undefined.
        /// </remarks>
        /// <param name="groupId">The group ID of the decal meshes to be removed.</param>
        /// <param name="receiverObject">
        ///     If it is not null, only the decal meshes projected to this receiver object are removed.
        /// </param>
        /// <param name="decalMaterial">
        ///     If it is not null, only the decal meshes using this decal material are removed.
        /// </param>
        public static void RemoveDecalMeshes(
            int groupId,
            GameObject receiverObject = null,
            Material decalMaterial = null)
        {
            AirStickerSystem.DecalMeshPool?.RemoveDecalMeshes(groupId, receiverObject, decalMaterial);
        }

        /// <summary>
        ///     Start projection decal.
        /// </summary>
        /// <remarks>
        ///     This processing is async, so the projection decal takes several frames to finish.
        ///     If you want to monitor the decal projection process, should be using a callback function.
        /// </remarks>
        public void Launch(UnityAction<State> onFinishedLaunch)
        {
            if (NowState != State.NotLaunch)
                Debug.LogError("This function can be called only once, but it was called multiply.");

            NowState = State.Launching;
            if (onFinishedLaunch != null) this.onFinishedLaunch.AddListener(onFinishedLaunch);
            // Request the launching of the decal.
            AirStickerSystem.DecalProjectorLauncher.Request(this);
        }

        /// <summary>
        ///     Start this projector's launch. Called by the launcher when the queued request reaches the front
        ///     of the queue.
        /// </summary>
        /// <remarks>
        ///     This is a method instead of a callback handed to <c>Request</c>, because a lambda there
        ///     allocated a delegate for every decal.
        /// </remarks>
        internal void OnLaunchRequestAccepted()
        {
            if (receiverObjects == null)
            {
                // Receiver object has been dead, so process is terminated.
                OnFinished(State.LaunchingCanceled);
                return;
            }

            // Started without being awaited. The body reports through onFinishedLaunch / NowState and handles
            // its own exceptions, so there is nothing to observe here. It is an Awaitable-returning method
            // rather than async void so that the state machine comes from Unity's pool.
            _executingLaunch = true;
            _ = ExecuteLaunchAsync();
        }

        private void InitializeOriginAxisInDecalSpace()
        {
            var trans = transform;
            _decalSpace = new DecalSpace(trans.right, trans.up, trans.forward * -1.0f);
        }
    }
}
