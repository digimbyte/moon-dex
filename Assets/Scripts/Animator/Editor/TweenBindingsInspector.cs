
#if UNITY_EDITOR
using Animator.Runtime;
using UnityEditor;

namespace Animator.Editor
{
    [CustomEditor(typeof(TweenBindings))]
    public sealed class TweenBindingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "TweenBindings maps string keys to scene objects.\n" +
                "Example key: 'PlayerCam' -> assign Transform (camera) and optionally the Camera component in Components list.",
                MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("bindings"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
