using System;
using System.Collections.Generic;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカールプロジェクターのFIFOキュー
    /// </summary>
    /// <remarks>
    ///     同時に実行されるプロジェクション処理の数を制御するための待ち行列キュー
    /// </remarks>
    internal sealed class CyLaunchDecalProjectorRequestQueue
    {
        private LaunchRequest _currentRequest; // 現在実行中のリクエスト。
        private readonly Queue<LaunchRequest> _launchRequestQueues = new Queue<LaunchRequest>();

        /// <summary>
        ///     リクエストを待ち行列にキューイング
        /// </summary>
        public void Enqueue(CyDecalProjector projector, Action onLaunch)
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
                   || _currentRequest.Projector.IsLaunched; // プロジェクションが完了している。
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
