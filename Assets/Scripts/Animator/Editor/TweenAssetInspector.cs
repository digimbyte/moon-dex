
#if UNITY_EDITOR
using Animator.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Animator.Editor
{
    [CustomEditor(typeof(TweenAsset))]
    public sealed class TweenAssetInspector : UnityEditor.Editor
    {
        private ReorderableList _tracks;

        private void OnEnable()
        {
            _tracks = new ReorderableList(serializedObject, serializedObject.FindProperty("tracks"), true, true, true, true);
            _tracks.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Tracks");
            };
            _tracks.elementHeightCallback = index =>
            {
                var el = _tracks.serializedProperty.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(el, true) + 6;
            };
            _tracks.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = _tracks.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2;
                rect.height -= 4;
                EditorGUI.PropertyField(rect, el, new GUIContent($"Track {index}"), true);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Author tracks/clips here. Scene references must be provided via TweenBindings at runtime.", MessageType.Info);

            _tracks.DoLayoutList();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Recompute Length"))
                {
                    foreach (var t in targets)
                    {
                        var a = (TweenAsset)t;
                        a.RecomputeLength();
                        EditorUtility.SetDirty(a);
                    }
                }

                if (GUILayout.Button("Open Tween Board Window"))
                {
                    TweenBoardWindow.ShowWindow();
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("lengthSeconds"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
