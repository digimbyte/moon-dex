#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Gaskellgames.EditorOnly;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomEditor(typeof(SelectableObject)), CanEditMultipleObjects]
    public class SelectableObjectEditor : GgEditor
    {
    	#region Serialized Properties / OnEnable
    
        private SelectableObject targetAsType;

        private SerializedProperty isSelected;
        private SerializedProperty onSelected;
        private SerializedProperty onDeselected;
    
        private static int selectedTab = 0;
        private string[] tabs = new[] { "Events", "Debug" };
        private int eventsTab = 0;
        private int debugTab = 1;
    
        private Texture banner;

        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
        
            isSelected = serializedObject.FindProperty("isSelected");
            onSelected = serializedObject.FindProperty("onSelected");
            onDeselected = serializedObject.FindProperty("onDeselected");
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnInspectorGUI
    
        public override void OnInspectorGUI()
        {
            // get & update references
            if (!targetAsType) { targetAsType = target as SelectableObject; }
            serializedObject.Update();
    
            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(SelectableObject).NicifyName());
    
            // custom inspector
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();
            if (selectedTab == eventsTab)
            {
                EditorGUILayout.PropertyField(onSelected);
                EditorGUILayout.PropertyField(onDeselected);
            }
            else if (selectedTab == debugTab)
            {
                EditorGUILayout.PropertyField(isSelected);
            }
    
            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }
    
        #endregion
    
    } // class end
}
#endif