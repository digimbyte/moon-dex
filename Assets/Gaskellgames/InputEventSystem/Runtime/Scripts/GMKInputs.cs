#if GASKELLGAMES
using System;
using UnityEngine;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [Serializable]
    public class GMKInputs
    {
        [Tooltip("Default: Space / Button South")]
        public ButtonInputs south; // button 0
        
        [Tooltip("Default: L Ctrl / Button East")]
        public ButtonInputs east; // button 1
        
        [Tooltip("Default: L Alt / Button West")]
        public ButtonInputs west; // button 2
        
        [Tooltip("Default: C / Button North")]
        public ButtonInputs north; // button 3
        
        [Tooltip("Default: L Shift / Left Shoulder")]
        public ButtonInputs leftShoulder; // button 4
        
        [Tooltip("Default: F / Right Shoulder")]
        public ButtonInputs rightShoulder; // button 5
        
        [Tooltip("Default: P / Select")]
        public ButtonInputs select; // button 6
        
        [Tooltip("Default: Esc / Start")]
        public ButtonInputs start; // button 7
        
        [Tooltip("Default: Q / Left Stick Press")]
        public ButtonInputs leftStickPress; // button 8
        
        [Tooltip("Default: E / Right Stick Press")]
        public ButtonInputs rightStickPress; // button 9
        
        [Tooltip("Default: Middle Mouse / Touchpad Press")]
        public ButtonInputs touchpadPress; // button 10

        [Graph("x-axis", "y-axis")]
        [Tooltip("Default: W, A, S, D / Left Stick")]
        public Vector2 leftStick; // axis XY
        
        [Graph("4-axis", "5-axis")]
        [Tooltip("Default: Arrows / Right Stick")]
        public Vector2 rightStick; // axis 45
        
        [Graph("6-axis", "7-axis")]
        [Tooltip("Default: 1, 2, 3, 4 / D-Pad")]
        public Vector2 dPad; // axis 67
        
        [Range(0, 1)]
        [Tooltip("Default: R Mouse / L Trigger")]
        public float leftTrigger; // axis 9
        
        [Range(0, 1)]
        [Tooltip("Default: L Mouse / R Trigger")]
        public float rightTrigger; // axis 10

        /// <summary>
        /// Returns the clamped [-1, 1] value of <see cref="leftStick"/>
        /// </summary>
        public Vector2 LeftStickClamped => new Vector2(Mathf.Clamp(leftStick.x, -1, 1), Mathf.Clamp(leftStick.y, -1, 1));
        
        /// <summary>
        /// Returns the clamped [-1, 1] value of <see cref="rightStick"/>
        /// </summary>
        public Vector2 RightStickClamped => new Vector2(Mathf.Clamp(rightStick.x, -1, 1), Mathf.Clamp(rightStick.y, -1, 1));

    } // class end
}

    

#endif