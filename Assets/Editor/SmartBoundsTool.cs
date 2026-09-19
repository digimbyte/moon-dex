using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SmartBoundsTool : EditorWindow
{
	[Header("Inflation (scale-aware)")]
	[Tooltip("Extra padding in WORLD meters added to the bounds on each axis (after accounting for transform scale).")]
	public float worldPaddingMeters = 25f;

	[Tooltip("Inflate bounds by this fraction of the mesh's size (local). Example: 0.10 = +10% on each axis.")]
	public float relativePadding = 0.10f;

	[Tooltip("Minimum local padding added per axis (in mesh local units), even if relativePadding is small.")]
	public float minLocalPadding = 0.5f;

	[Header("Safety")]
	[Tooltip("If true, duplicates the mesh per selected object so changes don't affect other instances sharing the mesh.")]
	public bool makeMeshUniquePerObject = true;

	[Tooltip("If true, recomputes tight bounds from vertices before inflating.")]
	public bool recomputeTightBoundsFromVertices = true;

	[MenuItem("Tools/Bounds/Smart Bounds Tool")]
	public static void ShowWindow()
	{
		GetWindow<SmartBoundsTool>("Smart Bounds Tool");
	}

	void OnGUI()
	{
		EditorGUILayout.LabelField("Smart Bounds (Frustum Culling Fix)", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		worldPaddingMeters = EditorGUILayout.FloatField("World Padding (m)", worldPaddingMeters);
		relativePadding = EditorGUILayout.Slider("Relative Padding", relativePadding, 0f, 2f);
		minLocalPadding = EditorGUILayout.FloatField("Min Local Padding", minLocalPadding);

		EditorGUILayout.Space();
		makeMeshUniquePerObject = EditorGUILayout.ToggleLeft("Make Mesh Unique Per Object", makeMeshUniquePerObject);
		recomputeTightBoundsFromVertices = EditorGUILayout.ToggleLeft("Recompute Tight Bounds From Vertices", recomputeTightBoundsFromVertices);

		EditorGUILayout.Space();
		using (new EditorGUI.DisabledScope(Selection.gameObjects == null || Selection.gameObjects.Length == 0))
		{
			if (GUILayout.Button("Process Selected (MeshFilter)"))
				ProcessSelected();
		}

		EditorGUILayout.HelpBox(
			"This modifies Mesh.bounds (local space). Unity frustum-culls using Renderer.bounds (world) derived from Mesh.bounds.\n" +
			"If you share a mesh asset across multiple objects, enable 'Make Mesh Unique' to avoid affecting everything.",
			MessageType.Info);
	}

	void ProcessSelected()
	{
		var gos = Selection.gameObjects;
		int processed = 0;

		foreach (var go in gos)
		{
			var mf = go.GetComponent<MeshFilter>();
			var mr = go.GetComponent<MeshRenderer>();
			if (!mf || !mr || mf.sharedMesh == null) continue;

			Mesh mesh = mf.sharedMesh;

			// Avoid modifying shared assets unintentionally
			if (makeMeshUniquePerObject)
			{
				mesh = DuplicateMesh(mesh, go.name);
				mf.sharedMesh = mesh;
			}

			Undo.RecordObject(mesh, "Smart Bounds Update");

			// 1) Tight bounds
			if (recomputeTightBoundsFromVertices)
				mesh.bounds = ComputeTightLocalBounds(mesh);

			// 2) Inflate bounds (local-space) based on:
			//    - relative padding (% of size)
			//    - min local padding
			//    - world padding converted into local padding using lossyScale
			Bounds b = mesh.bounds;

			Vector3 size = b.size;
			Vector3 localPadFromRelative = new Vector3(
				Mathf.Max(size.x * relativePadding, minLocalPadding),
				Mathf.Max(size.y * relativePadding, minLocalPadding),
				Mathf.Max(size.z * relativePadding, minLocalPadding)
			);

			Vector3 localPadFromWorld = WorldPaddingToLocal(go.transform, worldPaddingMeters);

			Vector3 totalLocalPad = localPadFromRelative + localPadFromWorld;

			// Expand expects "amount to add to size" (per axis total), so multiply padding-by-2? No:
			// If you want +pad on each SIDE, you add 2*pad to size.
			b.Expand(totalLocalPad * 2f);
			mesh.bounds = b;

			EditorUtility.SetDirty(mesh);
			processed++;
		}

		Debug.Log($"SmartBoundsTool: processed {processed} object(s).");
	}

	static Bounds ComputeTightLocalBounds(Mesh mesh)
	{
		var verts = mesh.vertices;
		if (verts == null || verts.Length == 0)
			return new Bounds(Vector3.zero, Vector3.one);

		Vector3 min = verts[0];
		Vector3 max = verts[0];

		for (int i = 1; i < verts.Length; i++)
		{
			Vector3 v = verts[i];
			min = Vector3.Min(min, v);
			max = Vector3.Max(max, v);
		}

		var bounds = new Bounds();
		bounds.SetMinMax(min, max);

		// Avoid zero-size bounds (can happen in flat meshes)
		Vector3 s = bounds.size;
		if (s.x < 0.0001f) s.x = 0.0001f;
		if (s.y < 0.0001f) s.y = 0.0001f;
		if (s.z < 0.0001f) s.z = 0.0001f;
		bounds.size = s;

		return bounds;
	}

	static Vector3 WorldPaddingToLocal(Transform t, float worldPaddingMeters)
	{
		// Convert a uniform WORLD padding into local-space padding per axis.
		// Use abs to handle negative scales; clamp to avoid division by zero.
		Vector3 s = t.lossyScale;
		float sx = Mathf.Max(Mathf.Abs(s.x), 0.0001f);
		float sy = Mathf.Max(Mathf.Abs(s.y), 0.0001f);
		float sz = Mathf.Max(Mathf.Abs(s.z), 0.0001f);

		return new Vector3(worldPaddingMeters / sx, worldPaddingMeters / sy, worldPaddingMeters / sz);
	}

	static Mesh DuplicateMesh(Mesh original, string ownerName)
	{
		var copy = Object.Instantiate(original);
		copy.name = $"{original.name}_BoundsCopy_{ownerName}";
		return copy;
	}
}
