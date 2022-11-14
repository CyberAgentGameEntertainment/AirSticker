using System;
using System.Collections;
using System.Collections.Generic;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールシステム
    /// </summary>
    public class CyDecalSystem : MonoBehaviour
    {
        // デカールメッシュプール。
        private readonly CyDecalMeshPool _decalMeshPool = new CyDecalMeshPool();

        // デカールプロジェクタのラウンチリクエストキュー
        private readonly CyLaunchDecalProjectorRequestQueue _launchDecalProjectorRequestQueue = new CyLaunchDecalProjectorRequestQueue();

        // デカールメッシュを貼り付けるレシーバーオブジェクトのプール。
        private readonly CyReceiverObjectTrianglePolygonsPool _receiverObjectTrianglePolygonsPool = new CyReceiverObjectTrianglePolygonsPool();

        public CyDecalSystem()
        {
            Instance = this;
        }

        private static CyDecalSystem Instance { get; set; }

        /// <summary>
        ///     更新
        /// </summary>
        private void Update()
        {
            // 各種プールのガベージコレクトを実行。
            _receiverObjectTrianglePolygonsPool.GarbageCollect();
            _decalMeshPool.GarbageCollect();
            // デカールの投影リクエストを処理する。
            _launchDecalProjectorRequestQueue.Update();
        }

        /// <summary>
        ///     待ち行列にキューイングされているデカールプロジェクタのラウンチリクエストの数を取得する。
        /// </summary>
        /// <returns></returns>
        public static int GetNumRequestLaunchDecalProjector()
        {
            return Instance._launchDecalProjectorRequestQueue.GetNumRequest();
        }

        /// <summary>
        ///     デカールプロジェクターをラウンチリクエストを待ち行列にキューイングする。
        /// </summary>
        public static void EnqueueRequestLaunchDecalProjector(CyDecalProjector projector, Action onLaunch)
        {
            Instance._launchDecalProjectorRequestQueue.Enqueue(projector, onLaunch);
        }

        /// <summary>
        ///     デカールメッシュレンダラーを無効にします。
        /// </summary>
        public static void DisableDecalMeshRenderers()
        {
            if (Instance == null) return;
            Instance._decalMeshPool.DisableDecalMeshRenderers();
        }

        /// <summary>
        ///     デカールメッシュレンダラーを有効にします。
        /// </summary>
        public static void EnableDecalMeshRenderers()
        {
            if (Instance == null) return;
            Instance._decalMeshPool.EnableDecalMeshRenderers();
        }

        /// <summary>
        ///     デカールメッシュの取得。
        /// </summary>
        /// <param name="results">デカールメッシュの格納先</param>
        /// <param name="projectorObject">デカールプロジェクターオブジェクト</param>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        public static void GetDecalMeshes(
            List<CyDecalMesh> results,
            GameObject projectorObject,
            GameObject receiverObject,
            Material decalMaterial)
        {
            if (Instance == null) return;
            Instance._decalMeshPool.GetDecalMeshes(
                results,
                receiverObject,
                decalMaterial);
        }

        /// <summary>
        ///     レシーバーオブジェクトのポリゴン情報がすでに存在しているか判定します。
        /// </summary>
        /// <param name="receiverObject"></param>
        /// <returns></returns>
        public static bool ExistTrianglePolygons(GameObject receiverObject)
        {
            return Instance._receiverObjectTrianglePolygonsPool.ExistConvexPolygons(receiverObject);
        }

        /// <summary>
        ///     レシーバーオブジェクトのポリゴン情報を登録します。
        /// </summary>
        /// <remarks>
        ///     事前条件: 本科数を呼び出す前に、ExistTrianglePolygons()を使用して未登録データであることを確認してください。
        /// </remarks>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <param name="meshFilters">レシーバーオブジェクトのメッシュフィルター</param>
        /// <param name="meshRenderers">レシーバーオブジェクトのメッシュレンダラー</param>
        /// <param name="skinnedMeshRenderers">レシーバーオブジェクトのスキンメッシュレンダラー</param>
        /// <returns></returns>
        public static IEnumerator RegisterTrianglePolygons(
            GameObject receiverObject,
            MeshFilter[] meshFilters,
            MeshRenderer[] meshRenderers,
            SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            if (Instance == null) yield break;
            yield return Instance._receiverObjectTrianglePolygonsPool.RegisterConvexPolygons(
                receiverObject,
                meshFilters,
                meshRenderers,
                skinnedMeshRenderers);
        }

        /// <summary>
        ///     デカールを貼り付けるレシーバーオブジェクトの三角形ポリゴン情報を取得。
        /// </summary>
        /// <remarks>
        ///     事前条件: RegisterTrianglePolygons()を呼び出してポリゴン情報が登録済みである必要があります。
        /// </remarks>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <returns></returns>
        public static List<ConvexPolygonInfo> GetTrianglePolygons(GameObject receiverObject)
        {
            if (Instance == null) return null;

            var convexPolygonInfos = Instance._receiverObjectTrianglePolygonsPool.ConvexPolygonsPool[receiverObject];
            foreach (var info in convexPolygonInfos) info.IsOutsideClipSpace = false;
            return convexPolygonInfos;
        }

        /// <summary>
        ///     レシーバーオブジェクトの三角形ポリゴンプールをクリア。
        /// </summary>
        public static void ClearReceiverObjectTrianglePolygonsPool()
        {
            if (Instance == null) return;
            Instance._receiverObjectTrianglePolygonsPool.Clear();
        }

        /// <summary>
        ///     デカールメッシュプールのサイズを取得。
        /// </summary>
        /// <returns></returns>
        public static int GetDecalMeshPoolSize()
        {
            if (Instance == null) return 0;
            return Instance._decalMeshPool.GetPoolSize();
        }

        /// <summary>
        ///     レシーバーオブジェクトの三角形ポリゴンスープのプールのサイズを取得。
        /// </summary>
        /// <returns></returns>
        public static int GetReceiverObjectTrianglePolygonsPoolSize()
        {
            if (Instance == null) return 0;
            return Instance._receiverObjectTrianglePolygonsPool.GetPoolSize();
        }
    }
}
