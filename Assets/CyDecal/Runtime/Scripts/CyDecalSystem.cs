using System;
using System.Collections;
using System.Collections.Generic;
using CyDecal.Runtime.Scripts.Core;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールシステム(Facadeパターン)
    /// </summary>
    public sealed class CyDecalSystem : MonoBehaviour
    {
        // デカールメッシュプール。
        private readonly CyDecalMeshPool _decalMeshPool = new CyDecalMeshPool();

        // デカールプロジェクタのラウンチリクエストキュー
        private readonly CyDecalProjectorLauncher _decalProjectorLauncher =
            new CyDecalProjectorLauncher();

        // デカールメッシュを貼り付けるレシーバーオブジェクトのプール。
        private readonly CyReceiverObjectTrianglePolygonsPool _receiverObjectTrianglePolygonsPool =
            new CyReceiverObjectTrianglePolygonsPool();
        
        private void Awake()
        {
            Debug.Assert(Instance == null, "CyDecalSystem can't be instantiated multiply, but but it has already been instantiated.");
            
            Instance = this;
        }

        private void OnDestroy()
        {
            _decalMeshPool.Dispose();
            Instance = null;
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
            _decalProjectorLauncher.Update();
        }

        /// <summary>
        ///     待ち行列にキューイングされているデカールプロジェクタのローンチリクエストの数を取得する。
        /// </summary>
        /// <returns></returns>
        public static int GetWaitingLaunchedProjectorCount()
        {
            return Instance._decalProjectorLauncher.GetRequestCount();
        }

        /// <summary>
        ///     デカールプロジェクターのローンチリクエストを行う。
        /// </summary>
        public static void RequestLaunching(CyDecalProjector projector, Action onLaunch)
        {
            Instance._decalProjectorLauncher.EnqueueLaunchRequest(projector, onLaunch);
        }

        /// <summary>
        ///     デカールメッシュレンダラーを無効にします。
        /// </summary>
        private static void DisableDecalMeshRenderers()
        {
            if (Instance == null) return;
            Instance._decalMeshPool.DisableDecalMeshRenderers();
        }

        /// <summary>
        ///     デカールメッシュレンダラーを有効にします。
        /// </summary>
        private static void EnableDecalMeshRenderers()
        {
            if (Instance == null) return;
            Instance._decalMeshPool.EnableDecalMeshRenderers();
        }

        /// <summary>
        ///     レシーバーオブジェクトのポリゴン情報がすでに存在しているか判定します。
        /// </summary>
        /// <param name="receiverObject"></param>
        /// <returns></returns>
        public static bool ContainsTrianglePolygonsInPool(GameObject receiverObject)
        {
            return Instance._receiverObjectTrianglePolygonsPool.Contains(receiverObject);
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
        internal static void RegisterTrianglePolygonsToPool(GameObject receiverObject, List<ConvexPolygonInfo> convexPolygonInfos)
        {
            if (Instance == null) return;
            Instance._receiverObjectTrianglePolygonsPool.RegisterConvexPolygons(receiverObject, convexPolygonInfos);
        }

        /// <summary>
        /// 三角形ポリゴンスープをレシーバーおジェクトから構築する
        /// </summary>
        /// <param name="meshFilters"></param>
        /// <param name="meshRenderers"></param>
        /// <param name="skinnedMeshRenderers"></param>
        /// <param name="convexPolygonInfos"></param>
        /// <returns></returns>
        internal static IEnumerator BuildTrianglePolygonsFromReceiverObject(
            MeshFilter[] meshFilters,
            MeshRenderer[] meshRenderers,
            SkinnedMeshRenderer[] skinnedMeshRenderers,
            List<ConvexPolygonInfo> convexPolygonInfos)
        {
            yield return CyTrianglePolygonsFactory.BuildFromReceiverObject(
                meshFilters,
                meshRenderers,
                skinnedMeshRenderers,
                convexPolygonInfos);
        }
        /// <summary>
        ///     デカールを貼り付けるレシーバーオブジェクトの三角形ポリゴン情報を取得。
        /// </summary>
        /// <remarks>
        ///     事前条件: RegisterTrianglePolygons()を呼び出してポリゴン情報が登録済みである必要があります。
        /// </remarks>
        /// <param name="receiverObject">レシーバーオブジェクト</param>
        /// <returns></returns>
        internal static List<ConvexPolygonInfo> GetTrianglePolygonsFromPool(GameObject receiverObject)
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
        /// <summary>
        /// 編集するデカールメッシュを収集する。
        /// </summary>
        internal static void CollectEditDecalMeshes(
            List<CyDecalMesh> results,
            GameObject receiverObject,
            Material decalMaterial)
        {
            // レシーバーオブジェクトのレンダラーのみを収集したいのだが、
            // レシーバーオブジェクトにデカールメッシュのレンダラーがぶら下がっているので
            // 一旦無効にする。
            DisableDecalMeshRenderers();
            // 編集するデカールメッシュを取得する。
            var renderers = receiverObject.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!renderer) return;
                var pool = Instance._decalMeshPool;
                var hash = CyDecalMeshPool.CalculateHash(receiverObject, renderer, decalMaterial);
                if (pool.Contains(hash))
                {
                    results.Add(pool.GetDecalMesh(hash));
                }
                else
                {
                    var newMesh = new CyDecalMesh(receiverObject, decalMaterial, renderer);
                    results.Add(newMesh);
                    pool.RegisterDecalMesh(hash, newMesh);
                }
            }

            // 無効にしたレンダラーを戻す。
            EnableDecalMeshRenderers();
        }
    }
}
