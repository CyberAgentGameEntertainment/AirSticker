using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     凸多角形情報
    /// </summary>
    public class ConvexPolygonInfo
    {
        public CyConvexPolygon ConvexPolygon { get; set; } // 凸多角形
        public bool IsOutsideClipSpace { get; set; } // クリップ平面の外側？
    }

    internal interface ICyReceiverObjectTrianglePolygonsPool
    {
        IReadOnlyDictionary<GameObject, List<ConvexPolygonInfo>> ConvexPolygonsPool { get; }

        bool Contains(GameObject receiverObject);
        void GarbageCollect();
    }

    /// <summary>
    ///     ターゲットオブジェクトの三角形ポリゴンブール
    /// </summary>
    public sealed class CyReceiverObjectTrianglePolygonsPool : ICyReceiverObjectTrianglePolygonsPool
    {
        private readonly Dictionary<GameObject, List<ConvexPolygonInfo>> _convexPolygonsPool =
            new Dictionary<GameObject, List<ConvexPolygonInfo>>();

        IReadOnlyDictionary<GameObject, List<ConvexPolygonInfo>> ICyReceiverObjectTrianglePolygonsPool.
            ConvexPolygonsPool => _convexPolygonsPool;

        /// <summary>
        ///     指定したレシーバーオブジェクトの凸ポリゴン情報が登録済みか判定する。
        /// </summary>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <returns></returns>
        public bool Contains(GameObject receiverObject)
        {
            return _convexPolygonsPool.ContainsKey(receiverObject);
        }

        /// <summary>
        ///     プールをガベージコレクト
        /// </summary>
        /// <remarks>
        ///     キーとなっているレシーバーオブジェクトが削除されていたら、プールから除去する。
        /// </remarks>
        void ICyReceiverObjectTrianglePolygonsPool.GarbageCollect()
        {
            var deleteList = _convexPolygonsPool.Where(item => item.Key == null).ToList();
            foreach (var item in deleteList) _convexPolygonsPool.Remove(item.Key);
        }

        /// <summary>
        ///     プールをクリア
        /// </summary>
        public void Clear()
        {
            _convexPolygonsPool.Clear();
        }

        /// <summary>
        ///     デカールを貼り付けるレシーバーオブジェクトの情報から凸多角形ポリゴンを登録する。
        /// </summary>
        /// <param name="receiverObject">デカールが貼り付けられるレシーバーオブジェクト</param>
        /// <param name="meshFilters">レシーバーオブジェクトのメッシュフィルター</param>
        /// <param name="meshRenderer">レシーバーオブジェクトのメッシュレンダラー</param>
        /// <param name="skinnedMeshRenderers">レシーバーオブジェクトのスキンメッシュレンダラー</param>
        public void RegisterConvexPolygons(GameObject receiverObject, List<ConvexPolygonInfo> convexPolygonInfos)
        {
            if (receiverObject
                && !this.Contains(receiverObject))
                // 処理再開時にレシーバーオブジェクトが破棄されている可能性があるのでオブジェクトが生きているかチェックを入れる。
                _convexPolygonsPool.Add(receiverObject, convexPolygonInfos);
        }

        /// <summary>
        ///     プールのサイズを取得。
        /// </summary>
        /// <returns></returns>
        public int GetPoolSize()
        {
            return _convexPolygonsPool.Count;
        }
    }
}
