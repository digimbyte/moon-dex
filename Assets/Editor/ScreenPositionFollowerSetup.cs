using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScreenPositionFollowerSetup
{
    const string ScenePath = "Assets/Scenes/MoonDex.unity";
    const string StatusPath = "Temp/ScreenPositionFollowerSetup.txt";

    public static void RunBatch()
    {
        ScreenPositionFollowerChecks.Run();
        EditorSceneManager.OpenScene(ScenePath);
        ConfigureLoadedScene();
    }

    [InitializeOnLoadMethod]
    static void QueueSetup()
    {
        EditorApplication.delayCall += RunChecksOnce;
        if (SessionState.GetBool("MoonDex.ScreenFollowerConfigured", false)) return;
        EditorApplication.delayCall += ConfigureLoadedScene;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void RunChecksOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("MoonDex.ScreenFollowerChecked", false)) return;
        try
        {
            ScreenPositionFollowerChecks.Run();
            SessionState.SetBool("MoonDex.ScreenFollowerChecked", true);
            File.WriteAllText("Temp/ScreenPositionFollowerChecks.txt", "PASS");
        }
        catch (System.Exception error)
        {
            File.WriteAllText("Temp/ScreenPositionFollowerChecks.txt", error.ToString());
            Debug.LogException(error);
        }
    }
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && !SessionState.GetBool("MoonDex.ScreenFollowerConfigured", false))
            EditorApplication.delayCall += ConfigureLoadedScene;
    }

    [MenuItem("Tools/MoonDex/Configure Screen Position Follower")]
    public static void ConfigureLoadedScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        Transform target = null;
        Camera camera = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name != "MoonViewSetup") continue;
                if (target != null) { Debug.LogError("Multiple MoonViewSetup objects; setup aborted."); return; }
                target = item;
            }
            foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
                if (candidate.CompareTag("MainCamera")) camera = candidate;
        }
        if (target == null || camera == null) return;
        var follower = target.GetComponent<ScreenPositionFollower>();
        if (follower == null)
        {
            bool wasDirty = scene.isDirty;
            follower = Undo.AddComponent<ScreenPositionFollower>(target.gameObject);
            Undo.RecordObject(follower, "Configure screen position follower");
            follower.sourceCamera = camera;
            follower.alignmentAnchor = target;
            follower.CaptureOrigin();
            EditorUtility.SetDirty(follower);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
        }
        SessionState.SetBool("MoonDex.ScreenFollowerConfigured", true);
        File.WriteAllText(StatusPath, "Configured MoonViewSetup in " + ScenePath + (scene.isDirty ? "; scene has unsaved changes." : "; scene saved."));
        Debug.Log("MoonViewSetup screen follower configured. Choose an Alignment Anchor child if needed.", follower);
    }
}
