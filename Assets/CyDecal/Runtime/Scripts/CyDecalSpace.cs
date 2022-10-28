using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.XR;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace CyDecal.Runtime.Scripts
{
    /// <summary>
    /// デカール空間
    /// </summary>
     public class CyDecalSpace 
    {
        public Vector3 Ex { get; }  // デカール空間のX軸( ワールドスペース )
        public Vector3 Ey { get; }  // デカール空間のY軸( ワールドスペース )
        public Vector3 Ez { get; }  // デカール空間のZ軸( ワールドスペース )

        public CyDecalSpace (Vector3 ex, Vector3 ey, Vector3 ez)
        {
            Ex = ex;
            Ey = ey;
            Ez = ez;
        }
    }
}
