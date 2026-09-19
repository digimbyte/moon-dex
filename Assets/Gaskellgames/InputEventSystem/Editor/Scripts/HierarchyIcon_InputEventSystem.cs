#if UNITY_EDITOR
#if GASKELLGAMES
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.InputEventSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [InitializeOnLoad]
    public class HierarchyIcon_InputEventSystem
    {
        #region Variables

        private const string icon_CursorLockstate = "Icon_CursorLockstate";
        private const string icon_GamepadCursor = "Icon_GamepadCursor";
        private const string icon_GMKInputController = "Icon_GMKInputController";
        private const string icon_GMKVisualiser = "Icon_GMKVisualiser";
        private const string icon_InputEvent = "Icon_InputEvent";
        private const string icon_InputEventManager = "Icon_InputEventManager";
        private const string icon_SelectableObject = "Icon_SelectableObject";
        private const string icon_SelectionRect = "Icon_SelectionRect";
        private const string icon_InputSequencer = "Icon_InputSequencer";
        private const string icon_XRInputController = "Icon_XRInputController";

        private const string packageRefName = "InputEventSystem";
        private const string relativePath = "/Editor/Icons/";
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Editor Loop

        static HierarchyIcon_InputEventSystem()
        {
            HierarchyUtility.onCacheHierarchyIcons -= CacheHierarchyIcons;
            HierarchyUtility.onCacheHierarchyIcons += CacheHierarchyIcons;
        }

        private static void CacheHierarchyIcons()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            
            HierarchyUtility.TryAddHierarchyIcon(typeof(InputEventManager), icon_InputEventManager, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_InputEventManager}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(GamepadCursor), icon_GamepadCursor, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_GamepadCursor}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(CursorLockState), icon_CursorLockstate, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_CursorLockstate}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(GMKInputController), icon_GMKInputController, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_GMKInputController}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(GMKVisualiser), icon_GMKVisualiser, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_GMKVisualiser}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(XRInputController), icon_XRInputController, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_XRInputController}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(InputEvent), icon_InputEvent, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_InputEvent}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(InputSequencer), icon_InputSequencer, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_InputSequencer}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(SelectionRect), icon_SelectionRect, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_SelectionRect}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(SelectableObject), icon_SelectableObject, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_SelectableObject}.png", typeof(Texture)) as Texture);
        }

        #endregion
        
    } // class end
}

#endif
#endif