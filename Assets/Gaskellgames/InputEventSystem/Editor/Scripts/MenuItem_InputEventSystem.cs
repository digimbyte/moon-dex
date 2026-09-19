#if UNITY_EDITOR
#if GASKELLGAMES
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gaskellgames.InputEventSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class MenuItem_InputEventSystem : MenuItemUtility
    {
        #region GameObject Menu
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Input Event Manager", true, GgGameObjectMenu_Priority)]
        private static bool Gaskellgames_GameObjectMenu_InputEventManagerValidate()
        {
            return !(GameObject.FindAnyObjectByType<InputEventManager>());
        }
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Input Event Manager", false, GgGameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_InputEventManager(MenuCommand menuCommand)
        {
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "InputEventManager");
            
            // Add scripts & components
            InputEventManager inputEventManager = go.AddComponent<InputEventManager>();
            inputEventManager.SetupInputEventManager();
            
            // select newly created gameObject
            Selection.activeObject = go;
        }
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Gamepad Cursor", false, GgGameObjectMenu_Priority)]
        private static async void Gaskellgames_GameObjectMenu_GamepadCursor(MenuCommand menuCommand)
        {
            // add input action manager
            AddInputEventManager(menuCommand);
            
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "Canvas (Cursor)");
            GameObject child1 = AddChildItemInContext(go.transform, "GamepadCursor");
            GameObject child2 = AddChildItemInContext(child1.transform, "Cursor Offset");
            GameObject child3 = AddChildItemInContext(child2.transform, "Cursor Image");
            
            // Add scripts & components: parent
            go.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            
            // Add scripts & components: child 1
            child1.layer = LayerMask.NameToLayer("UI");
            RectTransform rectTransform = child1.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(50, 50);
            GamepadCursor gamepadCursor = child1.AddComponent<GamepadCursor>();
            
            // Add scripts & components: child 2
            child2.layer = child1.layer;
            RectTransform rectTransform2 = child2.AddComponent<RectTransform>();
            rectTransform2.sizeDelta = new Vector2(2, 2);
            
            // Add scripts & components: child 3
            child3.layer = child2.layer;
            rectTransform = child3.AddComponent<RectTransform>();
            rectTransform.anchoredPosition3D = new Vector3(12, -12, 0);
            rectTransform.sizeDelta = new Vector2(25, 25);
            child3.AddComponent<CanvasRenderer>();
            Image image = child3.AddComponent<Image>();
            image.raycastTarget = false;

            // assign values
            if (await GgTask.WaitUntilNextFrame() != TaskResultType.Complete) { return; }
            gamepadCursor.CursorCanvas = canvas;
            gamepadCursor.CursorOffset = rectTransform2;
            gamepadCursor.CursorImage = image;
            
            // select newly created gameObject
            Selection.activeObject = child1;
        }
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Input Event", false, GgGameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_InputEvent(MenuCommand menuCommand)
        {
            // add input action manager
            AddInputEventManager(menuCommand);
            
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "InputEvent");
            
            // Add scripts & components
            go.AddComponent<InputEvent>();
            
            // select newly created gameObject
            Selection.activeObject = go;
        }
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Input Sequencer", false, GgGameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_InputSequencer(MenuCommand menuCommand)
        {
            // add input action manager
            AddInputEventManager(menuCommand);
            
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "InputSequencer");
            
            // Add scripts & components
            go.AddComponent<InputSequencer>();
            
            // select newly created gameObject
            Selection.activeObject = go;
        }
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Input Controller (Gamepad, Mouse, Keyboard)", false, GgGameObjectMenu_Priority)]
        private static GMKInputController Gaskellgames_GameObjectMenu_GMKInputController(MenuCommand menuCommand)
        {
            // add input action manager
            AddInputEventManager(menuCommand);
            
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "InputController (GMK)");
            
            // Add scripts & components
            GMKInputController inputController = go.AddComponent<GMKInputController>();

            // select newly created gameObject
            Selection.activeObject = go;
            
            return inputController;
        }
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Input Controller (Extended Reality)", false, GgGameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_XRInputController(MenuCommand menuCommand)
        {
            // add input action manager
            AddInputEventManager(menuCommand);
            
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "InputController (XR)");
            
            // Add scripts & components
            go.AddComponent<XRInputController>();
            
            // select newly created gameObject
            Selection.activeObject = go;
        }
        
        [MenuItem(InputEventSystem_GameObjectMenu_Path + "/Cursor Lock State", false, GgGameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_CursorLockState(MenuCommand menuCommand)
        {
            // add input action manager
            AddInputEventManager(menuCommand);
            
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "CursorLockState");
            
            // Add scripts & components
            go.AddComponent<CursorLockState>();
            
            // select newly created gameObject
            Selection.activeObject = go;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Public Methods

        public static void AddInputEventManager(MenuCommand menuCommand)
        {
            if (Gaskellgames_GameObjectMenu_InputEventManagerValidate())
            {
                Gaskellgames_GameObjectMenu_InputEventManager(menuCommand);
            }
        }

        public static GMKInputController AddPlayerInputController(MenuCommand menuCommand)
        {
            return Gaskellgames_GameObjectMenu_GMKInputController(menuCommand);
        }

        #endregion
        
    } // class end
}

#endif
#endif