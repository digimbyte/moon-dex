
#if UNITY_EDITOR
using Animator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Animator.Editor
{
    [CustomEditor(typeof(TweenDirector))]
    public sealed class TweenDirectorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("asset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bindings"));

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnStart"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timeScale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("verboseWarnings"));

            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(Application.isPlaying == false))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play (Reset)"))
                    {
                        ((TweenDirector)target).Play(resetTime: true);
                    }
                    if (GUILayout.Button("Play (No Reset)"))
                    {
                        ((TweenDirector)target).Play(resetTime: false);
                    }
                    if (GUILayout.Button("Stop"))
                    {
                        ((TweenDirector)target).Stop();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Compile"))
                    {
                        ((TweenDirector)target).Compile();
                    }
                    if (GUILayout.Button("Seek 0"))
                    {
                        ((TweenDirector)target).Seek(0f);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
