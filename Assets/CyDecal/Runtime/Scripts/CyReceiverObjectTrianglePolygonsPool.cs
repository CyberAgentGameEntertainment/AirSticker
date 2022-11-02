using System.Collections.Generic;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     凸多角形情報
    /// </summary>
    public class ConvexPolygonInfo
    {
        public CyConvexPolygon ConvexPolygon { get; set; } // 凸多角形
        public bool IsOutsideClipSpace { get; set; } // クリップ平面の外側？
    }


    /// <summary>
    ///     ターゲットオブジェクトの三角形ポリゴンブール
    /// </summary>
    public class CyReceiverObjectTrianglePolygonsPool
    {
        public Dictionary<GameObject, List<ConvexPolygonInfo>> ConvexPolygonsPool { get; } = new Dictionary<GameObject, List<ConvexPolygonInfo>>();
        /// <summary>
        /// プールをクリア
        /// </summary>
        public void Clear()
        {
            ConvexPolygonsPool.Clear();
        }
        /// <summary>
        ///     デカールを貼り付けるレシーバーオブジェクトの情報から凸多角形ポリゴンを登録する。
        /// </summary>
        /// <param name="receiverObject">デカールが貼り付けられるレシーバーオブジェクト</param>
        public void RegisterConvexPolygons(GameObject receiverObject)
        {
            if (ConvexPolygonsPool.ContainsKey(receiverObject))
                // 登録済み
                return;

            // 新規登録。
            var convexPolygonInfos = new List<ConvexPolygonInfo>();
            // 三角形ポリゴン情報を構築する。
            CyTrianglePolygonsFactory.BuildFromReceiverObject(receiverObject, convexPolygonInfos);
            ConvexPolygonsPool.Add(receiverObject, convexPolygonInfos);
        }
    }
}
