using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカールメッシュのプール
    /// </summary>
    internal sealed class CyDecalMeshPool
    {
        private readonly Dictionary<int, CyDecalMesh> _decalMeshes = new Dictionary<int, CyDecalMesh>();

        /// <summary>
        ///     プールをクリア
        /// </summary>
        public void Clear()
        {
            _decalMeshes.Clear();
        }

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
        public void DisableDecalMeshRenderers()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.DisableDecalMeshRenderer();
        }

        /// <summary>
        ///     デカールメッシュのレンダラーを有効にする。
        /// </summary>
        public void EnableDecalMeshRenderers()
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
        public bool Contains(int hash)
        {
            return _decalMeshes.ContainsKey(hash);
        }

        /// <summary>
        ///     デカールメッシュを登録。
        /// </summary>
        /// <param name="hash">ハッシュ値</param>
        /// <param name="decalMesh">デカールメッシュ</param>
        public void RegisterDecalMesh(int hash, CyDecalMesh decalMesh)
        {
            _decalMeshes.Add(hash, decalMesh);
        }

        /// <summary>
        ///     デカールメッシュを取得
        /// </summary>
        public CyDecalMesh GetDecalMesh(int hash)
        {
            return _decalMeshes[hash];
        }

        /// <summary>
        ///     プールをガベージコレクト。
        /// </summary>
        public void GarbageCollect()
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
