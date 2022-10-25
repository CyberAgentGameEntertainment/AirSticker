

using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// ラインを表すクラス。
    /// </summary>
    public struct CyLine
    {
        public Vector3 startPosition { get; private set; }  // ラインの始点の座標
        public Vector3 endPosition { get; private set; }    // ラインの終点の座標
        public Vector3 startToEndVec { get; private set; }  // 始点から終点に向かって伸びるベクトル
        public Vector3 startNormal { get; private set; }    // 始点の法線
        public Vector3 endNormal { get; private set; }      // 終点の法線
        public CyLine(Vector3 startPosition, Vector3 endPosition, Vector3 startNormal, Vector3 endNormal)
        {
            this.startPosition = startPosition;
            this.endPosition = endPosition;
            this.startNormal = startNormal;
            this.endNormal = endNormal;
            startToEndVec = this.endPosition - this.startPosition;
        }

        /// <summary>
        /// ラインの始点の座標を設定後、始点から終点に向かって伸びるベクトルを再計算します。
        /// </summary>
        /// <param name="newStartPosition">新しいラインの始点の座標</param>
        /// <param name="newStartNormal">新しいラインの始点の法線</param>
        public void SetStartAndCalcStartToEnd(Vector3 newStartPosition, Vector3 newStartNormal)
        {
            startPosition = newStartPosition;
            startNormal = newStartNormal;
            startToEndVec = endPosition - startPosition;
        }

        /// <summary>
        /// ラインの終点の座標を設定後、始点から終点に向かって伸びるベクトルを再計算します。
        /// </summary>
        /// <param name="newEndPosition">新しいラインの終点の座標</param>
        /// <param name="newEndNormal">新しいラインの終点の法線</param>
        public void SetEndAndCalcStartToEnd(Vector3 newEndPosition, Vector3 newEndNormal)
        {
            endPosition = newEndPosition;
            endNormal = newEndNormal;
            startToEndVec = endPosition - startPosition;
        }

        /// <summary>
        /// ラインの始点と終点の座標を設定後、始点から終点に向かって伸びるベクトルを再計算します。
        /// </summary>
        /// <param name="newStartPosition">新しいラインの始点の座標</param>
        /// <param name="newEndPosition">新しいラインの終点の座標</param>
        /// <param name="newStartNormal">新しいラインの始点の法線</param>
        /// <param name="newEndNormal">新しいラインの終点の法線</param>
        public void SetStartEndAndCalcStartToEnd(
            Vector3 newStartPosition, 
            Vector3 newEndPosition,
            Vector3 newStartNormal,
            Vector3 newEndNormal)
        {
            startPosition = newStartPosition;
            endPosition = newEndPosition;
            startNormal = newStartNormal;
            endNormal = newEndNormal;
            startToEndVec = endPosition - startPosition;
        }
    };
}
