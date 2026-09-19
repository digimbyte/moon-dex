#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Gaskellgames.EditorOnly;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomEditor(typeof(SelectionRect)), CanEditMultipleObjects]
    public class SelectionRectEditor : GgEditor
    {
    	#region Serialized Properties / OnEnable
    
        private SelectionRect targetAsType;
        
        private SerializedProperty rectTransform;
        private SerializedProperty selectionInputReference;
        private SerializedProperty mousePositionReference;
        
        private SerializedProperty onSelectionStart;
        private SerializedProperty onSelectionEnd;
    
        private static int selectedTab = 0;
        private string[] tabs = new[] { "Settings", "Events" };
        private int settingsTab = 0;
        private int eventsTab = 1;
    
        private Texture banner;

        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
        
            rectTransform = serializedObject.FindProperty("rectTransform");
            selectionInputReference = serializedObject.FindProperty("selectionInputReference");
            mousePositionReference = serializedObject.FindProperty("mousePositionReference");
            onSelectionStart = serializedObject.FindProperty("onSelectionStart");
            onSelectionEnd = serializedObject.FindProperty("onSelectionEnd");
        }

        #endregion

        //----------------------------------------------------------------------------------------------------
    
        #region OnInspectorGUI
    
        public override void OnInspectorGUI()
        {
            // get & update references
            if (!targetAsType) { targetAsType = target as SelectionRect; }
            serializedObject.Update();
    
            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(SelectionRect).NicifyName());
    
            // custom inspector
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();
            if (selectedTab == settingsTab)
            {
                EditorGUILayout.PropertyField(rectTransform);
                EditorGUILayout.PropertyField(selectionInputReference);
                EditorGUILayout.PropertyField(mousePositionReference);
            }
            else if (selectedTab == eventsTab)
            {
                EditorGUILayout.PropertyField(onSelectionStart);
                EditorGUILayout.PropertyField(onSelectionEnd);
            }
    
            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }
    
        #endregion
    
    } // class end
}
#endif