using UnityEngine;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class SelectableObject : GgMonoBehaviour
    {
        #region Variables
        
        [SerializeField, ReadOnly]
        [Tooltip("Flag showing whether this selectable object is currently selected.")]
        private bool isSelected;
        
        /// <summary>
        /// Called when this object is selected.
        /// </summary>
        public GgEvent onSelected;
        
        /// <summary>
        /// Called when this object is deselected.
        /// </summary>
        public GgEvent onDeselected;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Getter / Setter
        
        /// <summary>
        /// Set whether the object is selected or not.
        /// </summary>
        public bool IsSelected
        {
            get => enabled && isSelected;
            set
            {
                bool newValue = enabled && value;
                bool previousValue = isSelected;
                isSelected = newValue;
                
                if (newValue && !previousValue)
                {
                    onSelected?.Invoke();
                }
                else if (!newValue && previousValue)
                {
                    onDeselected?.Invoke();
                }
            }
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Game Loop
        
        private void OnEnable()
        {
            if (!SelectableManager.TryRegisterSelectableObject(this))
            {
                Log(GgLogType.Error, "Failed to register SelectableObject {0}", name);
            }
        }
        
        private void OnDisable()
        {
            IsSelected = false;
            if (!SelectableManager.TryUnregisterSelectableObject(this))
            {
                Log(GgLogType.Error, "Failed to unregister SelectableObject {0}", name);
            }
        }
        
        #endregion
        
    } // class end
}