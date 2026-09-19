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
    
    [CustomEditor(typeof(InputSequencer)), CanEditMultipleObjects]
    public class InputSequencerEditor : GgEditor
    {
        #region Serialized Properties / OnEnable

        private InputSequencer targetAsType;
        
        private SerializedProperty anyInput;
        private SerializedProperty inputSequence;
        private SerializedProperty onSequenceProgressed;
        private SerializedProperty onSequenceComplete;
        private SerializedProperty onSequenceReset;
        private SerializedProperty index;
        private SerializedProperty sequenceComplete;
        
        private static int selectedTab = 0;
        private string[] tabs = new[] { "Settings", "Events", "Debug" };
        private int settingsTab = 0;
        private int eventsTab = 1;
        private int debugTab = 2;
        
        private const string packageRefName = "InputEventSystem";
        private Texture banner;

        private void OnEnable()
        {
            if (!targetAsType) { targetAsType = target as InputSequencer; }
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            anyInput = serializedObject.FindProperty("anyInput");
            inputSequence = serializedObject.FindProperty("inputSequence");
            onSequenceProgressed = serializedObject.FindProperty("onSequenceProgressed");
            onSequenceComplete = serializedObject.FindProperty("onSequenceComplete");
            onSequenceReset = serializedObject.FindProperty("onSequenceReset");
            index = serializedObject.FindProperty("index");
            sequenceComplete = serializedObject.FindProperty("sequenceComplete");
        }

        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(InputSequencer).NicifyName());
            
            // draw inspector
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();
            if (selectedTab == settingsTab)
            {
                EditorGUILayout.PropertyField(anyInput);
                EditorGUILayout.PropertyField(inputSequence);
            }
            else if (selectedTab == eventsTab)
            {
                EditorGUILayout.PropertyField(onSequenceProgressed);
                EditorGUILayout.PropertyField(onSequenceComplete);
                EditorGUILayout.PropertyField(onSequenceReset);
            }
            else if (selectedTab == debugTab)
            {
                EditorGUILayout.PropertyField(index);
                EditorGUILayout.PropertyField(sequenceComplete);
            }

            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        
    } // class end
}

#endif
#endif