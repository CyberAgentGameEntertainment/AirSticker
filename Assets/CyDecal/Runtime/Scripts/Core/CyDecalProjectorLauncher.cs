using System;
using System.Collections.Generic;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカールプロジェクターのランチャー
    /// </summary>
    /// <remarks>
    ///     デカールプロジェクターはランチャークラスを経由して起動します。
    ///     起動リクエストは待ち行列にキューイングされて、適切なタイミングでデカールプロジェクターがローンチされます。
    /// </remarks>
    internal sealed class CyDecalProjectorLauncher
    {
        private LaunchRequest _currentRequest; // 現在実行中のリクエスト。
        private readonly Queue<LaunchRequest> _launchRequestQueues = new Queue<LaunchRequest>();

        /// <summary>
        ///     起動リクエストを待ち行列にキューイング
        /// </summary>
        public void EnqueueLaunchRequest(CyDecalProjector projector, Action onLaunch)
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
                   || _currentRequest.Projector.NowState == CyDecalProjector.State.Launched; // プロジェクションが完了している。
        }

        public void Update()
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
        ///     リクエスト数を取得
        /// </summary>
        /// <returns></returns>
        public int GetRequestCount()
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
