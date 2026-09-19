using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_2023_1_OR_NEWER
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Draws world-space Renderer.bounds for all Renderers that pass:
/// - enabled + activeInHierarchy
/// - layer is enabled in targetCamera.cullingMask
/// Optional:
/// - only those intersecting the camera frustum
/// - within maxDistance from camera
///
/// Attach this to any GameObject in the scene (or keep in a debug prefab).
/// </summary>
[ExecuteAlways]
public class CameraCullingBoundsGizmo : MonoBehaviour
{
	[Header("Target")]
	public Camera targetCamera;

	[Header("Filtering")]
	public bool onlyDrawInFrustum = true;

	[Tooltip("0 = no distance limit.")]
	public float maxDistance = 0f;

	[Tooltip("If true, uses camera.cullingMask. If false, uses the overrideMask below.")]
	public bool useCameraCullingMask = true;

	public LayerMask overrideMask = ~0;

	[Header("Rendering")]
	public bool drawWhenNotSelected = true;
	public bool drawWire = true;
	public bool drawSolid = false;

	[Range(0.01f, 1f)]
	public float solidAlpha = 0.08f;

	[Header("Layer Colors")]
	[Tooltip("Define explicit colors for some layers. Others will use a generated fallback.")]
	public LayerColor[] layerColors = Array.Empty<LayerColor>();

	[Tooltip("If a layer has no explicit color, generate one deterministically.")]
	public bool generateFallbackColors = true;

	[Header("Performance")]
	[Tooltip("Refresh renderer list every N seconds in play mode and edit mode.")]
	public float refreshIntervalSeconds = 1.0f;

	[Tooltip("If true, also include inactive objects in Edit mode (can be noisy).")]
	public bool includeInactiveInEditMode = false;

	[Serializable]
	public struct LayerColor
	{
		[Range(0, 31)] public int layer;
		public Color color;
	}

	float _nextRefreshTime;
	readonly List<Renderer> _renderers = new List<Renderer>(4096);
	readonly Dictionary<int, Color> _layerColorMap = new Dictionary<int, Color>(64);

	void OnEnable()
	{
		BuildLayerColorMap();
		RefreshRenderers(force: true);
	}

	void OnValidate()
	{
		BuildLayerColorMap();
	}

	void Update()
	{
		// Keep it simple: refresh periodically even in Edit mode.
		RefreshRenderers(force: false);
	}

	void RefreshRenderers(bool force)
	{
		float now = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
		if (!force && now < _nextRefreshTime) return;
		_nextRefreshTime = now + Mathf.Max(0.05f, refreshIntervalSeconds);

		if (!targetCamera) targetCamera = Camera.main;
		_renderers.Clear();

		// Find all renderers.
		// In Edit mode, FindObjectsByType can miss some inactive objects depending on Unity version,
		// so we use Resources.FindObjectsOfTypeAll when requested.
#if UNITY_EDITOR
		if (!Application.isPlaying && includeInactiveInEditMode)
		{
			var all = Resources.FindObjectsOfTypeAll<Renderer>();
			for (int i = 0; i < all.Length; i++)
			{
				var r = all[i];
				if (!r) continue;
				// Filter out prefabs / assets not in scene
				if (r.gameObject.scene.IsValid() == false) continue;
				_renderers.Add(r);
			}
			return;
		}
#endif

		var renderers = FindObjectsByType<Renderer>();
		_renderers.AddRange(renderers);
	}

	void BuildLayerColorMap()
	{
		_layerColorMap.Clear();
		if (layerColors == null) return;

		for (int i = 0; i < layerColors.Length; i++)
		{
			int layer = Mathf.Clamp(layerColors[i].layer, 0, 31);
			_layerColorMap[layer] = layerColors[i].color;
		}
	}

	Color GetColorForLayer(int layer)
	{
		if (_layerColorMap.TryGetValue(layer, out var c))
			return c;

		if (!generateFallbackColors)
			return Color.white;

		// Deterministic fallback: HSV based on layer index.
		// (Avoids having to manually define all 32.)
		float h = (layer * 0.6180339887f) % 1f; // golden ratio step
		Color fallback = Color.HSVToRGB(h, 0.85f, 1f);
		_layerColorMap[layer] = fallback;
		return fallback;
	}

	LayerMask EffectiveMask()
	{
		if (useCameraCullingMask && targetCamera) return targetCamera.cullingMask;
		return overrideMask;
	}

	bool LayerInMask(int layer, LayerMask mask)
	{
		int bit = 1 << layer;
		return (mask.value & bit) != 0;
	}

	void DrawAll()
	{
		if (!targetCamera) targetCamera = Camera.main;
		if (!targetCamera) return;

		LayerMask mask = EffectiveMask();
		Plane[] planes = null;
		if (onlyDrawInFrustum)
			planes = GeometryUtility.CalculateFrustumPlanes(targetCamera);

		Vector3 camPos = targetCamera.transform.position;
		float maxDistSqr = (maxDistance > 0f) ? maxDistance * maxDistance : 0f;

		for (int i = 0; i < _renderers.Count; i++)
		{
			var r = _renderers[i];
			if (!r) continue;
			if (!r.enabled) continue;

			var go = r.gameObject;
			if (!go) continue;
			if (!go.activeInHierarchy) continue;

			int layer = go.layer;
			if (!LayerInMask(layer, mask)) continue;

			Bounds b = r.bounds; // world-space AABB used for culling

			if (maxDistance > 0f)
			{
				// Cheap distance check using bounds center
				if ((b.center - camPos).sqrMagnitude > maxDistSqr) continue;
			}

			if (onlyDrawInFrustum)
			{
				if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
			}

			Color col = GetColorForLayer(layer);

			if (drawSolid)
			{
				var solid = col;
				solid.a = solidAlpha;
				Gizmos.color = solid;
				Gizmos.DrawCube(b.center, b.size);
			}

			if (drawWire)
			{
				Gizmos.color = col;
				Gizmos.DrawWireCube(b.center, b.size);
			}
		}
	}

	void OnDrawGizmos()
	{
		if (!drawWhenNotSelected) return;
		DrawAll();
	}

	void OnDrawGizmosSelected()
	{
		if (drawWhenNotSelected) return;
		DrawAll();
	}
}
