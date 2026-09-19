#if UNITY_EDITOR
#if GASKELLGAMES
using System.Collections.Generic;
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.InputEventSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomEditor(typeof(InputEventManager)), CanEditMultipleObjects]
    public class InputEventManagerEditor : GgEditor
    {
        #region Serialized Properties / OnEnable

        private SerializedProperty verboseLogs;
        private SerializedProperty gizmosOnSelected;

        // input action
        private SerializedProperty inputActionAssets;
        
        // input icon
        private SerializedProperty onDeviceAdded;
        private SerializedProperty onDeviceRemoved;
        private SerializedProperty onControlsChanged;
        private SerializedProperty activeDevice;
        private SerializedProperty activeControlScheme;
        private SerializedProperty deviceList;
        private SerializedProperty controlSchemes;
        
        private static int selectedTab = 0;
        private string[] tabs = new[] { "Settings", "Events", "Debug" };
        private int settingsTab = 0;
        private int eventsTab = 1;
        private int debugTab = 2;
        
        private const string packageRefName = "InputEventSystem";
        private Texture banner;

        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            verboseLogs = serializedObject.FindProperty("verboseLogs");
            gizmosOnSelected = serializedObject.FindProperty("gizmosOnSelected");
            
            inputActionAssets = serializedObject.FindProperty("inputActionAssets");
            onDeviceAdded = serializedObject.FindProperty("onDeviceAdded");
            onDeviceRemoved = serializedObject.FindProperty("onDeviceRemoved");
            onControlsChanged = serializedObject.FindProperty("onControlsChanged");
            activeDevice = serializedObject.FindProperty("activeDevice");
            activeControlScheme = serializedObject.FindProperty("activeControlScheme");
            deviceList = serializedObject.FindProperty("deviceList");
            controlSchemes = serializedObject.FindProperty("controlSchemes");
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            InputEventManager inputEventManager = (InputEventManager)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(InputEventManager).NicifyName());

            // draw inspector
            bool defaultGui = GUI.enabled;
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();
            if (selectedTab == settingsTab)
            {
                EditorGUILayout.PropertyField(inputActionAssets);
            }
            else if (selectedTab == eventsTab)
            {
                EditorGUILayout.PropertyField(onDeviceAdded);
                EditorGUILayout.PropertyField(onDeviceRemoved);
                EditorGUILayout.PropertyField(onControlsChanged);
            }
            else if (selectedTab == debugTab)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(verboseLogs);
                EditorGUILayout.PropertyField(gizmosOnSelected);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(activeDevice);
                EditorGUILayout.PropertyField(activeControlScheme);

                GUI.enabled = false;
                DrawListAsTags(deviceList);
                DrawListAsTags(controlSchemes);
                GUI.enabled = defaultGui;
            }

            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Private Methods

        private void DrawListAsTags(SerializedProperty property)
        {
            if (!property.isArray) { return; }
            
            // get string list
            List<string> stringList = new List<string>();
            for (int i = 0; i < property.arraySize; i++)
            {
                stringList.Add(property.GetArrayElementAtIndex(i).stringValue);
            }
            
            // get label
            GUIContent label = new GUIContent(property.name.NicifyName(), property.tooltip);

            bool isExpanded = property.isExpanded;
            DrawListAsTags(stringList, label, ref isExpanded);
            property.isExpanded = isExpanded;
        }
        
        private void DrawListAsTags(List<string> stringList, GUIContent label, ref bool isExpanded)
        {
            isExpanded = EditorExtensions.BeginFoldoutGroupNestable(label, isExpanded);
            if (isExpanded)
            {
                // draw list
                int gap = 2;
                int indentOffset = 28;
                if (0 < stringList.Count)
                {
                    float labelLineWidth = 0;
                    EditorGUILayout.BeginHorizontal();
                    Color defaultBackground = GUI.backgroundColor;
                    GUI.backgroundColor = new Color32(179, 179, 179, 255);
                    foreach (string item in stringList)
                    {
                        float labelWidth = StringExtensions.GetStringWidth(item) + (gap * 2f);
                        labelLineWidth += labelWidth + gap;
                        if (Screen.width <= labelLineWidth + gap + indentOffset)
                        {
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            labelLineWidth = labelWidth + gap;
                        }
                        GUILayout.Button(item, GUILayout.Width(labelWidth));
                    }
                    GUILayout.FlexibleSpace();
                    GUI.backgroundColor = defaultBackground;
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("n/a");
                }
            }
            EditorExtensions.EndFoldoutGroupNestable();
        }

        #endregion

    } // class end
}

#endif
#endif