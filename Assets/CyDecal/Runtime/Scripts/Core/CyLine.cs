using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     ラインを表すクラス。
    /// </summary>
    public struct CyLine
    {
        public Vector3 StartPosition { get; private set; } // ラインの始点の座標
        public Vector3 EndPosition { get; private set; } // ラインの終点の座標
        public Vector3 StartToEndVec { get; private set; } // 始点から終点に向かって伸びるベクトル
        public Vector3 StartNormal { get; private set; } // 始点の法線
        public Vector3 EndNormal { get; private set; } // 終点の法線
        public BoneWeight StartWeight { get; private set; } // 始点のボーンウェイト
        public BoneWeight EndWeight { get; private set; } // 終点のボーンウェイト

        /// <summary>
        ///     初期化
        /// </summary>
        /// <param name="startPosition">始点の座標</param>
        /// <param name="endPosition">終点の座標</param>
        /// <param name="startNormal">始点の法線</param>
        /// <param name="endNormal">終点の法線</param>
        /// <param name="startWeight">始点のボーンウェイト</param>
        /// <param name="endWeight">終点のボーンウェイト</param>
        public void Initialize(Vector3 startPosition, Vector3 endPosition, Vector3 startNormal, Vector3 endNormal, 
            BoneWeight startWeight, BoneWeight endWeight)
        {
            StartPosition = startPosition;
            EndPosition = endPosition;
            StartNormal = startNormal;
            EndNormal = endNormal;
            StartToEndVec = EndPosition - StartPosition;
            StartWeight = startWeight;
            EndWeight = endWeight;
        }

        /// <summary>
        ///     ラインの終点の座標を設定後、始点から終点に向かって伸びるベクトルを再計算します。
        /// </summary>
        /// <param name="newEndPosition">新しいラインの終点の座標</param>
        /// <param name="newEndNormal">新しいラインの終点の法線</param>
        public void SetEndAndCalcStartToEnd(Vector3 newEndPosition, Vector3 newEndNormal)
        {
            EndPosition = newEndPosition;
            EndNormal = newEndNormal;
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
            Vector3 newEndNormal)
        {
            StartPosition = newStartPosition;
            EndPosition = newEndPosition;
            StartNormal = newStartNormal;
            EndNormal = newEndNormal;
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
