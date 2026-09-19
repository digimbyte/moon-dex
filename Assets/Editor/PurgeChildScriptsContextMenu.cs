#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class PurgeChildScriptsContextMenu
{
	private const int MENU_PRIORITY = 49;

	// ---------------------------
	// 1) Right-click: Keep Root
	// ---------------------------
	[MenuItem("GameObject/Purge/Purge Child Scripts (Keep Root)", false, MENU_PRIORITY)]
	private static void PurgeChildren_KeepRoot(MenuCommand command)
	{
		var root = ResolveTarget(command);
		if (root == null) return;
		PurgeAllScripts(root, includeRoot: false);
	}

	[MenuItem("GameObject/Purge/Purge Child Scripts (Keep Root)", true)]
	private static bool PurgeChildren_KeepRoot_Validate(MenuCommand command)
		=> ResolveTarget(command) != null;

	// ---------------------------
	// 2) Right-click: Self + Children
	// ---------------------------
	[MenuItem("GameObject/Purge/Purge Scripts (Self + Children)", false, MENU_PRIORITY + 1)]
	private static void PurgeSelfAndChildren(MenuCommand command)
	{
		var root = ResolveTarget(command);
		if (root == null) return;
		PurgeAllScripts(root, includeRoot: true);
	}

	[MenuItem("GameObject/Purge/Purge Scripts (Self + Children)", true)]
	private static bool PurgeSelfAndChildren_Validate(MenuCommand command)
		=> ResolveTarget(command) != null;

	// ---------------------------
	// 3) Right-click: Missing Only (Root + Children)
	// ---------------------------
	[MenuItem("GameObject/Purge/Purge Missing Scripts Only (Self + Children)", false, MENU_PRIORITY + 2)]
	private static void PurgeMissingOnly(MenuCommand command)
	{
		var root = ResolveTarget(command);
		if (root == null) return;
		PurgeMissingScriptsOnly(root, includeRoot: true);
	}

	[MenuItem("GameObject/Purge/Purge Missing Scripts Only (Self + Children)", true)]
	private static bool PurgeMissingOnly_Validate(MenuCommand command)
		=> ResolveTarget(command) != null;

	// ---------------------------
	// Target resolution
	// ---------------------------
	private static GameObject ResolveTarget(MenuCommand command)
	{
		if (command != null && command.context is GameObject go)
			return go;

		// Fallback (Unity sometimes provides null context)
		return Selection.activeGameObject;
	}

	// ---------------------------
	// Core: remove ALL MonoBehaviours + missing scripts
	// ---------------------------
	private static void PurgeAllScripts(GameObject root, bool includeRoot)
	{
		if (root == null) return;

		string title = includeRoot ? "Purge Scripts (Self + Children)" : "Purge Child Scripts (Keep Root)";
		string body =
			includeRoot
				? "Remove ALL MonoBehaviour scripts from THIS object AND ALL CHILDREN.\nAlso removes Missing (Mono Script) components.\n\nUndoable."
				: "Remove ALL MonoBehaviour scripts from ALL CHILDREN.\nRoot object will NOT be touched.\nAlso removes Missing (Mono Script) components.\n\nUndoable.";

		if (!EditorUtility.DisplayDialog(title, body, "Proceed", "Cancel"))
			return;

		int removedBehaviours = 0;
		int removedMissing = 0;

		Undo.IncrementCurrentGroup();
		int undoGroup = Undo.GetCurrentGroup();
		Undo.SetCurrentGroupName(title);

		var transforms = root.GetComponentsInChildren<Transform>(true);

		foreach (var t in transforms)
		{
			if (!includeRoot && t == root.transform)
				continue;

			var go = t.gameObject;
			if (go == null) continue;

			Undo.RegisterCompleteObjectUndo(go, "Purge Scripts");

			var mbs = go.GetComponents<MonoBehaviour>();
			for (int i = 0; i < mbs.Length; i++)
			{
				var mb = mbs[i];
				if (mb == null) continue; // missing handled below
				Undo.DestroyObjectImmediate(mb);
				removedBehaviours++;
			}

			removedMissing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

			EditorUtility.SetDirty(go);
		}

		MarkDirty(root);
		Undo.CollapseUndoOperations(undoGroup);

		Debug.Log(
			$"[Purge] Root: '{root.name}' | Removed MonoBehaviours: {removedBehaviours} | Removed Missing Scripts: {removedMissing} | IncludeRoot: {includeRoot}",
			root
		);
	}

	// ---------------------------
	// Core: remove ONLY missing scripts (null) + children
	// ---------------------------
	private static void PurgeMissingScriptsOnly(GameObject root, bool includeRoot)
	{
		if (root == null) return;

		const string title = "Purge Missing Scripts Only (Self + Children)";
		const string body =
			"Remove ONLY Missing (Mono Script) components from THIS object AND ALL CHILDREN.\n" +
			"No valid scripts will be removed.\n\nUndoable.";

		if (!EditorUtility.DisplayDialog(title, body, "Proceed", "Cancel"))
			return;

		int removedMissing = 0;

		Undo.IncrementCurrentGroup();
		int undoGroup = Undo.GetCurrentGroup();
		Undo.SetCurrentGroupName(title);

		var transforms = root.GetComponentsInChildren<Transform>(true);

		foreach (var t in transforms)
		{
			if (!includeRoot && t == root.transform)
				continue;

			var go = t.gameObject;
			if (go == null) continue;

			Undo.RegisterCompleteObjectUndo(go, "Purge Missing Scripts");
			removedMissing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
			EditorUtility.SetDirty(go);
		}

		MarkDirty(root);
		Undo.CollapseUndoOperations(undoGroup);

		Debug.Log($"[PurgeMissingOnly] Root: '{root.name}' | Removed Missing Scripts: {removedMissing}", root);
	}

	private static void MarkDirty(GameObject root)
	{
		if (root != null && root.scene.IsValid())
			EditorSceneManager.MarkSceneDirty(root.scene);

		// Prefab stage / assets are still covered by SetDirty calls above.
	}
}
#endif
