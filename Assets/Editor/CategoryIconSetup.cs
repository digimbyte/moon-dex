using System.IO;
using Core.Registry;
using UnityEditor;
using UnityEngine;

// Registers generated category icons without changing existing faction logos.
public static class CategoryIconSetup
{
    const string IconFolder = "Assets/ArtAssets/Textures/icons/categories";

    [InitializeOnLoadMethod]
    static void Queue()
    {
        EditorApplication.delayCall += Register;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Register;
    }

    [MenuItem("Tools/MoonDex/Register Category Icons")]
    public static void Register()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !Directory.Exists(IconFolder)) return;
        var registry = AssetDatabase.LoadAssetAtPath<Registry>("Assets/data/Icons.asset");
        if (registry == null) return;
        bool changed = false;
        foreach (var file in Directory.GetFiles(IconFolder, "*.png"))
        {
            string path = file.Replace('\\', '/');
            string uid = "category_" + Path.GetFileNameWithoutExtension(path);
            if (registry.HasItem(uid)) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;
            if (!changed) Undo.RegisterCompleteObjectUndo(registry, "Register MoonDex category icons");
            registry.AddItem(new ItemEntry { uid = uid, asset = texture, description = "MoonDex category icon" });
            changed = true;
        }
        if (!changed) return;
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssetIfDirty(registry);
    }
}

public sealed class CategoryIconImports : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
            if (path.StartsWith("Assets/ArtAssets/Textures/icons/categories/") && path.EndsWith(".png"))
            {
                EditorApplication.delayCall += CategoryIconSetup.Register;
                break;
            }
    }
}
