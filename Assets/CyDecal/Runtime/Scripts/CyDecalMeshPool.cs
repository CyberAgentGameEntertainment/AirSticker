using System.Collections.Generic;
using UnityEngine;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    ///     デカールメッシュのプール
    /// </summary>
    public class CyDecalMeshPool
    {
        private readonly Dictionary<int, CyDecalMesh> _decalMeshes = new();
        /// <summary>
        /// デカールメッシュの編集開始時に呼び出される関数。
        /// </summary>
        public void OnBeginEditDecalMesh()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.BeginEdit();
        }
        /// <summary>
        /// デカールメッシュの編集終了時に呼び出される関数。
        /// </summary>
        public void OnEndEditDecalMesh()
        {
            foreach (var decalMesh in _decalMeshes) decalMesh.Value.EndEdit();
        }

        /// <summary>
        ///     デカールメッシュを取得
        /// </summary>
        /// <remarks>
        ///     デカールメッシュは貼り付けるターゲットオブジェクトとデカールマテリアルが同じ場合に共有されます。
        ///     また、全く新規のターゲットオブジェクトとマテリアルであれば、
        ///     新規のデカールメッシュを作成します。
        /// </remarks>
        /// <param name="projectorObject">デカールプロジェクター</param>
        /// <param name="receiverObject">デカールを貼り付けるターゲットオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <returns></returns>
        public List<CyDecalMesh> GetDecalMeshes(
            GameObject projectorObject,
            GameObject receiverObject,
            Material decalMaterial)
        {
            var decalMeshes = new List<CyDecalMesh>();
            var renderers = receiverObject.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var hash = receiverObject.GetHashCode()
                           + decalMaterial.name.GetHashCode()
                           + renderer.GetHashCode();
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

            return decalMeshes;
        }
    }
}
