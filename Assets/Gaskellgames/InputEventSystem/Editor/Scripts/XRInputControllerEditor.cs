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
    
    [CustomEditor(typeof(XRInputController)), CanEditMultipleObjects]
    public class XRInputControllerEditor : GgEditor
    {
        #region Serialized Properties / OnEnable
        
        private SerializedProperty controllerType;
        private SerializedProperty trackingType;
        private SerializedProperty leftController;
        private SerializedProperty rightController;
        
        private SerializedProperty menuButtonL;
        private SerializedProperty primaryButtonL;
        private SerializedProperty secondaryButtonL;
        private SerializedProperty joystickButtonL;
        private SerializedProperty joystickAxisL;
        private SerializedProperty triggerAxisL;
        private SerializedProperty gripAxisL;

        private SerializedProperty primaryButtonTouchL;
        private SerializedProperty secondaryButtonTouchL;
        private SerializedProperty joystickButtonTouchL;
        private SerializedProperty triggerAxisTouchL;
        
        private SerializedProperty isTrackedL;
        private SerializedProperty positionL;
        private SerializedProperty rotationL;
        
        private SerializedProperty menuButtonR;
        private SerializedProperty primaryButtonR;
        private SerializedProperty secondaryButtonR;
        private SerializedProperty joystickButtonR;
        private SerializedProperty joystickAxisR;
        private SerializedProperty triggerAxisR;
        private SerializedProperty gripAxisR;

        private SerializedProperty primaryButtonTouchR;
        private SerializedProperty secondaryButtonTouchR;
        private SerializedProperty joystickButtonTouchR;
        private SerializedProperty triggerAxisTouchR;
        
        private SerializedProperty isTrackedR;
        private SerializedProperty positionR;
        private SerializedProperty rotationR;
        
        private SerializedProperty leftControllerInputs;
        private SerializedProperty leftControllerTracking;
        private SerializedProperty rightControllerInputs;
        private SerializedProperty rightControllerTracking;

        private static int selectedTab = 0;
        private string[] tabs = new[] { "Settings", "Debug" };
        private int settingsTab = 0;
        private int debugTab = 1;

        private const string packageRefName = "InputEventSystem";
        private Texture banner;

        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            controllerType = serializedObject.FindProperty("controllerType");
            trackingType = serializedObject.FindProperty("trackingType");
            leftController = serializedObject.FindProperty("leftController");
            rightController = serializedObject.FindProperty("rightController");
            
            menuButtonL = serializedObject.FindProperty("menuButtonL");
            primaryButtonL = serializedObject.FindProperty("primaryButtonL");
            secondaryButtonL = serializedObject.FindProperty("secondaryButtonL");
            joystickButtonL = serializedObject.FindProperty("joystickButtonL");
            joystickAxisL = serializedObject.FindProperty("joystickAxisL");
            triggerAxisL = serializedObject.FindProperty("triggerAxisL");
            gripAxisL = serializedObject.FindProperty("gripAxisL");
            
            primaryButtonTouchL = serializedObject.FindProperty("primaryButtonTouchL");
            secondaryButtonTouchL = serializedObject.FindProperty("secondaryButtonTouchL");
            joystickButtonTouchL = serializedObject.FindProperty("joystickButtonTouchL");
            triggerAxisTouchL = serializedObject.FindProperty("triggerAxisTouchL");
            
            isTrackedL = serializedObject.FindProperty("isTrackedL");
            positionL = serializedObject.FindProperty("positionL");
            rotationL = serializedObject.FindProperty("rotationL");
                
            menuButtonR = serializedObject.FindProperty("menuButtonR");
            primaryButtonR = serializedObject.FindProperty("primaryButtonR");
            secondaryButtonR = serializedObject.FindProperty("secondaryButtonR");
            joystickButtonR = serializedObject.FindProperty("joystickButtonR");
            joystickAxisR = serializedObject.FindProperty("joystickAxisR");
            triggerAxisR = serializedObject.FindProperty("triggerAxisR");
            gripAxisR = serializedObject.FindProperty("gripAxisR");
            
            primaryButtonTouchR = serializedObject.FindProperty("primaryButtonTouchR");
            secondaryButtonTouchR = serializedObject.FindProperty("secondaryButtonTouchR");
            joystickButtonTouchR = serializedObject.FindProperty("joystickButtonTouchR");
            triggerAxisTouchR = serializedObject.FindProperty("triggerAxisTouchR");
            
            isTrackedR = serializedObject.FindProperty("isTrackedR");
            positionR = serializedObject.FindProperty("positionR");
            rotationR = serializedObject.FindProperty("rotationR");
            
            leftControllerInputs = serializedObject.FindProperty("leftControllerInputs");
            leftControllerTracking = serializedObject.FindProperty("leftControllerTracking");
            rightControllerInputs = serializedObject.FindProperty("rightControllerInputs");
            rightControllerTracking = serializedObject.FindProperty("rightControllerTracking");
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnInspectorGUI
        
        public override void OnInspectorGUI()
        {
            // get & update references
            XRInputController xrInputController = (XRInputController)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(XRInputController).NicifyName());

            // draw inspector
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();
            if (selectedTab == settingsTab)
            {
                EditorGUILayout.PropertyField(controllerType);
                EditorGUILayout.PropertyField(trackingType);
                if (xrInputController.isLeftController) { EditorGUILayout.PropertyField(leftController); }
                if (xrInputController.isRightController) { EditorGUILayout.PropertyField(rightController); }
                if (xrInputController.isLeftController)
                {
                    EditorGUILayout.PropertyField(menuButtonL);
                    EditorGUILayout.PropertyField(primaryButtonL);
                    EditorGUILayout.PropertyField(secondaryButtonL);
                    EditorGUILayout.PropertyField(joystickButtonL);
                    EditorGUILayout.PropertyField(joystickAxisL);
                    EditorGUILayout.PropertyField(triggerAxisL);
                    EditorGUILayout.PropertyField(gripAxisL);
                    
                    EditorGUILayout.PropertyField(primaryButtonTouchL);
                    EditorGUILayout.PropertyField(secondaryButtonTouchL);
                    EditorGUILayout.PropertyField(joystickButtonTouchL);
                    EditorGUILayout.PropertyField(triggerAxisTouchL);
                    
                    EditorGUILayout.PropertyField(isTrackedL);
                    EditorGUILayout.PropertyField(positionL);
                    EditorGUILayout.PropertyField(rotationL);
                }
                if (xrInputController.isRightController)
                {
                    EditorGUILayout.PropertyField(menuButtonR);
                    EditorGUILayout.PropertyField(primaryButtonR);
                    EditorGUILayout.PropertyField(secondaryButtonR);
                    EditorGUILayout.PropertyField(joystickButtonR);
                    EditorGUILayout.PropertyField(joystickAxisR);
                    EditorGUILayout.PropertyField(triggerAxisR);
                    EditorGUILayout.PropertyField(gripAxisR);
                    
                    EditorGUILayout.PropertyField(primaryButtonTouchR);
                    EditorGUILayout.PropertyField(secondaryButtonTouchR);
                    EditorGUILayout.PropertyField(joystickButtonTouchR);
                    EditorGUILayout.PropertyField(triggerAxisTouchR);
                    
                    EditorGUILayout.PropertyField(isTrackedR);
                    EditorGUILayout.PropertyField(positionR);
                    EditorGUILayout.PropertyField(rotationR);
                }
            }
            else if (selectedTab == debugTab)
            {
                if (xrInputController.isLeftController)
                {
                    EditorGUILayout.PropertyField(leftControllerInputs);
                    EditorGUILayout.PropertyField(leftControllerTracking);
                }
                if (xrInputController.isRightController)
                {
                    EditorGUILayout.PropertyField(rightControllerInputs);
                    EditorGUILayout.PropertyField(rightControllerTracking);
                }
            }

            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

    } // class end
}

#endif
#endif