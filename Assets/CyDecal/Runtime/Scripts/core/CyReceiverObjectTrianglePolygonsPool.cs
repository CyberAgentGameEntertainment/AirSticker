using System.Collections;
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


    /// <summary>
    ///     ターゲットオブジェクトの三角形ポリゴンブール
    /// </summary>
    public class CyReceiverObjectTrianglePolygonsPool
    {
        public Dictionary<GameObject, List<ConvexPolygonInfo>> ConvexPolygonsPool { get; } = new Dictionary<GameObject, List<ConvexPolygonInfo>>();

        /// <summary>
        ///     プールをクリア
        /// </summary>
        public void Clear()
        {
            ConvexPolygonsPool.Clear();
        }

        /// <summary>
        ///     デカールを貼り付けるレシーバーオブジェクトの情報から凸多角形ポリゴンを登録する。
        /// </summary>
        /// <param name="receiverObject">デカールが貼り付けられるレシーバーオブジェクト</param>
        /// <param name="meshFilters">レシーバーオブジェクトのメッシュフィルター</param>
        /// <param name="meshRenderer">レシーバーオブジェクトのメッシュレンダラー</param>
        /// <param name="skinnedMeshRenderers">レシーバーオブジェクトのスキンメッシュレンダラー</param>
        public IEnumerator RegisterConvexPolygons(
            GameObject receiverObject,
            MeshFilter[] meshFilters,
            MeshRenderer[] meshRenderer,
            SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            // 新規登録
            var convexPolygonInfos = new List<ConvexPolygonInfo>();
            // 三角形ポリゴン情報を構築する。
            yield return CyTrianglePolygonsFactory.BuildFromReceiverObject(meshFilters,
                meshRenderer,
                skinnedMeshRenderers,
                convexPolygonInfos);
            if (receiverObject
                && !ExistConvexPolygons(receiverObject))
                // 処理再開時にレシーバーオブジェクトが破棄されている可能性があるのでオブジェクトが生きているかチェックを入れる。
                ConvexPolygonsPool.Add(receiverObject, convexPolygonInfos);
        }

        /// <summary>
        ///     すでにプールに凸ポリゴン情報が登録されているか判定
        /// </summary>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <returns></returns>
        public bool ExistConvexPolygons(GameObject receiverObject)
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
            foreach (var item in deleteList) ConvexPolygonsPool.Remove(item.Key);
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
