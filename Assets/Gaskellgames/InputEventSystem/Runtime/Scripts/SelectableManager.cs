using System.Collections.Generic;
using UnityEngine;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class SelectableManager
    {
        #region Variables

        [Tooltip("All currently registered selectable objects.")]
        private static List<SelectableObject> registeredObjects;

        #endregion
        
    	//----------------------------------------------------------------------------------------------------
        
        #region Getter/Setter
        
        /// <summary>
        /// All currently registered selectable objects.
        /// </summary>
        public static List<SelectableObject> RegisteredObjects
        {
            get
            {
                registeredObjects ??= new List<SelectableObject>();
                return registeredObjects;
            }
        }

        #endregion
        
    	//----------------------------------------------------------------------------------------------------
        
        #region Static Methods
        
        /// <summary>
        /// Register a SelectableObject for use in SelectionRect logic.
        /// </summary>
        /// <param name="selectableObject"></param>
        /// <returns></returns>
        public static bool TryRegisterSelectableObject(SelectableObject selectableObject)
        {
            if (selectableObject == null) { return false; }
            registeredObjects ??= new List<SelectableObject>();
            if (registeredObjects.Contains(selectableObject)) { return false; }
            return registeredObjects.TryAdd(selectableObject);
        }
        
        /// <summary>
        /// Unregister a SelectableObject from use in SelectionRect logic.
        /// </summary>
        /// <param name="selectableObject"></param>
        /// <returns></returns>
        public static bool TryUnregisterSelectableObject(SelectableObject selectableObject)
        {
            if (selectableObject == null) { return false; }
            registeredObjects ??= new List<SelectableObject>();
            if (!registeredObjects.Contains(selectableObject)) { return false; }
            return registeredObjects.Remove(selectableObject);
        }
        
        #endregion
        
    } // class end
}