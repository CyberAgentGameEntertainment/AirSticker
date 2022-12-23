using System;
using System.Collections.Generic;

namespace CyDecal.Runtime.Scripts.Core
{
    internal interface ICyDecalProjectorLauncher
    {
        void Update();
    }
    /// <summary>
    ///     デカールプロジェクターのランチャー
    /// </summary>
    /// <remarks>
    ///     デカールプロジェクターはランチャークラスを経由して起動します。
    ///     起動リクエストは待ち行列にキューイングされて、適切なタイミングでデカールプロジェクターがローンチされます。
    /// </remarks>
    public sealed class CyDecalProjectorLauncher : ICyDecalProjectorLauncher
    {
        private LaunchRequest _currentRequest; // 現在実行中のリクエスト。
        private readonly Queue<LaunchRequest> _launchRequestQueues = new Queue<LaunchRequest>();

        /// <summary>
        ///     起動リクエストを待ち行列にキューイング
        /// </summary>
        public void Request(CyDecalProjector projector, Action onLaunch)
        {
            _launchRequestQueues.Enqueue(new LaunchRequest(projector, onLaunch));
        }

        /// <summary>
        ///     現在処理中のリクエストの処理が完了しているか判定。
        /// </summary>
        /// <returns></returns>
        private bool IsCurrentRequestIsFinished()
        {
            return _currentRequest == null // そもそもリクエストを発行していない
                   || !_currentRequest.Projector // リクエストを投げたプロジェクターが死亡している。
                   || _currentRequest.Projector.NowState == CyDecalProjector.State.LaunchingCompleted; // プロジェクションが完了している。
        }

        void ICyDecalProjectorLauncher.Update()
        {
            if (!IsCurrentRequestIsFinished())
            {
                //　まだ処理中なので次のリクエストの処理は行わない。
            }

            // 次のリクエストを処理する。
            ProcessNextRequest();
        }

        /// <summary>
        ///     次のリクエストを処理する。
        /// </summary>
        private void ProcessNextRequest()
        {
            while (_launchRequestQueues.Count > 0)
            {
                _currentRequest = _launchRequestQueues.Peek();
                _launchRequestQueues.Dequeue();
                if (!_currentRequest.Projector) continue; // このリクエストはすでに死んでいるので次。
                _currentRequest.OnLaunch();
                // ラウンチしたので抜ける。
                break;
            }
        }

        /// <summary>
        ///     待ち状態のリクエスト数を取得
        /// </summary>
        /// <returns></returns>
        public int GetWaitingRequestCount()
        {
            return _launchRequestQueues.Count;
        }

        // ラウンチリクエスト
        private class LaunchRequest
        {
            public LaunchRequest(CyDecalProjector projector, Action onLaunch)
            {
                Projector = projector;
                OnLaunch = onLaunch;
            }

            public CyDecalProjector Projector { get; }
            public Action OnLaunch { get; }
        }
    }
}
