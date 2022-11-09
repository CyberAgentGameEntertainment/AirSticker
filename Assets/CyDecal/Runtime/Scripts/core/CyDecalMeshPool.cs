using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカールメッシュのプール
    /// </summary>
    public class CyDecalMeshPool
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
        ///     デカールメッシュを取得
        /// </summary>
        /// <remarks>
        ///     デカールメッシュは貼り付けるターゲットオブジェクトとデカールマテリアルが同じ場合に共有されます。
        ///     また、全く新規のターゲットオブジェクトとマテリアルであれば、
        ///     新規のデカールメッシュを作成します。
        /// </remarks>
        /// <param name="decalMeshes">デカールメッシュの格納先</param>
        /// <param name="projectorObject">デカールプロジェクター</param>
        /// <param name="receiverObject">デカールを貼り付けるターゲットオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <returns></returns>
        public void GetDecalMeshes(
            List<CyDecalMesh> decalMeshes,
            GameObject projectorObject,
            GameObject receiverObject,
            Material decalMaterial)
        {
            var renderers = receiverObject.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!renderer) return;

                var hash = receiverObject.GetInstanceID()
                           + decalMaterial.name.GetHashCode()
                           + renderer.GetInstanceID();
                if (_decalMeshes.ContainsKey(hash))
                {
                    decalMeshes.Add(_decalMeshes[hash]);
                }
                else
                {
                    var newMesh = new CyDecalMesh(receiverObject, decalMaterial, renderer);
                    decalMeshes.Add(newMesh);
                    _decalMeshes.Add(hash, newMesh);
                }
            }
        }

        public void GarbageCollect()
        {
            // 削除可能リストを作成。
            var removeList = _decalMeshes.Where(item => item.Value.IsPossibleRemovePool()).ToList();
            foreach (var item in removeList)
            {
                item.Value.Destroy();
                _decalMeshes.Remove(item.Key);
            }
        }
    }
}
