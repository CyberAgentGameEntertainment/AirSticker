using System;
using System.Collections.Generic;

namespace AirSticker.Runtime.Scripts.Core
{
    internal interface IDecalProjectorLauncher
    {
        void Update();
    }

    /// <summary>
    ///     Launcher of decal projector.
    /// </summary>
    /// <remarks>
    ///     The decal projector is activated via this class.
    ///     The decal projector is queued in the queue and it is launched at the appropriate time.
    /// </remarks>
    public sealed class DecalProjectorLauncher : IDecalProjectorLauncher
    {
        private readonly Queue<LaunchRequest> _launchRequestQueues = new Queue<LaunchRequest>();
        private LaunchRequest _currentRequest;
        private bool _hasCurrentRequest;

        void IDecalProjectorLauncher.Update()
        {
            if (!IsCurrentRequestFinished())
                // The current request is still running, so returned.
                return;

            ProcessNextRequest();
        }

        /// <summary>
        ///     Queueing startup requests to the queue.
        /// </summary>
        /// <remarks>
        ///     The projector's own launch method is called when the request is processed, so no callback has
        ///     to be allocated per decal. <see cref="Request(AirStickerProjector, Action)" /> remains for
        ///     callers that need a custom callback.
        /// </remarks>
        internal void Request(AirStickerProjector projector)
        {
            _launchRequestQueues.Enqueue(new LaunchRequest(projector, null));
        }

        /// <summary>
        ///     Queueing startup requests to the queue.
        /// </summary>
        public void Request(AirStickerProjector projector, Action onLaunch)
        {
            _launchRequestQueues.Enqueue(new LaunchRequest(projector, onLaunch));
        }

        private bool IsCurrentRequestFinished()
        {
            if (!_hasCurrentRequest) return true; // The request is empty.

            var projector = _currentRequest.Projector;
            // Even if the projector is dead or the launching is canceled, its launch may still be unwinding:
            // destroying the projector neither stops the scheduled jobs nor the async launch body, which only
            // observes the destruction at its next resume point. Starting the next launch in that state
            // corrupts the state the pipeline and the triangle extraction share between launches, so the next
            // request must wait for both.
            // The C# instance is still readable after the Unity object is destroyed.
            if (projector.IsWorkerThreadRunning || projector.IsExecutingLaunch) return false;

            return !projector // Projector that threw the request is dead.
                   || projector.NowState == AirStickerProjector.State.LaunchingCompleted
                   || projector.NowState == AirStickerProjector.State.LaunchingCanceled;
        }

        private void ProcessNextRequest()
        {
            while (_launchRequestQueues.Count > 0)
            {
                _currentRequest = _launchRequestQueues.Dequeue();
                _hasCurrentRequest = true;
                if (!_currentRequest.Projector) continue; // This request was dead, so skipped.
                if (_currentRequest.OnLaunch != null)
                    _currentRequest.OnLaunch();
                else
                    _currentRequest.Projector.OnLaunchRequestAccepted();
                break;
            }
        }

        public int GetWaitingRequestCount()
        {
            return _launchRequestQueues.Count;
        }

        // A struct so that queueing a request does not allocate. OnLaunch is null for the projector's own
        // launch path, which calls AirStickerProjector.OnLaunchRequestAccepted instead of a delegate.
        private readonly struct LaunchRequest
        {
            public LaunchRequest(AirStickerProjector projector, Action onLaunch)
            {
                Projector = projector;
                OnLaunch = onLaunch;
            }

            public AirStickerProjector Projector { get; }
            public Action OnLaunch { get; }
        }
    }
}
