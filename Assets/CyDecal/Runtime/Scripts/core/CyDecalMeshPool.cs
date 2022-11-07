using System.Collections.Generic;
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
        ///     デカールメッシュのリストを取得
        /// </summary>
        /// <remarks>
        ///     デカールメッシュは貼り付けるレシーバーオブジェクト、レンダラー、デカールマテリアルのハッシュ値の合計が同じ場合に共有されます。
        ///     また、未登録のハッシュ地の場合、新規のデカールメッシュを作成します。
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
                var hash =
                    $"{receiverObject.GetHashCode()}_{decalMaterial.name.GetHashCode()}_{renderer.GetHashCode()}".GetHashCode();
                if (_decalMeshes.ContainsKey(hash))
                {
                    decalMeshes.Add(_decalMeshes[hash]);
                }
                else
                {
                    var newMesh = new CyDecalMesh(projectorObject, decalMaterial, renderer);
                    decalMeshes.Add(newMesh);
                    _decalMeshes.Add(hash, newMesh);
                }
            }
        }
    }
}
