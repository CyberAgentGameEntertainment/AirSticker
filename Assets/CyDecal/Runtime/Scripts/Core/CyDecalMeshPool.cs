using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    internal interface ICyDecalMeshPool
    {
        int GetPoolSize();
        void DisableDecalMeshRenderers();
        void EnableDecalMeshRenderers();
        bool Contains(int hash);
        void RegisterDecalMesh(int hash, CyDecalMesh decalMesh);
        CyDecalMesh GetDecalMesh(int hash);
        void Dispose();
        void GarbageCollect();
    }
    /// <summary>
    ///     デカールメッシュのプール
    /// </summary>
    public sealed class CyDecalMeshPool : ICyDecalMeshPool
    {
        private readonly Dictionary<int, CyDecalMesh> _decalMeshes = new Dictionary<int, CyDecalMesh>();

        /// <summary>
        ///     プールのサイズを取得。
        /// </summary>
        /// <returns></returns>
        public int GetPoolSize()
        {
            return _decalMeshes.Count;
        }

        /// <summary>
        ///     デカールメッシュのレンダラーを無効にする。
        /// </summary>
        void ICyDecalMeshPool.DisableDecalMeshRenderers()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.DisableDecalMeshRenderer();
        }

        /// <summary>
        ///     デカールメッシュのレンダラーを有効にする。
        /// </summary>
        void ICyDecalMeshPool.EnableDecalMeshRenderers()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.EnableDecalMeshRenderer();
        }

        /// <summary>
        ///     プールに登録されるハッシュ値を計算
        /// </summary>
        /// <param name="receiverObject">デカールを貼り付けるターゲットオブジェクト</param>
        /// <param name="renderer">レンダラー</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <returns>ハッシュ値</returns>
        public static int CalculateHash(GameObject receiverObject, Renderer renderer, Material decalMaterial)
        {
            return receiverObject.GetInstanceID()
                   + decalMaterial.name.GetHashCode()
                   + renderer.GetInstanceID();
        }

        /// <summary>
        ///     指定したハッシュ値にデカールメッシュがプールに記録されているか判定。
        /// </summary>
        /// <param name="hash"> ここに渡すハッシュ値はCalculateHashを利用して計算してください。 </param>
        /// <returns>プールに含まれている場合はtrueを返します。</returns>
        bool ICyDecalMeshPool.Contains(int hash)
        {
            return _decalMeshes.ContainsKey(hash);
        }

        /// <summary>
        ///     デカールメッシュを登録。
        /// </summary>
        /// <param name="hash">ハッシュ値</param>
        /// <param name="decalMesh">デカールメッシュ</param>
        void ICyDecalMeshPool.RegisterDecalMesh(int hash, CyDecalMesh decalMesh)
        {
            _decalMeshes.Add(hash, decalMesh);
        }

        /// <summary>
        ///     デカールメッシュを取得
        /// </summary>
        CyDecalMesh ICyDecalMeshPool.GetDecalMesh(int hash)
        {
            return _decalMeshes[hash];
        }

        void ICyDecalMeshPool.Dispose()
        {
            foreach (var item in _decalMeshes)
            {
                item.Value?.Dispose();
            }
        }
        /// <summary>
        ///     プールをガベージコレクト。
        /// </summary>
        void ICyDecalMeshPool.GarbageCollect()
        {
            // 削除可能リストを作成。
            var removeList = _decalMeshes.Where(item => item.Value.CanRemoveFromPool()).ToList();
            foreach (var item in removeList)
            {
                item.Value.Dispose();
                _decalMeshes.Remove(item.Key);
            }
        }
    }
}
