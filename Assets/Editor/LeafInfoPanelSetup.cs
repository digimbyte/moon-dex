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
        if (menu == null || panel == null || menu.leafInfoPanel != null) return;
        var title = panel.Find("Text Title")?.GetComponent<Nova.TextBlock>();
        var body = panel.Find("Text Body")?.GetComponent<Nova.TextBlock>();
        if (title == null || body == null) { Debug.LogError("Info_Panel requires Text Title and Text Body."); return; }
        bool wasDirty = scene.isDirty;
        Undo.RecordObject(menu, "Assign leaf information panel");
        menu.leafInfoPanel = panel.gameObject;
        menu.leafTitle = title;
        menu.leafBody = body;
        // Navigation owns visibility; do not start the authored IN and OUT simultaneously.
        foreach (var animation in panel.GetComponents<Animator.Animate>())
        {
            Undo.RecordObject(animation, "Disable panel startup playback");
            animation.playAllOnStart = false;
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
