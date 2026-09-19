using UnityEditor;
using UnityEngine;

public static class PurgeChildScripts
{
    [MenuItem("Tools/Utility/Purge_Child_Scripts")]
    private static void PurgeChildScriptsFromSelection()
    {
        var selectedTransforms = Selection.transforms;

        if (selectedTransforms == null || selectedTransforms.Length == 0)
        {
            Debug.LogWarning("No GameObjects selected. Select one or more root objects and run again.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Purge Child Scripts",
                "This will remove ALL scripts (MonoBehaviours) from ALL children of the selected GameObjects.\n\n" +
                "The selected GameObjects themselves will NOT be touched.\n\n" +
                "This action is undoable.",
                "Proceed",
                "Cancel"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        int removedCount = 0;

        foreach (var root in selectedTransforms)
        {
            var childTransforms = root.GetComponentsInChildren<Transform>(true);

            foreach (var t in childTransforms)
            {
                if (t == root)
                    continue;

                var go = t.gameObject;

                var scripts = go.GetComponents<MonoBehaviour>();
                foreach (var script in scripts)
                {
                    if (script == null)
                        continue;

                    Undo.DestroyObjectImmediate(script);
                    removedCount++;
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Purge_Child_Scripts: Removed {removedCount} script component(s) from child GameObjects.");
    }
}
