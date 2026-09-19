
#if UNITY_EDITOR
using Animator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Animator.Editor
{
    public sealed class TweenBoardWindow : EditorWindow
    {
        private TweenAsset _asset;
        private Vector2 _scroll;

        [MenuItem("Tools/Tween Board/Board Window")]
        public static void ShowWindow()
        {
            var w = GetWindow<TweenBoardWindow>("Tween Board");
            w.minSize = new Vector2(520, 360);
            w.Show();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _asset = (TweenAsset)EditorGUILayout.ObjectField(_asset, typeof(TweenAsset), false, GUILayout.Width(280));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Create Asset", EditorStyles.toolbarButton))
                {
                    CreateNewAsset();
                }

                if (_asset != null && GUILayout.Button("Recompute Length", EditorStyles.toolbarButton))
                {
                    _asset.RecomputeLength();
                    EditorUtility.SetDirty(_asset);
                }
            }

            if (_asset == null)
            {
                EditorGUILayout.HelpBox("Assign a TweenAsset to edit. This window is a lightweight helper; the full editor is the asset inspector.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(6);

            SerializedObject so = new SerializedObject(_asset);
            var tracksProp = so.FindProperty("tracks");

            for (int i = 0; i < tracksProp.arraySize; i++)
            {
                var trackProp = tracksProp.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.PropertyField(trackProp, new GUIContent($"Track {i}"), true);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove Track", GUILayout.Width(120)))
                        {
                            tracksProp.DeleteArrayElementAtIndex(i);
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_asset);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("+ Add Track", GUILayout.Height(28)))
            {
                tracksProp.InsertArrayElementAtIndex(tracksProp.arraySize);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(so.FindProperty("lengthSeconds"));
            so.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void CreateNewAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Tween Asset", "TweenAsset", "asset", "Choose a location");
            if (string.IsNullOrEmpty(path)) return;

            var a = CreateInstance<TweenAsset>();
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssets();
            _asset = a;
            Selection.activeObject = a;
        }
    }
}
#endif
