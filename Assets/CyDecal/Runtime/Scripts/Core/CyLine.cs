using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     Line of convex polygons.
    /// </summary>
    public struct CyLine
    {
        public Vector3 StartPosition { get; private set; }
        public Vector3 EndPosition { get; private set; }
        public Vector3 StartToEndVec { get; private set; }
        public Vector3 StartNormal { get; private set; }
        public Vector3 EndNormal { get; private set; }
        public BoneWeight StartWeight { get; private set; }
        public BoneWeight EndWeight { get; private set; }
        public Vector3 StartLocalPosition { get; private set; }
        public Vector3 EndLocalPosition { get; private set; }
        public Vector3 StartLocalNormal { get; private set; }
        public Vector3 EndLocalNormal { get; private set; }

        /// <summary>
        ///     初期化
        /// </summary>
        /// <param name="startPosition">始点の座標</param>
        /// <param name="endPosition">終点の座標</param>
        /// <param name="startNormal">始点の法線</param>
        /// <param name="endNormal">終点の法線</param>
        /// <param name="startWeight">始点のボーンウェイト</param>
        /// <param name="endWeight">終点のボーンウェイト</param>
        /// <param name="startLocalPosition">始点のローカル座標</param>
        public void Initialize(Vector3 startPosition, Vector3 endPosition, Vector3 startNormal, Vector3 endNormal,
            BoneWeight startWeight, BoneWeight endWeight, Vector3 startLocalPosition, Vector3 endLocalPosition,
            Vector3 startLocalNormal, Vector3 endLocalNormal)
        {
            StartPosition = startPosition;
            EndPosition = endPosition;
            StartNormal = startNormal;
            EndNormal = endNormal;
            StartToEndVec = EndPosition - StartPosition;
            StartWeight = startWeight;
            EndWeight = endWeight;
            StartLocalPosition = startLocalPosition;
            EndLocalPosition = endLocalPosition;
            StartLocalNormal = startLocalNormal;
            EndLocalNormal = endLocalNormal;
        }

        /// <summary>
        ///     ラインの終点の座標を設定後、始点から終点に向かって伸びるベクトルを再計算します。
        /// </summary>
        /// <param name="newEndPosition">新しいラインの終点の座標</param>
        /// <param name="newEndNormal">新しいラインの終点の法線</param>
        public void SetEndAndCalcStartToEnd(Vector3 newEndPosition, Vector3 newEndNormal,
            Vector3 newEndLocalPosition, Vector3 newEndLocalNormal)
        {
            EndPosition = newEndPosition;
            EndNormal = newEndNormal;
            EndLocalPosition = newEndLocalPosition;
            EndLocalNormal = newEndLocalNormal;
            StartToEndVec = EndPosition - StartPosition;
        }

        /// <summary>
        ///     ラインの始点と終点の座標を設定後、始点から終点に向かって伸びるベクトルを再計算します。
        /// </summary>
        /// <param name="newStartPosition">新しいラインの始点の座標</param>
        /// <param name="newEndPosition">新しいラインの終点の座標</param>
        /// <param name="newStartNormal">新しいラインの始点の法線</param>
        /// <param name="newEndNormal">新しいラインの終点の法線</param>
        public void SetStartEndAndCalcStartToEnd(
            Vector3 newStartPosition,
            Vector3 newEndPosition,
            Vector3 newStartNormal,
            Vector3 newEndNormal,
            Vector3 newStartLocalPosition,
            Vector3 newEndLocalPosition,
            Vector3 newStartLocalNormal,
            Vector3 newEndLocalNormal)
        {
            StartPosition = newStartPosition;
            EndPosition = newEndPosition;
            StartNormal = newStartNormal;
            EndNormal = newEndNormal;

            StartLocalPosition = newStartLocalPosition;
            EndLocalPosition = newEndLocalPosition;
            StartLocalNormal = newStartLocalNormal;
            EndLocalNormal = newEndLocalNormal;

            StartToEndVec = EndPosition - StartPosition;
        }

        /// <summary>
        ///     始点と終点のボーンウェイトを設定。
        /// </summary>
        /// <param name="newStartWeight">新しい始点のボーンウェイト</param>
        /// <param name="newEndWeight">新しい終点のボーンウェイト</param>
        public void SetStartEndBoneWeights(BoneWeight newStartWeight, BoneWeight newEndWeight)
        {
            StartWeight = newStartWeight;
            EndWeight = newEndWeight;
        }

        /// <summary>
        ///     終点のボーンウェイトを設定
        /// </summary>
        /// <param name="newEndWeight">新しい終点のボーンウェイト</param>
        public void SetEndBoneWeight(BoneWeight newEndWeight)
        {
            EndWeight = newEndWeight;
        }
    }
}
