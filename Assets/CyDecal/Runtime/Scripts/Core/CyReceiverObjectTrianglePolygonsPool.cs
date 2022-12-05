using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     凸多角形情報
    /// </summary>
    internal class ConvexPolygonInfo
    {
        public CyConvexPolygon ConvexPolygon { get; set; } // 凸多角形
        public bool IsOutsideClipSpace { get; set; } // クリップ平面の外側？
    }


    /// <summary>
    ///     ターゲットオブジェクトの三角形ポリゴンブール
    /// </summary>
    internal sealed class CyReceiverObjectTrianglePolygonsPool
    {
        Dictionary<GameObject, List<ConvexPolygonInfo>> _convexPolygonsPool = new Dictionary<GameObject, List<ConvexPolygonInfo>>();

        public IReadOnlyDictionary <GameObject, List<ConvexPolygonInfo>> ConvexPolygonsPool
        {
            get => _convexPolygonsPool;
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
        internal void RegisterConvexPolygons(GameObject receiverObject, List<ConvexPolygonInfo> convexPolygonInfos)
        {
            if (receiverObject
                && !Contains(receiverObject))
                // 処理再開時にレシーバーオブジェクトが破棄されている可能性があるのでオブジェクトが生きているかチェックを入れる。
                _convexPolygonsPool.Add(receiverObject, convexPolygonInfos);
        }

        /// <summary>
        ///     指定したレシーバーオブジェクトの凸ポリゴン情報が登録済みか判定する。
        /// </summary>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <returns></returns>
        public bool Contains(GameObject receiverObject)
        {
            return ConvexPolygonsPool.ContainsKey(receiverObject);
        }

        /// <summary>
        ///     プールをガベージコレクト
        /// </summary>
        /// <remarks>
        ///     キーとなっているレシーバーオブジェクトが削除されていたら、プールから除去する。
        /// </remarks>
        public void GarbageCollect()
        {
            var deleteList = ConvexPolygonsPool.Where(item => !item.Key).ToList();
            foreach (var item in deleteList) _convexPolygonsPool.Remove(item.Key);
        }

        /// <summary>
        ///     プールのサイズを取得。
        /// </summary>
        /// <returns></returns>
        public int GetPoolSize()
        {
            return ConvexPolygonsPool.Count;
        }
    }
}
