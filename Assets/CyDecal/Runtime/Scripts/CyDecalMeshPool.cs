using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// デカールメッシュのプール
    /// </summary>
    public class CyDecalMeshPool
    {
        private Dictionary<int, CyDecalMesh> _decalMeshes = new Dictionary<int, CyDecalMesh>();

        /// <summary>
        /// デカールメッシュを取得
        /// </summary>
        /// <remarks>
        /// デカールメッシュは貼り付けるターゲットオブジェクトとデカールマテリアルが同じ場合に共有されます。
        /// また、全く新規のターゲットオブジェクトとマテリアルであれば、
        /// 新規のデカールメッシュを作成します。
        /// </remarks>
        /// <param name="receiverObject">デカールを貼り付けるターゲットオブジェクト</param>
        /// <param name="decalMaterial">デカールマテリアル</param>
        /// <param name="isNew">新しくメッシュを生成した場合trueが設定される</param>
        /// <returns></returns>
        public CyDecalMesh GetDecalMesh( 
            GameObject receiverObject,
            Material decalMaterial,
            out bool isNew)
        {
            isNew = false;
            int hash = receiverObject.GetHashCode()
                       + decalMaterial.name.GetHashCode();
            if (_decalMeshes.ContainsKey(hash))
            {
                return _decalMeshes[hash];
            }
            var newMesh = new CyDecalMesh(decalMaterial);
            _decalMeshes.Add(hash, newMesh);
            isNew = true;
            return newMesh;
        }
    }
}
