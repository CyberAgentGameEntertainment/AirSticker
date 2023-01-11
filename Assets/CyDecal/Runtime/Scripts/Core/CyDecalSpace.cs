using UnityEngine;

namespace CyDecal.Runtime.Scripts.Core
{
    /// <summary>
    /// Decal space.
    /// </summary>
    internal sealed class CyDecalSpace
    {
        public CyDecalSpace(Vector3 ex, Vector3 ey, Vector3 ez)
        {
            Ex = ex;
            Ey = ey;
            Ez = ez;
        }
        
        /// <summary>
        /// Axis X of decal space in world space. 
        /// </summary>
        public Vector3 Ex { get; }
        /// <summary>
        /// Axis Y of decal space in world space. 
        /// </summary>
        public Vector3 Ey { get; }
        /// <summary>
        /// Axis Z of decal space in world space. 
        /// </summary>
        public Vector3 Ez { get; } 
    }
}
