using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Rendering;

public class RadialMenuFromYaml : MonoBehaviour
{
	[Header("Input")]
	public TextAsset yaml;
	public string rootKey = "items";

	[Header("Leaf Information Panel")]
	public GameObject leafInfoPanel;
	public Nova.TextBlock leafTitle;
	public Nova.TextBlock leafBody;

	[Header("Fallback Input")]
	[Tooltip("If true, the builder will attach a fallback physics-raycast input forwarder when playing. Useful when UI elements intercept EventSystem raycasts.")]
	public bool enableFallbackPhysicsInput = true;

	[Header("Mouse Wheel Rotation")]
	[Tooltip("Degrees of ring rotation per mouse-wheel step.")]
	public float wheelRotationDegrees = 150f;
	[Tooltip("How quickly rings reach their rotation target. Higher responds faster.")]
	[Min(0.01f)] public float wheelRotationResponse = 30f;

	[Header("Prefab Root (required)")]
	public GameObject itemRootPrefab;

	[Header("Mesh Child")]
	public string meshChildName = "WedgeMesh";
	public Material wedgeMaterial;
	public bool addMeshColliderOnMeshChild = false;

	[Header("Anchors")]
	public bool createAnchors = true;
	public string centerAnchorName = "CenterAnchor";
	public string startCapAnchorName = "StartCapAnchor";
	public string endCapAnchorName = "EndCapAnchor";

	[Header("Radial Layout")]
	[Range(1, 512)] public int arcSegments = 24;
	public float startAngleDegrees = -90f;
	[Min(0f)] public float neighborGapDegrees = 2f;

	[Header("Rings (multi-depth)")]
	public bool useRings = true;
	public string ringRootNamePrefix = "Ring_";
	public Vector3 ringOffsetBase = Vector3.zero;
	public Vector3 ringOffsetPerDepth = new Vector3(0f, 0f, -140f);
	[Tooltip("Meters added to the wedge RADIUS per ring depth. (Positive pushes rings outward; negative pulls inward.)")]
	public float ringRadiusOffsetPerDepth = 0f;

	[Header("Wedge Geometry (meters)")]
	[Min(0f)] public float innerDiameter = 0.50f;
	[Min(0.0001f)] public float radialThickness = 0.15f;
	[Min(0.0001f)] public float extrusionDepth = 0.02f;

	[Header("Renderer Defaults (mesh child)")]
	public ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
	public bool receiveShadows = false;

	[Header("Runtime")]
	public bool rebuildOnEnable = true;
	public bool rebuildOnlyOncePerPlay = true;
	public bool autoRebuildPollEnabled = false;
	[Min(0.1f)] public float autoRebuildPollSeconds = 1f;

	[Header("Editor")]
	public bool previewInEditor = false; // keep false unless you explicitly want edit-mode previews
	public bool clearChildrenOnRebuild = true;

	[Header("Safety")]
	[Range(1, 2048)] public int maxItemsPerLevel = 512;

	[Header("Selection State")]
	public bool trackSelectionState = true;
	public string ActiveNodeId { get; private set; }

	public MenuNode Root { get; private set; }

	// Simple pool for runtime Mesh objects to reduce allocations when rebuilding frequently.
	readonly Stack<Mesh> _meshPool = new Stack<Mesh>();
	readonly HashSet<Mesh> _ownedMeshes = new HashSet<Mesh>();
	readonly HashSet<Mesh> _pooledMeshes = new HashSet<Mesh>();

	Mesh GetPooledMesh()
	{
		while (_meshPool.Count > 0)
		{
			var m = _meshPool.Pop();
			_pooledMeshes.Remove(m);
			if (m == null) { _ownedMeshes.Remove(m); continue; }
			m.Clear();
			return m;
		}
		var mesh = new Mesh { name = "WedgeMeshRuntime" };
		mesh.MarkDynamic();
		mesh.hideFlags = HideFlags.DontSave;
		_ownedMeshes.Add(mesh);
		return mesh;
	}

	void ReleasePooledMesh(Mesh mesh)
	{
		if (mesh == null || !_ownedMeshes.Contains(mesh) || !_pooledMeshes.Add(mesh)) return;
		mesh.Clear();
		// keep hideFlags; mesh is transient only
		_meshPool.Push(mesh);
	}

	readonly Stack<MenuNode> _navStack = new Stack<MenuNode>();
	readonly List<MenuButtonBase> _menuItems = new List<MenuButtonBase>();
	readonly List<Transform> _ringRoots = new List<Transform>();

	bool _building;
	SplineSync _depthTracks;
	bool _rebuiltOnceThisPlay;
	Coroutine _autoRebuildCo;
	bool _lastAutoRebuildPollState;

#if UNITY_EDITOR
	void OnValidate()
	{
		if (itemRootPrefab == null) return;

		// Enforce: itemRootPrefab must be a prefab asset (Project window), not a scene object.
		if (!PrefabUtility.IsPartOfPrefabAsset(itemRootPrefab))
		{
			Debug.LogError("[RadialMenuFromYaml] itemRootPrefab must be a prefab asset (Project window), not a scene object. Clearing field.", this);
			itemRootPrefab = null;
			EditorUtility.SetDirty(this);
		}
	}
#endif

	void Update()
	{
		if (!Application.isPlaying) return;
		if (_lastAutoRebuildPollState != autoRebuildPollEnabled)
		{
			_lastAutoRebuildPollState = autoRebuildPollEnabled;
			if (autoRebuildPollEnabled) StartAutoRebuildPollIfNeeded();
			else StopAutoRebuildPoll();
		}
	}

	void OnEnable()
	{
		if (!Application.isPlaying && !previewInEditor) return;
		if (rebuildOnEnable) Rebuild();
		StartAutoRebuildPollIfNeeded();

		// Attach input forwarder if requested so clicks still work when UI blocks EventSystem raycasts.
		if (Application.isPlaying && enableFallbackPhysicsInput)
		{
			if (GetComponent<RadialMenuInputForwarder>() == null)
				gameObject.AddComponent<RadialMenuInputForwarder>();
		}
	}

	void OnDisable()
	{
		StopAutoRebuildPoll();
	}

	void OnDestroy()
	{
		ClearRingsFromDepth(0);
		foreach (var mesh in _ownedMeshes)
		{
			if (mesh == null) continue;
			if (Application.isPlaying) Destroy(mesh);
			else DestroyImmediate(mesh);
		}
		_meshPool.Clear();
		_pooledMeshes.Clear();
		_ownedMeshes.Clear();
	}

	void StartAutoRebuildPollIfNeeded()
	{
		if (!Application.isPlaying) return;
		if (!autoRebuildPollEnabled) return;
		if (_autoRebuildCo != null) return;
		_autoRebuildCo = StartCoroutine(AutoRebuildPollLoop());
	}

	void StopAutoRebuildPoll()
	{
		if (_autoRebuildCo == null) return;
		StopCoroutine(_autoRebuildCo);
		_autoRebuildCo = null;
	}

	System.Collections.IEnumerator AutoRebuildPollLoop()
	{
		while (enabled && autoRebuildPollEnabled)
		{
			var wait = Mathf.Max(0.1f, autoRebuildPollSeconds);
			yield return new WaitForSeconds(wait);
			if (!enabled) yield break;
			Rebuild();
		}
		_autoRebuildCo = null;
	}

	[ContextMenu("Rebuild")]
	public void Rebuild()
	{
		if (_building) return;

		// If something is enabling/disabling this object every frame, Rebuild() can get called repeatedly.
		// Default to once-per-play unless you explicitly want rebuild spam.
		if (Application.isPlaying && rebuildOnlyOncePerPlay && _rebuiltOnceThisPlay) return;

		_building = true;


		try
		{


			if (yaml == null)
			{
				// No YAML => do nothing. This component is inert without data.
				return;
			}

			if (string.IsNullOrWhiteSpace(yaml.text))
			{
				// Empty YAML => do nothing.
				return;
			}

			if (itemRootPrefab == null)
			{
				Debug.LogError("[RadialMenuFromYaml] Missing itemRootPrefab.");
				return;
			}

			// HEALTH CHECK: if itemRootPrefab points at THIS scene object (or otherwise includes this builder),
			// Instantiate() will clone the builder, which will Rebuild(), which will clone again => hard-freeze.
			if (ReferenceEquals(itemRootPrefab, gameObject))
			{
				Debug.LogError("[RadialMenuFromYaml] itemRootPrefab is set to the same GameObject that has RadialMenuFromYaml. " +
					"This will recursively instantiate itself and freeze Unity. Assign a separate item prefab (your 3D mesh button root). Aborting.");
				return;
			}

			if (itemRootPrefab.GetComponent<RadialMenuFromYaml>() != null)
			{
				Debug.LogError("[RadialMenuFromYaml] itemRootPrefab contains RadialMenuFromYaml. " +
					"That would recursively instantiate menus. Assign a separate item prefab (no RadialMenuFromYaml on it). Aborting.");
				return;
			}

			if (wedgeMaterial == null)
			{
				Debug.LogError("[RadialMenuFromYaml] Missing wedgeMaterial.");
				return;
			}

			if (!YamlSubsetParser.TryParseMenu(yaml.text, rootKey, out var root, out var err))
			{
				Debug.LogError("[RadialMenuFromYaml] YAML parse failed:\n" + err);
				return;
			}

			CacheRemainingDepth(root);
			if (!ReplaceLevel(root, 0)) return;
			Root = root;
			_navStack.Clear();
			_navStack.Push(root);

			// Reset selection state when parsing a new tree.
			ActiveNodeId = null;

			// Build ring 0 (root).
			_rebuiltOnceThisPlay = Application.isPlaying;
			UpdateSelectionStates();
		}
		finally
		{
			_building = false;
		}
	}

	[ContextMenu("Back")]
	public void Back()
	{
		if (_building || _navStack.Count <= 1) return;
		_building = true;
		try
		{
			var path = _navStack.ToArray();
			int parentDepth = _navStack.Count - 2;
			if (!useRings && !ReplaceLevel(path[1], parentDepth)) return;
			if (useRings) ClearRingsFromDepth(parentDepth + 1);
			_navStack.Pop();
			ActiveNodeId = _navStack.Count > 1 ? _navStack.Peek().id : null;
			UpdateSelectionStates();
		}
		finally { _building = false; }
	}

	public void Open(MenuNode node)
	{
		if (_building || node == null || node.children == null || node.children.Count == 0) return;
		if (node.RemainingDepth < 0) CacheRemainingDepth(node);
		Navigate(node, _navStack.Count - 1);
	}

	public void HandleItemClicked(MenuButtonBase item)
	{
		if (_building || item == null || !item.isActiveAndEnabled || item.Owner != this || !_menuItems.Contains(item)) return;
		if (item.Node == null || string.IsNullOrWhiteSpace(item.Node.id) || item.ParentDepth < 0) return;
		Navigate(item.Node, item.ParentDepth);
	}

	void Navigate(MenuNode node, int parentDepth)
	{
		_building = true;
		try
		{
			int depth = parentDepth + 1;
			if (node.children != null && node.children.Count > 0)
			{
				if (!ReplaceLevel(node, depth)) return;
			}
			else ClearRingsFromDepth(depth);
			while (_navStack.Count - 1 > parentDepth) _navStack.Pop();
			_navStack.Push(node);
			ActiveNodeId = node.id;
			UpdateSelectionStates();
		}
		finally { _building = false; }
	}

	bool ReplaceLevel(MenuNode parent, int depth)
	{
		Transform pending = null;
		var pendingItems = new List<MenuButtonBase>();
		var layoutActions = new List<Action>();
		try
		{
			if (itemRootPrefab == null || wedgeMaterial == null)
				throw new InvalidOperationException("Missing menu prefab or material.");
			if (itemRootPrefab.GetComponentInChildren<RadialMenuFromYaml>(true) != null)
				throw new InvalidOperationException("Menu item prefab contains a menu builder.");
			pending = CreateRingRoot(depth);
			BuildLevel(parent, depth, pending, pendingItems, layoutActions);
			// Nova requires activation to resolve layout. Keep the old ring intact
			// until this synchronous finalization has also succeeded.
			pending.gameObject.SetActive(true);
			foreach (var applyLayout in layoutActions) applyLayout();
		}
		catch (Exception error)
		{
			if (pending != null) DestroyGO(pending.gameObject);
			Debug.LogException(error, this);
			return false;
		}

		ClearRingsFromDepth(useRings ? depth : 0);
		while (_ringRoots.Count <= depth) _ringRoots.Add(null);
		_ringRoots[depth] = pending;
		_menuItems.AddRange(pendingItems);
		return true;
	}

	void UpdateSelectionStates()
	{
		var selectedNode = _navStack.Count > 1 ? _navStack.Peek() : null;
		bool showInfo = selectedNode != null && (selectedNode.children == null || selectedNode.children.Count == 0);
		if (leafTitle != null) leafTitle.Text = showInfo ? selectedNode.label : string.Empty;
		if (leafBody != null) leafBody.Text = showInfo ? selectedNode.description ?? string.Empty : string.Empty;
		if (leafInfoPanel != null) leafInfoPanel.SetActive(showInfo);
		// A leaf adds no ring: anchor the deepest existing ring, not stack count.
		int activeRingDepth = -1;
		for (int i = _ringRoots.Count - 1; i >= 0; i--)
			if (_ringRoots[i] != null) { activeRingDepth = i; break; }
		for (int i = 0; i <= activeRingDepth; i++)
		{
			if (_ringRoots[i] == null) continue;
			_ringRoots[i].localPosition = useRings
				? transform.InverseTransformVector(ringOffsetPerDepth * (i - activeRingDepth))
				: Vector3.zero;
		}
		if (_depthTracks == null) _depthTracks = GetComponentInParent<SplineSync>();
		if (_depthTracks != null && _navStack.Count > 0)
		{
			int depth = _navStack.Count - 1;
			int remaining = Mathf.Max(0, _navStack.Peek().RemainingDepth);
			_depthTracks.SetPercentage(depth == 0 ? 0f : (float)depth / (depth + remaining));
		}
		if (!trackSelectionState) return;

		_menuItems.RemoveAll(m => m == null);

		// stack.ToArray() => top-first. last entry is the synthetic root node.
		var path = _navStack.ToArray();
		string activeId = null;
		var selectedIds = new HashSet<string>();

		if (path.Length >= 2)
		{
			activeId = path[0]?.id;

			// Selected = ancestors excluding active and excluding synthetic root.
			for (int i = 1; i < path.Length - 1; i++)
			{
				var id = path[i]?.id;
				if (!string.IsNullOrWhiteSpace(id)) selectedIds.Add(id);
			}
		}

		for (int i = 0; i < _menuItems.Count; i++)
		{
			var m = _menuItems[i];
			if (!m) continue;

			var id = m.NodeId;
			if (string.IsNullOrWhiteSpace(id)) continue;

			m.SetActive(id == activeId);
			m.SetSelected(selectedIds.Contains(id));
		}
	}

	void BuildLevel(MenuNode parent, int ringDepth, Transform ringRoot, List<MenuButtonBase> pendingItems, List<Action> layoutActions)
	{

		var items = parent.children ?? new List<MenuNode>();
		if (items.Count == 0)
		{
			Debug.LogWarning($"[RadialMenuFromYaml] No items under '{parent.id}'.");
			return;
		}

		if (items.Count > maxItemsPerLevel)
		{
			throw new InvalidOperationException($"Menu has {items.Count} items (limit {maxItemsPerLevel}).");
		}

		float totalWeight = 0f;
		for (int i = 0; i < items.Count; i++)
			totalWeight += Mathf.Max(0.0001f, items[i].weight);

		float cursor = startAngleDegrees;



		for (int i = 0; i < items.Count; i++)
		{
			var node = items[i];
			float w = Mathf.Max(0.0001f, node.weight);
			float wedgeDeg = (w / totalWeight) * 360f;

			// 1) Instantiate the behavior root prefab
			var rootGO = Instantiate(itemRootPrefab, ringRoot);

			rootGO.name = $"MenuItem_{i:00}_{node.id}";
			rootGO.transform.localPosition = Vector3.zero;
			rootGO.transform.localRotation = Quaternion.identity;
			rootGO.transform.localScale = Vector3.one;

			// 1.5) Ensure the correct button type exists for this node.
			EnsureButtonType(rootGO, node);

			// 2) Ensure procedural mesh child exists under it
			var meshChild = EnsureMeshChild(rootGO.transform);

			// 3) Ensure MeshFilter / MeshRenderer on mesh child
			var mf = meshChild.GetComponent<MeshFilter>();
			if (mf == null) mf = meshChild.gameObject.AddComponent<MeshFilter>();

			var mr = meshChild.GetComponent<MeshRenderer>();
			if (mr == null) mr = meshChild.gameObject.AddComponent<MeshRenderer>();

			mr.sharedMaterial = wedgeMaterial;
			mr.shadowCastingMode = shadowCastingMode;
			mr.receiveShadows = receiveShadows;

			// 4) Create (or reuse) a mesh instance for this wedge
			// IMPORTANT: do NOT let multiple instances share one mesh object.
			var mesh = GetPooledMesh();
			mf.sharedMesh = mesh;

			// 5) Build geometry via TOOL (no MonoBehaviour wedge script)
			var p = RadialWedgeMeshTool.Params.Default;

			// Per-ring radius offset (supports outer/inner ring spacing)
			float innerDiameterForRing = innerDiameter + (2f * ringRadiusOffsetPerDepth * ringDepth);
			p.innerDiameter = Mathf.Max(0.0001f, innerDiameterForRing);
			p.radialThickness = radialThickness;
			p.extrusionDepth = extrusionDepth;

			p.arcSegments = arcSegments;

			// Allocation logic:
			// cursor = start angle of allocated slice
			p.wedgeDegrees = wedgeDeg;
			p.offsetDegrees = cursor;
			p.neighborGapDegrees = neighborGapDegrees;

			p.recalcNormals = true;
			p.recalcBounds = true;

			RadialWedgeMeshTool.BuildInto(mesh, p);
			var triangles = mesh.triangles;
			for (int triangle = 0; triangle < triangles.Length; triangle += 3)
			{
				int first = triangles[triangle];
				triangles[triangle] = triangles[triangle + 2];
				triangles[triangle + 2] = first;
			}
			mesh.triangles = triangles;
			mesh.RecalculateNormals();

			// A separate, unmodified mesh preserves the resting hit area while
			// WedgeMesh moves, scales, or changes its vertices on hover.
			var hitArea = new GameObject("StationaryHitArea");
			hitArea.layer = meshChild.gameObject.layer;
			hitArea.transform.SetParent(rootGO.transform, false);
			hitArea.transform.localPosition = meshChild.localPosition;
			hitArea.transform.localRotation = meshChild.localRotation;
			hitArea.transform.localScale = meshChild.localScale;
			var hitMesh = GetPooledMesh();
			hitArea.AddComponent<MeshFilter>().sharedMesh = hitMesh;
			RadialWedgeMeshTool.BuildInto(hitMesh, p);

			// 6) Optional MeshCollider on mesh child
			// 6) Ensure MeshCollider uses the generated mesh at runtime so input/event
			// handlers have a deterministic collider to raycast against.
			{
				var mc = meshChild.GetComponent<MeshCollider>();
				if (mc == null) mc = meshChild.gameObject.AddComponent<MeshCollider>();
				mc.sharedMesh = null;
				// Keep physics outward-facing while the visible surface is inverted.
				mc.sharedMesh = hitMesh;
			}

			// 6.5) Create anchor transforms for text/UI placement
			if (createAnchors)
			{
				CreateAnchors(rootGO.transform, p);
			}

			// 7) Bind data to the prefab root's behavior scripts
			BindToHost(rootGO, node, i, cursor, wedgeDeg);

			// 8) If the item has MenuSetup, call its post-build hook (anchors/mesh now exist)
			TryPostBuildSetup(rootGO, meshChild, ringDepth, pendingItems);
			hitArea.AddComponent<RadialMenuMeshEvents>().Initialize(pendingItems[pendingItems.Count - 1]);

			// 8.5) Move UIBlock2D under the Center anchor if it exists (ensure UI follows anchor)
			if (!string.IsNullOrEmpty(centerAnchorName))
			{
				var uiBlock = rootGO.transform.Find("UIBlock2D");
				var center = rootGO.transform.Find(centerAnchorName);
				if (uiBlock != null && center != null)
				{
					uiBlock.SetParent(center, false);
					uiBlock.localPosition = Vector3.zero;
					uiBlock.localRotation = Quaternion.identity;
					uiBlock.localScale = Vector3.one;

					// Anchor text and icon halfway through the ring's extrusion.
					float faceZ = Mathf.Max(0.0001f, p.extrusionDepth) * 0.5f;
					Vector3 labelPosition = center.localPosition;
					labelPosition.z = faceZ;
					Vector3 labelLocalPosition = center.InverseTransformPoint(rootGO.transform.TransformPoint(labelPosition));
					var block = uiBlock.GetComponent<Nova.UIBlock2D>();
					float midRadius = p.innerDiameter * 0.5f + p.radialThickness * 0.5f;
					float labelWidth = Mathf.Max(0.001f, midRadius * Mathf.Max(0.001f, p.wedgeDegrees - p.neighborGapDegrees) * Mathf.Deg2Rad * 0.9f);
					float labelHeight = p.radialThickness * 0.8f;
					var tracker = uiBlock.GetComponent<TrackCamera>();
					if (tracker == null) tracker = uiBlock.gameObject.AddComponent<TrackCamera>();
					tracker.enabled = true;
					uiBlock.localRotation = Quaternion.Inverse(center.localRotation);
					if (block != null)
					{
						block.Size.X = labelWidth;
						block.Size.Y = labelHeight;
						layoutActions.Add(() => block.TrySetLocalPosition(labelLocalPosition));
					}
					else
						uiBlock.localPosition = labelLocalPosition;

					var text = uiBlock.GetComponentInChildren<Nova.TextBlock>(true);
					if (text != null)
					{
						text.Text = node.label;
						text.Size.X = labelWidth;
						text.Size.Y = labelHeight;
						text.TMP.fontSize *= 10f;
						text.TMP.fontSizeMax = text.TMP.fontSize;
						text.TMP.fontSizeMin = 1f;
						text.TMP.enableAutoSizing = true;
						text.TMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
						text.TMP.overflowMode = TMPro.TextOverflowModes.Truncate;
						text.TMP.margin = Vector4.zero;
						text.AutoSize.X = Nova.AutoSize.None;
						text.AutoSize.Y = Nova.AutoSize.None;
						text.TMP.alignment = TMPro.TextAlignmentOptions.Center;
						text.Alignment.X = 0;
						text.Alignment.Y = 0;
						var curved = text.gameObject.AddComponent<CurvedMenuText>();
						curved.Configure(text.TMP, rootGO.transform, midRadius, p.offsetDegrees + p.wedgeDegrees * 0.5f, faceZ);
						layoutActions.Add(() =>
						{
							text.TrySetLocalPosition(Vector3.zero);
							text.CalculateLayout();
						});
					}
				}
			}
			cursor += wedgeDeg;
		}
	}

	void EnsureButtonType(GameObject rootGO, MenuNode node)
	{
		if (rootGO == null) return;

		bool isLeaf = (node == null || node.children == null || node.children.Count == 0);

		var item = rootGO.GetComponent<ItemButton>();
		var menu = rootGO.GetComponent<MenuButton>();

		if (isLeaf)
		{
			if (item == null) item = rootGO.AddComponent<ItemButton>();
			item.enabled = true;
			if (menu != null) menu.enabled = false;
		}
		else
		{
			if (menu == null) menu = rootGO.AddComponent<MenuButton>();
			menu.enabled = true;
			if (item != null) item.enabled = false;
		}
	}

	Transform EnsureMeshChild(Transform root)
	{
		var existing = root.Find(meshChildName);
		if (existing != null) return existing;

		var go = new GameObject(meshChildName);
		go.transform.SetParent(root, false);
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;
		return go.transform;
	}

	void TryPostBuildSetup(GameObject rootGO, Transform meshChild, int parentDepth, List<MenuButtonBase> pendingItems)
	{
		if (rootGO == null) return;

		MenuButtonBase setup = null;
		var itemBtn = rootGO.GetComponent<ItemButton>();
		var menuBtn = rootGO.GetComponent<MenuButton>();

		if (itemBtn != null && itemBtn.enabled) setup = itemBtn;
		else if (menuBtn != null && menuBtn.enabled) setup = menuBtn;

		if (!setup) return;

		Transform center = null;
		Transform startCap = null;
		Transform endCap = null;

		if (createAnchors)
		{
			var t = rootGO.transform;
			if (!string.IsNullOrEmpty(centerAnchorName)) center = t.Find(centerAnchorName);
			if (!string.IsNullOrEmpty(startCapAnchorName)) startCap = t.Find(startCapAnchorName);
			if (!string.IsNullOrEmpty(endCapAnchorName)) endCap = t.Find(endCapAnchorName);
		}

		setup.PostBuildSetup(this, parentDepth, meshChild, center, startCap, endCap);
		pendingItems.Add(setup);

	}

	void CreateAnchors(Transform root, RadialWedgeMeshTool.Params p)
	{
		// Calculate wedge geometry parameters
		float innerR = Mathf.Max(0f, p.innerDiameter * 0.5f);
		float outerR = Mathf.Max(innerR + 0.0001f, innerR + Mathf.Max(0.0001f, p.radialThickness));
		float midR = (innerR + outerR) * 0.5f;
		float z0 = 0f;
		float z1 = Mathf.Max(0.0001f, p.extrusionDepth);
		float zMid = (z0 + z1) * 0.5f;

		// Account for neighbor gap to get effective arc
		float effDeg = Mathf.Clamp(p.wedgeDegrees - Mathf.Max(0f, p.neighborGapDegrees), 0.001f, 360f);
		float centerDeg = p.offsetDegrees + (p.wedgeDegrees * 0.5f);
		float startDeg = centerDeg - (effDeg * 0.5f);
		float endDeg = centerDeg + (effDeg * 0.5f);

		// 1) Center anchor - at radial center of wedge, midpoint of extrusion depth
		if (!string.IsNullOrEmpty(centerAnchorName))
		{
			float centerRad = centerDeg * Mathf.Deg2Rad;
			Vector3 centerPos = new Vector3(
				Mathf.Cos(centerRad) * midR,
				Mathf.Sin(centerRad) * midR,
				zMid
			);

			var centerAnchor = EnsureAnchor(root, centerAnchorName);
			centerAnchor.localPosition = centerPos;
			
			// Orient with forward pointing radially outward, up = +Z
			Vector3 radialDir = new Vector3(Mathf.Cos(centerRad), Mathf.Sin(centerRad), 0f).normalized;
			Vector3 forward = radialDir;
			Vector3 up = Vector3.forward; // +Z
			centerAnchor.localRotation = Quaternion.LookRotation(forward, up);
		}

		// 2) Start cap anchor - center of start cap face
		if (!string.IsNullOrEmpty(startCapAnchorName))
		{
			float startRad = startDeg * Mathf.Deg2Rad;
			Vector3 startPos = new Vector3(
				Mathf.Cos(startRad) * midR,
				Mathf.Sin(startRad) * midR,
				zMid
			);

			var startAnchor = EnsureAnchor(root, startCapAnchorName);
			startAnchor.localPosition = startPos;
			
			// Forward = tangent perpendicular to start edge (inward toward wedge)
			// Up = radial direction at start angle
			Vector3 radialDir = new Vector3(Mathf.Cos(startRad), Mathf.Sin(startRad), 0f).normalized;
			float perpRad = (startDeg + 90f) * Mathf.Deg2Rad; // 90° CCW from radial
			Vector3 forward = new Vector3(Mathf.Cos(perpRad), Mathf.Sin(perpRad), 0f).normalized;
			Vector3 up = radialDir;
			startAnchor.localRotation = Quaternion.LookRotation(forward, up);
		}

		// 3) End cap anchor - center of end cap face
		if (!string.IsNullOrEmpty(endCapAnchorName))
		{
			float endRad = endDeg * Mathf.Deg2Rad;
			Vector3 endPos = new Vector3(
				Mathf.Cos(endRad) * midR,
				Mathf.Sin(endRad) * midR,
				zMid
			);

			var endAnchor = EnsureAnchor(root, endCapAnchorName);
			endAnchor.localPosition = endPos;
			
			// Forward = tangent perpendicular to end edge (inward toward wedge)
			// Up = radial direction at end angle
			Vector3 radialDir = new Vector3(Mathf.Cos(endRad), Mathf.Sin(endRad), 0f).normalized;
			float perpRad = (endDeg - 90f) * Mathf.Deg2Rad; // 90° CW from radial
			Vector3 forward = new Vector3(Mathf.Cos(perpRad), Mathf.Sin(perpRad), 0f).normalized;
			Vector3 up = radialDir;
			endAnchor.localRotation = Quaternion.LookRotation(forward, up);
		}
	}

	Transform EnsureAnchor(Transform root, string anchorName)
	{
		var existing = root.Find(anchorName);
		if (existing != null) return existing;

		var go = new GameObject(anchorName);
		go.transform.SetParent(root, false);
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;
		return go.transform;
	}

	void BindToHost(GameObject rootGO, MenuNode node, int index, float startDeg, float wedgeDeg)
	{
		var hosts = rootGO.GetComponents<MonoBehaviour>();
		for (int i = 0; i < hosts.Length; i++)
		{
			if (hosts[i] is IRadialMenuItemHost host)
			{
				host.Bind(node, index, startDeg, wedgeDeg);
			}
		}
	}

	Transform CreateRingRoot(int depth)
	{
		var go = new GameObject(string.IsNullOrEmpty(ringRootNamePrefix) ? $"Ring_{depth}" : ringRootNamePrefix + depth);
		go.SetActive(false);
		go.transform.SetParent(transform, false);
		go.AddComponent<RadialRingRotation>().Initialize(this);
		go.transform.localPosition = useRings
			? transform.InverseTransformPoint(transform.position + ringOffsetBase + ringOffsetPerDepth * depth)
			: Vector3.zero;
		return go.transform;
	}

	void ClearRingsFromDepth(int depth)
	{
		depth = Mathf.Max(0, depth);

		for (int i = _ringRoots.Count - 1; i >= depth; i--)
		{
			var t = _ringRoots[i];
			if (t != null) DestroyGO(t.gameObject);
			_ringRoots[i] = null;
		}

		// MenuButtonBase.ParentDepth == ring depth that spawned it.
		_menuItems.RemoveAll(m => m == null || m.ParentDepth >= depth);
	}

	void DestroyGO(GameObject go)
	{
		if (!go) return;
		go.SetActive(false);
		foreach (var button in go.GetComponentsInChildren<MenuButtonBase>(true)) button.Retire();
		var released = new HashSet<Mesh>();
		foreach (var collider in go.GetComponentsInChildren<MeshCollider>(true))
		{
			if (collider.sharedMesh != null && _ownedMeshes.Contains(collider.sharedMesh))
				collider.sharedMesh = null;
		}
		foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
		{
			var mesh = filter.sharedMesh;
			if (mesh == null || !_ownedMeshes.Contains(mesh)) continue;
			filter.sharedMesh = null;
			released.Add(mesh);
		}
		foreach (var mesh in released) ReleasePooledMesh(mesh);
		if (Application.isPlaying) Destroy(go);
		else DestroyImmediate(go);
	}

	// -----------------------------
	// Data model
	// -----------------------------
	static int CacheRemainingDepth(MenuNode node)
	{
		int remaining = 0;
		if (node.children != null)
			foreach (var child in node.children)
				remaining = Mathf.Max(remaining, 1 + CacheRemainingDepth(child));
		return node.RemainingDepth = remaining;
	}

	public class MenuNode
	{
		internal int RemainingDepth = -1;
		public string id;
		public string label;
		public string description;
		public float weight = 1f;
		public List<MenuNode> children = new List<MenuNode>();
	}

	// -----------------------------
	// YAML subset parser
	// Supports:
	// - rootKey: (e.g. items:)
	// - list items: "- id: vehicles"
	// - scalar props: "label: Vehicles", "weight: 3"
	// - nested lists via "children:"
	//
	// Not supported (by design):
	// - complex YAML features, anchors, inline lists, multiline, etc.
	// -----------------------------
	static class YamlSubsetParser
	{
		struct Line
		{
			public int indent;
			public string text;
			public int number;
		}

		public static bool TryParseMenu(string yamlText, string rootKey, out MenuNode root, out string error)
		{
			root = new MenuNode { id = rootKey, label = rootKey, weight = 1f, children = new List<MenuNode>() };
			error = null;

			var lines = Preprocess(yamlText);
			if (lines.Count == 0)
			{
				error = "Empty YAML.";
				return false;
			}

			int rootIdx = -1;
			for (int i = 0; i < lines.Count; i++)
			{
				if (IsKey(lines[i].text, rootKey))
				{
					rootIdx = i;
					break;
				}
			}

			if (rootIdx < 0)
			{
				error = $"Missing top-level key '{rootKey}:'";
				return false;
			}

			int baseIndent = lines[rootIdx].indent;
			int idx = rootIdx + 1;

			var children = new List<MenuNode>();

			while (idx < lines.Count)
			{
				if (lines[idx].indent <= baseIndent) break;

				if (!IsListItem(lines[idx].text))
				{
					idx++;
					continue;
				}

				if (!TryParseNode(lines, ref idx, out var node, out error))
					return false;

				children.Add(node);
			}

			root.children = children;
			return true;
		}

		static bool TryParseNode(List<Line> lines, ref int idx, out MenuNode node, out string error)
		{
			error = null;
			node = new MenuNode();

			var first = lines[idx];
			int itemIndent = first.indent;

			// "- key: value" inline
			string afterDash = first.text.TrimStart().Substring(2).Trim();
			if (!string.IsNullOrEmpty(afterDash))
			{
				if (!TryApplyKeyValue(afterDash, node, out error, first.number))
					return false;
			}

			idx++;

			// Consume indented properties until sibling/outdent
			while (idx < lines.Count)
			{
				var ln = lines[idx];

				// sibling or outdent => stop
				if (ln.indent <= itemIndent) break;

				// If we hit a list item directly, schema is broken (forgot children:)
				if (IsListItem(ln.text))
				{
					error = $"Line {ln.number}: Unexpected list item. Did you forget 'children:'?";
					return false;
				}

				if (TrySplitKeyValue(ln.text, out var key, out var value))
				{
					if (key == "children")
					{
						int childrenKeyIndent = ln.indent;
						idx++;

						var kids = new List<MenuNode>();
						while (idx < lines.Count)
						{
							var childLine = lines[idx];
							if (childLine.indent <= childrenKeyIndent) break;

							if (!IsListItem(childLine.text))
							{
								idx++;
								continue;
							}

							if (!TryParseNode(lines, ref idx, out var kid, out error))
								return false;

							kids.Add(kid);
						}

						node.children = kids;
						continue;
					}

					if (!ApplyScalar(key, value, node, out error, ln.number))
						return false;

					idx++;
					continue;
				}

				// Ignore unknown non key/value lines
				idx++;
			}

			// Defaults
			if (string.IsNullOrWhiteSpace(node.id))
				node.id = Guid.NewGuid().ToString("N");
			if (string.IsNullOrWhiteSpace(node.label))
				node.label = node.id;
			if (node.weight <= 0f)
				node.weight = 1f;
			node.children ??= new List<MenuNode>();

			return true;
		}

		static bool TryApplyKeyValue(string text, MenuNode node, out string error, int lineNumber)
		{
			error = null;
			if (!TrySplitKeyValue(text, out var key, out var value))
			{
				error = $"Line {lineNumber}: Expected 'key: value' after '-'. Got: {text}";
				return false;
			}

			return ApplyScalar(key, value, node, out error, lineNumber);
		}

		static bool ApplyScalar(string key, string value, MenuNode node, out string error, int lineNumber)
		{
			error = null;
			value = (value ?? "").Trim();

			switch (key)
			{
				case "id":
					node.id = Unquote(value);
					return true;

				case "label":
					node.label = Unquote(value);
					return true;

				case "description":
					node.description = Unquote(value);
					return true;

				case "weight":
					if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out var w))
					{
						error = $"Line {lineNumber}: weight must be a number. Got '{value}'";
						return false;
					}
					node.weight = w;
					return true;

				default:
					// Unknown keys allowed (forward compatible)
					return true;
			}
		}

		static bool TrySplitKeyValue(string text, out string key, out string value)
		{
			key = null;
			value = null;

			int c = text.IndexOf(':');
			if (c < 0) return false;

			key = text.Substring(0, c).Trim();
			value = (c + 1 < text.Length) ? text.Substring(c + 1).Trim() : "";
			return true;
		}

		static bool IsKey(string text, string key)
		{
			return text.Trim() == (key + ":");
		}

		static bool IsListItem(string text)
		{
			return text.TrimStart().StartsWith("- ");
		}

		static string Unquote(string s)
		{
			if (string.IsNullOrEmpty(s)) return s;
			s = s.Trim();
			if ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'")))
				return s.Substring(1, s.Length - 2);
			return s;
		}

		static List<Line> Preprocess(string yamlText)
		{
			var outLines = new List<Line>(256);
			if (string.IsNullOrEmpty(yamlText)) return outLines;

			var raw = yamlText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for (int i = 0; i < raw.Length; i++)
			{
				var s = raw[i];
				if (string.IsNullOrWhiteSpace(s)) continue;

				// strip simple comments
				int hash = s.IndexOf('#');
				if (hash >= 0) s = s.Substring(0, hash);
				if (string.IsNullOrWhiteSpace(s)) continue;

				int indent = 0;
				while (indent < s.Length && s[indent] == ' ') indent++;

				outLines.Add(new Line
				{
					indent = indent,
					text = s.TrimEnd(),
					number = i + 1
				});
			}

			return outLines;
		}
	}
}
