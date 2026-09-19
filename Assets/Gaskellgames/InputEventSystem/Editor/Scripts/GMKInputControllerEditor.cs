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
    
    [CustomEditor(typeof(GMKInputController)), CanEditMultipleObjects]
    public class GMKInputControllerEditor : GgEditor
    {
        #region Serialized Properties / OnEnable

        private GMKInputController targetAsType;
        
        private SerializedProperty south;
        private SerializedProperty east;
        private SerializedProperty west;
        private SerializedProperty north;
        private SerializedProperty leftShoulder;
        private SerializedProperty rightShoulder;
        private SerializedProperty select;
        private SerializedProperty start;
        private SerializedProperty leftStickPress;
        private SerializedProperty rightStickPress;
        private SerializedProperty touchpadPress;
        private SerializedProperty leftStick;
        private SerializedProperty rightStick;
        private SerializedProperty dPad;
        private SerializedProperty leftTrigger;
        private SerializedProperty rightTrigger;
        
        private SerializedProperty onSouthStateChanged;
        private SerializedProperty onEastStateChanged;
        private SerializedProperty onWestStateChanged;
        private SerializedProperty onNorthStateChanged;
        private SerializedProperty onLeftShoulderStateChanged;
        private SerializedProperty onRightShoulderStateChanged;
        private SerializedProperty onStartStateChanged;
        private SerializedProperty onSelectStateChanged;
        private SerializedProperty onLeftStickPressStateChanged;
        private SerializedProperty onRightStickPressStateChanged;
        private SerializedProperty onTouchpadPressStateChanged;

        private SerializedProperty onLeftStickValueChanged;
        private SerializedProperty onRightStickValueChanged;
        private SerializedProperty onDPadValueChanged;
        private SerializedProperty onLeftTriggerValueChanged;
        private SerializedProperty onRightTriggerValueChanged;
        
        private SerializedProperty inputs;
        
        private const string packageRefName = "InputEventSystem";
        private Texture banner;

        private static int selectedTab = 0;
        private string[] tabs = new[] { "Settings", "Events", "Debug" };
        private int settingsTab = 0;
        private int eventsTab = 1;
        private int debugTab = 2;

        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            south = serializedObject.FindProperty("south");
            east = serializedObject.FindProperty("east");
            west = serializedObject.FindProperty("west");
            north = serializedObject.FindProperty("north");
            leftShoulder = serializedObject.FindProperty("leftShoulder");
            rightShoulder = serializedObject.FindProperty("rightShoulder");
            select = serializedObject.FindProperty("select");
            start = serializedObject.FindProperty("start");
            leftStickPress = serializedObject.FindProperty("leftStickPress");
            rightStickPress = serializedObject.FindProperty("rightStickPress");
            touchpadPress = serializedObject.FindProperty("touchpadPress");
            leftStick = serializedObject.FindProperty("leftStick");
            rightStick = serializedObject.FindProperty("rightStick");
            dPad = serializedObject.FindProperty("dPad");
            leftTrigger = serializedObject.FindProperty("leftTrigger");
            rightTrigger = serializedObject.FindProperty("rightTrigger");
            
            onSouthStateChanged = serializedObject.FindProperty("onSouthStateChanged");
            onEastStateChanged = serializedObject.FindProperty("onEastStateChanged");
            onWestStateChanged = serializedObject.FindProperty("onWestStateChanged");
            onNorthStateChanged = serializedObject.FindProperty("onNorthStateChanged");
            onLeftShoulderStateChanged = serializedObject.FindProperty("onLeftShoulderStateChanged");
            onRightShoulderStateChanged = serializedObject.FindProperty("onRightShoulderStateChanged");
            onStartStateChanged = serializedObject.FindProperty("onStartStateChanged");
            onSelectStateChanged = serializedObject.FindProperty("onSelectStateChanged");
            onLeftStickPressStateChanged = serializedObject.FindProperty("onLeftStickPressStateChanged");
            onRightStickPressStateChanged = serializedObject.FindProperty("onRightStickPressStateChanged");
            onTouchpadPressStateChanged = serializedObject.FindProperty("onTouchpadPressStateChanged");
            
            onLeftStickValueChanged = serializedObject.FindProperty("onLeftStickValueChanged");
            onRightStickValueChanged = serializedObject.FindProperty("onRightStickValueChanged");
            onDPadValueChanged = serializedObject.FindProperty("onDPadValueChanged");
            onLeftTriggerValueChanged = serializedObject.FindProperty("onLeftTriggerValueChanged");
            onRightTriggerValueChanged = serializedObject.FindProperty("onRightTriggerValueChanged");
            
            inputs = serializedObject.FindProperty("inputs");
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            if(!targetAsType) { targetAsType = target as GMKInputController; }
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(GMKInputController).NicifyName());

            // draw inspector
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();
            if (selectedTab == settingsTab)
            {
                EditorGUILayout.PropertyField(south);
                EditorGUILayout.PropertyField(east);
                EditorGUILayout.PropertyField(west);
                EditorGUILayout.PropertyField(north);
                EditorGUILayout.PropertyField(leftShoulder);
                EditorGUILayout.PropertyField(rightShoulder);
                EditorGUILayout.PropertyField(select);
                EditorGUILayout.PropertyField(start);
                EditorGUILayout.PropertyField(leftStickPress);
                EditorGUILayout.PropertyField(rightStickPress);
                EditorGUILayout.PropertyField(touchpadPress);
                EditorGUILayout.PropertyField(leftStick);
                EditorGUILayout.PropertyField(rightStick);
                EditorGUILayout.PropertyField(dPad);
                EditorGUILayout.PropertyField(leftTrigger);
                EditorGUILayout.PropertyField(rightTrigger);
            }
            else if (selectedTab == eventsTab)
            {
                EditorGUILayout.PropertyField(onSouthStateChanged);
                EditorGUILayout.PropertyField(onEastStateChanged);
                EditorGUILayout.PropertyField(onWestStateChanged);
                EditorGUILayout.PropertyField(onNorthStateChanged);
                EditorGUILayout.PropertyField(onLeftShoulderStateChanged);
                EditorGUILayout.PropertyField(onRightShoulderStateChanged);
                EditorGUILayout.PropertyField(onStartStateChanged);
                EditorGUILayout.PropertyField(onSelectStateChanged);
                EditorGUILayout.PropertyField(onLeftStickPressStateChanged);
                EditorGUILayout.PropertyField(onRightStickPressStateChanged);
                EditorGUILayout.PropertyField(onTouchpadPressStateChanged);
                
                EditorGUILayout.PropertyField(onLeftStickValueChanged);
                EditorGUILayout.PropertyField(onRightStickValueChanged);
                EditorGUILayout.PropertyField(onDPadValueChanged);
                EditorGUILayout.PropertyField(onLeftTriggerValueChanged);
                EditorGUILayout.PropertyField(onRightTriggerValueChanged);
            }
            else if (selectedTab == debugTab)
            {
                EditorGUILayout.PropertyField(inputs);
            }

            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

    } // class end
}

#endif
#endif