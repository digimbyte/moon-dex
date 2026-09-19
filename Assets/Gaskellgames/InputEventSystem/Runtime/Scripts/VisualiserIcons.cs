#if GASKELLGAMES
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [Serializable]
    public class VisualiserIcons
    {
        [SerializeField, Required]
        [Tooltip("The UI image for this VisualiserIcon.")]
        public Image image;
        
        [SerializeField, Required]
        [Tooltip("The UI sprite for this VisualiserIcon, when in it's default state.")]
        public Sprite normal;
        
        [SerializeField, Required]
        [Tooltip("The UI sprite for this VisualiserIcon, when in it's active state.")]
        public Sprite active;

        public void SetActive(bool isActive)
        {
            if (!image || !active || !normal) { return; }
            image.sprite = isActive ? active : normal;
        }
        
    } // class end
}
#endif