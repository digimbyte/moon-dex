#if GASKELLGAMES
using System;
using UnityEngine;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [Serializable]
    public class XRTracking
    {
        public float isTracked;
        public Vector3 position;
        public Quaternion rotation;
        
    } // class end
}

#endif