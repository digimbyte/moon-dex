using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LeafInfoPanelSetup
{
    [InitializeOnLoadMethod]
    static void Queue()
    {
        EditorApplication.delayCall += Configure;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }
    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Configure;
    }
    [MenuItem("Tools/MoonDex/Configure Leaf Info Panel")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MoonDex.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        RadialMenuFromYaml menu = null;
        Transform panel = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in root.GetComponentsInChildren<RadialMenuFromYaml>(true))
            {
                if (menu != null) { Debug.LogError("Multiple menus; assign the leaf panel explicitly."); return; }
                menu = candidate;
            }
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "Info_Panel") continue;
                if (panel != null) { Debug.LogError("Multiple Info_Panel objects; assign the panel explicitly."); return; }
                panel = candidate;
            }
        }
        if (menu == null || panel == null) return;
        var title = panel.Find("Text Title")?.GetComponent<Aura.TextBlock>();
        var body = panel.Find("Text Body")?.GetComponent<Aura.TextBlock>();
        if (title == null || body == null) { Debug.LogError("Info_Panel requires Text Title and Text Body."); return; }
        bool needsSetup = menu.leafPanelIn == null || menu.leafPanelOut == null;
        bool wasDirty = scene.isDirty;
        // The authored endpoints are vectors. Position.X is a Length, whereas
        // Position.Raw is the vector binding supported by Core Animate.
        foreach (var animation in panel.GetComponents<Core.Animator.Animate>())
        {
            var data = new SerializedObject(animation);
            var entries = data.FindProperty("configuredTweens");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("name").stringValue;
                if (name != "IN" && name != "OUT") continue;
                if (entry.FindPropertyRelative("propertyName").stringValue != "Position.X") continue;
                if (entry.FindPropertyRelative("targetComponent").objectReferenceValue != panel.GetComponent<Aura.UIBlock2D>()) continue;
                Undo.RegisterCompleteObjectUndo(animation, "Correct panel Nova position binding");
                entry.FindPropertyRelative("propertyName").stringValue = "Position.Raw";
                entry.FindPropertyRelative("detectedPropertyType").stringValue = "Vector3";
                entry.FindPropertyRelative("vectorMask").intValue = 1; // X only; preserve Y/Z.
                data.ApplyModifiedProperties();
                EditorUtility.SetDirty(animation);
                needsSetup = true;
            }
        }
        foreach (var root in scene.GetRootGameObjects())
        foreach (var button in root.GetComponentsInChildren<AuraSamples.UIControls.Button>(true))
        {
            if (button.name != "Back") continue;
            button.OnClicked ??= new UnityEngine.Events.UnityEvent();
            bool wired = false;
            for (int i = 0; i < button.OnClicked.GetPersistentEventCount(); i++)
                wired |= button.OnClicked.GetPersistentTarget(i) == menu && button.OnClicked.GetPersistentMethodName(i) == "Back";
            if (wired) continue;
            Undo.RecordObject(button, "Connect menu Back button");
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.OnClicked, menu.Back);
            EditorUtility.SetDirty(button);
            needsSetup = true;
        }
        if (!needsSetup) return;
        Undo.RecordObject(menu, "Assign leaf information panel");
        menu.leafInfoPanel = panel.gameObject;
        menu.leafTitle = title;
        menu.leafBody = body;
        // Navigation owns visibility; do not start the authored IN and OUT simultaneously.
        foreach (var animation in panel.GetComponents<Core.Animator.Animate>())
        {
            Undo.RecordObject(animation, "Disable panel startup playback");
            animation.playAllOnStart = false;
            var serialized = new SerializedObject(animation);
            var entries = serialized.FindProperty("configuredTweens");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("name").stringValue;
                if (name == "IN") menu.leafPanelIn = animation;
                if (name == "OUT") menu.leafPanelOut = animation;
            }
            EditorUtility.SetDirty(animation);
        }
        Undo.RecordObject(panel.gameObject, "Hide leaf panel until selection");
        panel.gameObject.SetActive(false);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!wasDirty) EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/LeafInfoPanelSetup.txt", "Info_Panel assigned to menu; " + (scene.isDirty ? "scene has unsaved changes." : "scene saved."));
    }
}
