using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HologramWedgeSetup
{
    const string MaterialPath = "Assets/Materials/HologramWedges.mat";
    const string SetupKey = "MoonDex.HologramWedges.Assigned";

    [InitializeOnLoadMethod]
    static void Queue()
    {
        EditorApplication.delayCall += BrightenPalette;
        EditorApplication.delayCall += ApplyOnce;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void BrightenPalette()
    {
        const string paletteKey = "MoonDex.HologramWedges.BrightPalette";
        if (SessionState.GetBool(paletteKey, false)) return;
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) return;
        Undo.RecordObject(material, "Brighten hologram wedge palette");
        material.SetColor("_ActiveFill", new Color(0.08f, 0.62f, 0.88f, 1f));
        material.SetColor("_ActiveEdge", new Color(0.65f, 1f, 1f, 1f));
        material.SetColor("_InactiveFill", new Color(0.38f, 0.40f, 0.72f, 1f));
        material.SetColor("_InactiveEdge", new Color(0.78f, 0.76f, 1f, 1f));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        SessionState.SetBool(paletteKey, true);
        Debug.Log("Hologram wedge palette brightened.");
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) ApplyOnce();
    }

    static void ApplyOnce()
    {
        if (!SessionState.GetBool(SetupKey, false)) Apply();
    }

    [MenuItem("Tools/MoonDex/Apply Hologram Wedges")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MoonDex.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        RadialMenuFromYaml menu = null;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var candidate in root.GetComponentsInChildren<RadialMenuFromYaml>(true))
        {
            if (menu != null)
            {
                Debug.LogError("Hologram setup found multiple radial menus; no material was assigned.");
                return;
            }
            menu = candidate;
        }
        if (menu == null) return;
        var shader = Shader.Find("hologram");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) return;
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "HologramWedges" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            Undo.RegisterCreatedObjectUndo(material, "Create hologram wedge material");
        }
        if (menu.wedgeMaterial != material)
        {
            Undo.RecordObject(menu, "Apply hologram wedges");
            menu.wedgeMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        SessionState.SetBool(SetupKey, true);
        Debug.Log("Hologram wedges assigned. Save the scene and enter Play mode to view.", menu);
    }
}
