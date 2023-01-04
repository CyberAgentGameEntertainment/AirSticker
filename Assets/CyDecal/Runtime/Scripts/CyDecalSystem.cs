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
        private readonly ICyDecalMeshPool _decalMeshPool = new CyDecalMeshPool();
        private CyTrianglePolygonsFactory _trianglePolygonsFactory;

        // デカールプロジェクタのラウンチリクエストキュー
        private readonly ICyDecalProjectorLauncher _decalProjectorLauncher =
            new CyDecalProjectorLauncher();

        // デカールメッシュを貼り付けるレシーバーオブジェクトのプール。
        private readonly ICyReceiverObjectTrianglePolygonsPool _receiverObjectTrianglePolygonsPool =
            new CyReceiverObjectTrianglePolygonsPool();

        public static CyDecalProjectorLauncher DecalProjectorLauncher
        {
            get
            {
                if (!Instance) return null;
                return (CyDecalProjectorLauncher)Instance._decalProjectorLauncher;
            }
        }

        public static CyDecalMeshPool DecalMeshPool
        {
            get
            {
                if (!Instance) return null;
                return (CyDecalMeshPool)Instance._decalMeshPool;
            }
        }

        public static CyReceiverObjectTrianglePolygonsPool ReceiverObjectTrianglePolygonsPool
        {
            get
            {
                if (!Instance) return null;
                return (CyReceiverObjectTrianglePolygonsPool)Instance._receiverObjectTrianglePolygonsPool;
            }
        }

        private void Awake()
        {
            Debug.Assert(Instance == null,
                "CyDecalSystem can't be instantiated multiply, but but it has already been instantiated.");
            Instance = this;
            _trianglePolygonsFactory = new CyTrianglePolygonsFactory();
        }

        private void OnDestroy()
        {
            _decalMeshPool.Dispose();
            _trianglePolygonsFactory.Dispose();
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
        /// \三角形ポリゴンスープをレシーバーおジェクトから構築する
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
            yield return Instance._trianglePolygonsFactory.BuildFromReceiverObject(
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
            Instance._decalMeshPool.DisableDecalMeshRenderers();
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
            Instance._decalMeshPool.EnableDecalMeshRenderers();
        }
    }
}
