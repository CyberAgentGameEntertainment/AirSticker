using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    ///     デカール空間
    /// </summary>
    public class CyDecalSpace
    {
        public CyDecalSpace(Vector3 ex, Vector3 ey, Vector3 ez)
        {
            Ex = ex;
            Ey = ey;
            Ez = ez;
        }

        public Vector3 Ex { get; } // デカール空間のX軸( ワールドスペース )
        public Vector3 Ey { get; } // デカール空間のY軸( ワールドスペース )
        public Vector3 Ez { get; } // デカール空間のZ軸( ワールドスペース )
    }
}
