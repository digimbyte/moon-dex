using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
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
	public Aura.TextBlock leafTitle;
	public Aura.TextBlock leafBody;
	public Core.Animator.Animate leafPanelIn;
	public Core.Animator.Animate leafPanelOut;
	bool _leafPanelVisible;
	GameObject _backButton;
	MenuNode _selectedVarient;
	readonly List<Aura.UIBlockHit> _pointerUiHits = new List<Aura.UIBlockHit>();
	int _uiConsumedFrame = -1;
	internal bool RingInputConsumed => _uiConsumedFrame == Time.frameCount;

	internal bool BlocksRingInput(Ray ray)
	{
		if (RingInputConsumed) return true;
		Aura.Interaction.RaycastAll(ray, _pointerUiHits);
		foreach (var hit in _pointerUiHits)
		{
			var block = hit.UIBlock;
			if (block == null) continue;
			if ((leafInfoPanel != null && block.transform.IsChildOf(leafInfoPanel.transform))
				|| block.GetComponentInParent<AuraSamples.UIControls.Button>() != null)
			{
				_uiConsumedFrame = Time.frameCount;
				return true;
			}
		}
		return false;
	}

	[Header("Wiki Images")]
	public Core.Registry.Registry imageCache;
	public bool useLocalImages;
	[Tooltip("Folder mirroring the YAML category paths for local testing. Relative paths start at the project/build folder.")]
	public string localImageDirectory = "remote_assets";
	Aura.UIBlock2D _leafImage;
	Coroutine _imageLoad;
	UnityWebRequest _imageRequest;
	Texture2D _loadedImage;
	string _imageUrl;
	MenuNode _imageNode;
	ulong _imageGeneration;

	[Header("Fallback Input")]
	[Tooltip("If true, the builder will attach a fallback physics-raycast input forwarder when playing. Useful when UI elements intercept EventSystem raycasts.")]
	public bool enableFallbackPhysicsInput = true;

	[Header("Mouse Wheel Rotation")]
	[Tooltip("Angular velocity added per mouse-wheel step, in degrees per second.")]
	[Min(0f)] public float wheelImpulse = 180f;
	[Tooltip("Exponential momentum decay per second.")]
	[Min(0.01f)] public float wheelFriction = 4f;
	[Min(1f)] public float wheelMaximumSpeed = 720f;

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
	Transform _ringAlignmentPivot;
	readonly Dictionary<Transform, Material> _ringMaterials = new Dictionary<Transform, Material>();
	public int ActiveRingDepth { get; private set; } = -1;
	MaterialPropertyBlock _wedgeProperties;

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
		if (Application.isPlaying) UpdateSelectionStates();

		// Attach input forwarder if requested so clicks still work when UI blocks EventSystem raycasts.
		if (Application.isPlaying && enableFallbackPhysicsInput)
		{
			if (GetComponent<RadialMenuInputForwarder>() == null)
				gameObject.AddComponent<RadialMenuInputForwarder>();
		}
	}

	void OnDisable()
	{
		foreach (var audio in GetComponentsInChildren<RadialMenuSfx>(true)) audio.StopPlayback();
		foreach (var ring in GetComponentsInChildren<RadialRingRotation>(true)) ring.StopMomentum();
		StopAutoRebuildPoll();
		ClearLeafImage();
	}

	void OnDestroy()
	{
		ClearLeafImage();
		ClearRingsFromDepth(0, false);
		// Closing rings have already left _ringRoots but still own their meshes/materials.
		foreach (var ring in new List<Transform>(_ringMaterials.Keys))
			if (ring != null) DestroyGO(ring.gameObject);
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
		_uiConsumedFrame = Time.frameCount;
		if (_building || _navStack.Count <= 1) return;
		_building = true;
		try
		{
			var path = _navStack.ToArray();
			int parentDepth = _navStack.Count - 2;
			if (!useRings && !ReplaceLevel(path[1], parentDepth)) return;
			if (useRings) ClearRingsFromDepth(parentDepth + 1);
			_navStack.Pop();
			_selectedVarient = null;
			ActiveNodeId = _navStack.Count > 1 ? _navStack.Peek().id : null;
			UpdateSelectionStates();
			GetRingSfx(parentDepth)?.PlayBack();
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
		if (Application.isPlaying && RingInputConsumed) return;
		if (_building || item == null || !item.isActiveAndEnabled || item.Owner != this || !_menuItems.Contains(item)) return;
        if (item.Node == null || string.IsNullOrWhiteSpace(item.Node.id) || item.ParentDepth < 0) return;
        if (!Navigate(item.Node, item.ParentDepth)) return;
        item.GetComponentInParent<RadialRingRotation>()?.StopMomentum();
        GetRingSfx(item.ParentDepth)?.PlaySelection();
        if (item.IsLeaf && ActiveNodeId == item.NodeId)
            item.GetComponentInParent<RadialRingRotation>()?.FaceCamera(item.CenterAnchor, Camera.main);
	}

	bool Navigate(MenuNode node, int parentDepth)
	{
		_building = true;
		try
		{
			int depth = parentDepth + 1;
			_selectedVarient = null;
			if (node.children != null && node.children.Count > 0)
			{
				if (!ReplaceLevel(node, depth)) return false;
			}
			else ClearRingsFromDepth(depth);
			while (_navStack.Count - 1 > parentDepth) _navStack.Pop();
			_navStack.Push(node);
			ActiveNodeId = node.id;
			UpdateSelectionStates();
			return true;
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
		if (Application.isPlaying && depth == 0)
		{
			_ringAlignmentPivot = pending.GetComponentInChildren<ScreenPositionFollower>(true).pivotReference;
			foreach (var follower in FindObjectsByType<ScreenPositionFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (follower.gameObject.scene != gameObject.scene || follower.name != "MoonViewSetup") continue;
				follower.SetPivot(pending);
				break;
			}
		}
		_menuItems.AddRange(pendingItems);
		pending.GetComponentInChildren<RadialRingRotation>(true).GrowWedges();
		return true;
	}

	void UpdateSelectionStates()
	{
		if (Application.isPlaying)
		{
			if (_backButton == null)
			{
				foreach (var root in gameObject.scene.GetRootGameObjects())
				foreach (var button in root.GetComponentsInChildren<AuraSamples.UIControls.Button>(true))
				{
					if (button.name != "Back" || button.OnClicked == null) continue;
					for (int i = 0; i < button.OnClicked.GetPersistentEventCount(); i++)
						if (button.OnClicked.GetPersistentTarget(i) == this && button.OnClicked.GetPersistentMethodName(i) == nameof(Back))
							_backButton = button.gameObject;
				}
			}
			if (_backButton != null)
			{
				var backSort = _backButton.GetComponent<Aura.SortGroup>();
				if (backSort == null) backSort = _backButton.AddComponent<Aura.SortGroup>();
				backSort.RenderOverOpaqueGeometry = true;
				// The back button is a permanent top-level control. Keep it above
				// the temporary hovered-wedge queues (3997-3999), even when
				// ScreenSpace inherits its root's overlay queue (4000).
				backSort.RenderQueue = 5000;
				backSort.SortingOrder = short.MaxValue;
				backSort.enabled = true;
				_backButton.SetActive(_navStack.Count > 1);
			}
		}
		var selectedNode = _navStack.Count > 1 ? _navStack.Peek() : null;
		bool showInfo = IsLeafNode(selectedNode);
		if (showInfo) EnsureSelectedVarient(selectedNode);
		else _selectedVarient = null;
		var infoNode = showInfo ? (_selectedVarient ?? selectedNode) : null;
		if (Application.isPlaying) UpdateLeafImage(infoNode);
		if (showInfo)
		{
			if (Application.isPlaying && leafInfoPanel != null)
			{
				var panelSort = leafInfoPanel.GetComponent<Aura.SortGroup>();
				if (panelSort == null) panelSort = leafInfoPanel.AddComponent<Aura.SortGroup>();
				panelSort.RenderOverOpaqueGeometry = true;
				panelSort.RenderQueue = (int)RenderQueue.Overlay + 1;
				panelSort.SortingOrder = short.MaxValue;
				panelSort.enabled = true;
			}
			if (leafTitle != null) leafTitle.Text = infoNode.DisplayLabel;
			if (leafBody != null) leafBody.Text = infoNode.description ?? string.Empty;
		}
		UpdateVarientOptions(showInfo ? selectedNode : null);
		if (leafInfoPanel != null && showInfo != _leafPanelVisible)
		{
			_leafPanelVisible = showInfo;
			if (leafPanelIn != null) leafPanelIn.StopAllTweens();
			if (leafPanelOut != null) leafPanelOut.StopAllTweens();
			if (showInfo)
			{
				leafInfoPanel.SetActive(true);
				if (leafPanelIn != null) leafPanelIn.PlayByName("IN");
			}
			else if (leafPanelOut != null)
				leafPanelOut.PlayByName("OUT");
			else leafInfoPanel.SetActive(false);
		}
		// A leaf adds no ring: anchor the deepest existing ring, not stack count.
		int activeRingDepth = -1;
		for (int i = _ringRoots.Count - 1; i >= 0; i--)
			if (_ringRoots[i] != null) { activeRingDepth = i; break; }
		ActiveRingDepth = activeRingDepth;
		for (int i = 0; i <= activeRingDepth; i++)
		{
			if (_ringRoots[i] == null) continue;
			_ringRoots[i].GetComponentInChildren<RadialRingRotation>(true).SetScale(Vector3.one * Mathf.Pow(0.5f, activeRingDepth - i));
			_ringRoots[i].localPosition = useRings
				? transform.InverseTransformVector(ringOffsetPerDepth * (i - activeRingDepth))
				: Vector3.zero;
		}
		if (_wedgeProperties == null) _wedgeProperties = new MaterialPropertyBlock();
		foreach (var item in _menuItems)
		{
			if (item == null || item.MeshChild == null) continue;
			var renderer = item.MeshChild.GetComponent<MeshRenderer>();
			if (renderer == null) continue;
			renderer.GetPropertyBlock(_wedgeProperties);
			_wedgeProperties.SetFloat("_RingActive", item.ParentDepth == activeRingDepth ? 1f : 0f);
			renderer.SetPropertyBlock(_wedgeProperties);
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
		var selectedNodes = new HashSet<MenuNode>();

		if (path.Length >= 2)
		{
			activeId = path[0]?.id;

			// Every real node in the navigation path stays selected, including the
			// current node. This keeps the orange breadcrumb recursive across rings.
			for (int i = 0; i < path.Length - 1; i++)
			{
				if (path[i] != null) selectedNodes.Add(path[i]);
			}
		}

		for (int i = 0; i < _menuItems.Count; i++)
		{
			var m = _menuItems[i];
			if (!m) continue;

			var id = m.NodeId;
			if (string.IsNullOrWhiteSpace(id)) continue;

			m.SetActive(id == activeId);
			bool isLeaf = IsLeafNode(m.Node);
			// Orange is navigation history. A selected leaf only receives the lighter
			// leaf highlight and never the history color.
			m.SetSelected(selectedNodes.Contains(m.Node) && !isLeaf);
			m.SetLeafSelected(selectedNodes.Contains(m.Node) && isLeaf);
		}
	}

	static bool IsLeafNode(MenuNode node)
	{
		// Variants are inspector data, never navigation children. A node with
		// variants but no children must keep the normal leaf rendering path.
		return node != null && (node.children == null || node.children.Count == 0);
	}

	static bool HasVarients(MenuNode node)
	{
		return node != null && node.varients != null && node.varients.Count > 0;
	}

	void EnsureSelectedVarient(MenuNode leaf)
	{
		if (!HasVarients(leaf))
		{
			_selectedVarient = null;
			return;
		}

		if (_selectedVarient != null)
		{
			for (int i = 0; i < leaf.varients.Count; i++)
			{
				var candidate = leaf.varients[i];
				if (candidate == _selectedVarient ||
					(candidate != null && candidate.id == _selectedVarient.id))
					return;
			}
		}

		_selectedVarient = leaf.varients[0];
	}

	void UpdateLeafImage(MenuNode node)
	{
		if (_leafImage == null && leafInfoPanel != null)
			_leafImage = leafInfoPanel.transform.Find("Image")?.GetComponent<Aura.UIBlock2D>();
		if (_leafImage == null) return;
		string url = null;
		try { url = ResolveImageUrl(node); }
		catch (Exception error) { Debug.LogWarning($"Invalid wiki image location: {error.Message}", this); }
		if (url != null && url == _imageUrl && node == _imageNode) return;
		ClearLeafImage();
		if (string.IsNullOrEmpty(url) || !isActiveAndEnabled) return;
		_imageUrl = url;
		_imageNode = node;
		if (useLocalImages)
			_imageLoad = StartCoroutine(LoadLeafImage(url, _imageGeneration, _leafImage));
		else
			LoadCachedLeafImage(url, _imageGeneration, _leafImage);
	}

	bool IsCurrentImage(string url, ulong generation, Aura.UIBlock2D target)
	{
		return this != null && isActiveAndEnabled && generation == _imageGeneration
			&& _imageUrl == url && target != null && _leafImage == target;
	}

	async void LoadCachedLeafImage(string url, ulong generation, Aura.UIBlock2D target)
	{
		try
		{
			if (imageCache == null || imageCache.AssetType != Core.Registry.RegistryAssetType.Texture
				|| imageCache.RuntimeAccess != Core.Registry.RegistryRuntimeAccess.Cache)
				throw new InvalidOperationException("Assign a Texture registry with Cache (Read/Write) access to Image Cache.");
			var manager = Core.Registry.RegistryManager.Instance;
			if (manager == null)
				manager = new GameObject("Image Cache Worker").AddComponent<Core.Registry.RegistryManager>();
			var registered = manager.GetRegistry(imageCache.name);
			if (registered == null) manager.RegisterRegistry(imageCache);
			else if (registered != imageCache)
				throw new InvalidOperationException("A different registry already uses the Image Cache bucket name.");

			// The manager owns the request and texture, independently of this display.
			var queue = manager.GetComponent<WikiImageRequestQueue>();
			if (queue == null) queue = manager.gameObject.AddComponent<WikiImageRequestQueue>();
			var texture = await queue.GetTextureAsync(manager, imageCache.name, url);
			if (!IsCurrentImage(url, generation, target)) return;
			target.SetImage(texture);
			target.BodyEnabled = true;
			target.gameObject.SetActive(true);
		}
		catch (Exception error)
		{
			if (!IsCurrentImage(url, generation, target)) return;
			_imageUrl = null; // Allow the next selection to retry a failed request.
			Debug.LogWarning($"Could not load wiki image '{url}': {error.Message}", this);
		}
	}

	void UpdateVarientOptions(MenuNode leaf)
	{
		if (!Application.isPlaying || leafInfoPanel == null) return;
		var options = leafInfoPanel.transform.Find("Button_Options");
		if (options == null) return;
		var controller = options.GetComponent<VariantButtonPanelController>();
		if (controller == null) controller = options.gameObject.AddComponent<VariantButtonPanelController>();
		bool hasVarients = HasVarients(leaf);
		options.gameObject.SetActive(hasVarients);
		if (!hasVarients) return;
		var buttons = options.GetComponentsInChildren<AuraSamples.UIControls.Button>(true);
		int selectedIndex = 0;
		if (_selectedVarient != null)
		{
			for (int i = 0; i < leaf.varients.Count; i++)
				if (leaf.varients[i] == _selectedVarient || leaf.varients[i]?.id == _selectedVarient.id)
				{
					selectedIndex = i;
					break;
				}
		}
		for (int i = 0; i < buttons.Length; i++)
		{
			bool active = i < leaf.varients.Count;
			buttons[i].gameObject.SetActive(active);
			if (!active) continue;
			int index = i;
			buttons[i].OnClicked.RemoveAllListeners();
			buttons[i].OnClicked.AddListener(() => SelectVarient(index));
		}
		controller.Apply(buttons, selectedIndex);
	}

	public void SelectVarient(int index)
	{
		var leaf = _navStack.Count > 1 ? _navStack.Peek() : null;
		if (leaf == null || leaf.varients == null || index < 0 || index >= leaf.varients.Count) return;
		_selectedVarient = leaf.varients[index];
		// Variants are inspector data, not ring nodes; keep the parent leaf active.
		ActiveNodeId = leaf.id;
		UpdateSelectionStates();
		GetRingSfx(_navStack.Count - 2)?.PlaySelection();
	}

	string ResolveImageUrl(MenuNode node)
	{
		if (node == null || string.IsNullOrWhiteSpace(node.id)) return null;
		if (useLocalImages)
		{
			string relativePath = node.ImagePath ?? node.id + ".png";
			foreach (string segment in relativePath.Split('/'))
				if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment.Contains("\\"))
					throw new ArgumentException("Image path must contain valid entry IDs.");
			if (string.IsNullOrWhiteSpace(localImageDirectory)) return null;
			if (node.id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || node.id.Contains("/") || node.id.Contains("\\"))
				throw new ArgumentException("Entry ID must be a file name.");
			string folder = Path.IsPathRooted(localImageDirectory) ? localImageDirectory
				: Path.Combine(Application.dataPath, "..", localImageDirectory);
			return new Uri(Path.GetFullPath(Path.Combine(folder, relativePath))).AbsoluteUri;
		}
		string url = node.image;
		if (string.IsNullOrWhiteSpace(url)) return null;
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
			throw new ArgumentException("Images require an HTTP or HTTPS URL.");
		return url;
	}

	System.Collections.IEnumerator LoadLeafImage(string url, ulong generation, Aura.UIBlock2D target)
	{
		using (var request = UnityWebRequestTexture.GetTexture(url))
		{
			_imageRequest = request;
			try
			{
				request.timeout = 30;
				yield return request.SendWebRequest();
				if (!IsCurrentImage(url, generation, target)) yield break;
				if (request.result != UnityWebRequest.Result.Success)
				{
					_imageUrl = null;
					Debug.LogWarning($"Could not load wiki image '{url}': {request.error}", this);
				}
				else
				{
					_loadedImage = DownloadHandlerTexture.GetContent(request);
					_leafImage.SetImage(_loadedImage);
					_leafImage.BodyEnabled = true;
					_leafImage.gameObject.SetActive(true);
				}
			}
			finally { if (generation == _imageGeneration) _imageRequest = null; }
		}
		if (generation == _imageGeneration) _imageLoad = null;
	}

	void ClearLeafImage()
	{
		unchecked { _imageGeneration++; }
		_imageNode = null;
		if (_imageLoad != null) StopCoroutine(_imageLoad);
		_imageLoad = null;
		if (_imageRequest != null)
		{
			_imageRequest.Abort();
			_imageRequest.Dispose();
			_imageRequest = null;
		}
		_imageUrl = null;
		if (_leafImage != null)
		{
			_leafImage.ClearImage();
			_leafImage.BodyEnabled = false;
			_leafImage.gameObject.SetActive(false);
		}
		if (_loadedImage != null) Destroy(_loadedImage);
		_loadedImage = null;
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
		// Keep ring meshes in stable depth queues; icon/text overlays are assigned below.
		int ringQueue = (int)RenderQueue.Transparent + ringDepth * 50;
		var ringMaterial = new Material(wedgeMaterial) { renderQueue = ringQueue };
		_ringMaterials.Add(ringRoot, ringMaterial);
		var contents = ringRoot.GetComponentInChildren<RadialRingRotation>(true).transform;



		for (int i = 0; i < items.Count; i++)
		{
			var node = items[i];
			float w = Mathf.Max(0.0001f, node.weight);
			float wedgeDeg = (w / totalWeight) * 360f;

			// 1) Instantiate the behavior root prefab
			var rootGO = Instantiate(itemRootPrefab, contents);

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

			mr.sharedMaterial = ringMaterial;
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
			float halfArc = Mathf.Clamp(p.wedgeDegrees - p.neighborGapDegrees, 0.001f, 360f) * Mathf.Deg2Rad * 0.5f;
			if (_wedgeProperties == null) _wedgeProperties = new MaterialPropertyBlock();
			mr.GetPropertyBlock(_wedgeProperties);
			_wedgeProperties.SetVector("_WedgeShape", new Vector4(p.innerDiameter * 0.5f, p.radialThickness, p.extrusionDepth, halfArc));
			_wedgeProperties.SetFloat("_WedgeCenter", (p.offsetDegrees + p.wedgeDegrees * 0.5f) * Mathf.Deg2Rad);
			mr.SetPropertyBlock(_wedgeProperties);
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
			// Match the visible shell's inward winding so its back face is
			// hittable through the open front, without adding invisible faces.
			hitMesh.triangles = triangles;
			hitMesh.RecalculateNormals();

			// 6.5) Create anchor transforms for text/UI placement
			if (createAnchors)
			{
				CreateAnchors(rootGO.transform, p);
			}

			// 7) Bind data to the prefab root's behavior scripts
			BindToHost(rootGO, node, i, cursor, wedgeDeg);

			// 8) If the item has MenuSetup, call its post-build hook (anchors/mesh now exist)
			TryPostBuildSetup(rootGO, meshChild, ringDepth, pendingItems);
			// Keep the collider and event receiver on the same object. The fallback
			// raycaster resolves RadialMenuMeshEvents from the hit collider.
			var hitCollider = hitArea.AddComponent<MeshCollider>();
			hitCollider.convex = false;
			hitCollider.sharedMesh = hitMesh;
			hitArea.AddComponent<RadialMenuMeshEvents>().Initialize(pendingItems[pendingItems.Count - 1]);

			// 8.5) Place the prefab's separate icon and text siblings at the wedge center.
			if (!string.IsNullOrEmpty(centerAnchorName))
			{
				var center = rootGO.transform.Find(centerAnchorName);
				var uiBlock = rootGO.transform.Find("UIBlock2D");
				if (uiBlock != null && center != null)
				{
					// Anchor text and icon halfway through the ring's extrusion.
					float faceZ = Mathf.Max(0.0001f, p.extrusionDepth) * 0.5f;
					Vector3 labelPosition = center.localPosition;
					labelPosition.z = faceZ;
					Vector3 visualLocalPosition = labelPosition;
					var block = uiBlock.GetComponent<Aura.UIBlock2D>();
					var iconSort = uiBlock.GetComponent<Aura.SortGroup>();
					if (iconSort == null) iconSort = uiBlock.gameObject.AddComponent<Aura.SortGroup>();
					// Keep each ring's UI inside its 50-wide render band so lower-ring
					// content cannot draw through the active ring's wedge.
					iconSort.RenderOverOpaqueGeometry = true;
					iconSort.RenderQueue = ringQueue + 1;
					iconSort.SortingOrder = ringDepth;
					iconSort.enabled = true;
					float midRadius = p.innerDiameter * 0.5f + p.radialThickness * 0.5f;
					float labelWidth = Mathf.Max(0.001f, midRadius * Mathf.Max(0.001f, p.wedgeDegrees - p.neighborGapDegrees) * Mathf.Deg2Rad * 0.9f);
					bool isLeaf = IsLeafNode(node);
					float labelHeight = p.radialThickness * 0.8f;
					if (isLeaf)
					{
						// A straight billboard must fit the wedge's chord, not its longer arc.
						float halfAngle = Mathf.Min(180f, Mathf.Max(0.001f, p.wedgeDegrees - p.neighborGapDegrees)) * Mathf.Deg2Rad * 0.5f;
						labelWidth = Mathf.Max(0.001f, 2f * midRadius * Mathf.Sin(halfAngle) * 0.9f);
						labelWidth = Mathf.Min(labelWidth, p.radialThickness * 0.8f);
						labelHeight = Mathf.Min(labelHeight, labelWidth);
					}
					float iconSize = Mathf.Max(0.001f, Mathf.Min(labelWidth, labelHeight));
					var tracker = uiBlock.GetComponent<TrackCamera>();
					if (tracker == null) tracker = uiBlock.gameObject.AddComponent<TrackCamera>();
					tracker.enabled = true;
					tracker.LockScreenAxes = true;
					if (block != null)
					{
						var registry = RegistrySingleton.Instance;
						Texture2D icon = registry != null ? registry.GetIcon(node.icon) : null;
						var iconMask = uiBlock.GetComponent<Aura.ClipMask>();
						if (iconMask != null)
							iconMask.Mask = icon;
						block.Size.X = iconSize;
						block.Size.Y = iconSize;
						layoutActions.Add(() => block.TrySetLocalPosition(visualLocalPosition));
					}

					var text = rootGO.transform.Find("TextBlock")?.GetComponent<Aura.TextBlock>()
						?? uiBlock.GetComponentInChildren<Aura.TextBlock>(true);
					if (text != null)
					{
						layoutActions.Add(() =>
						{
							text.TrySetLocalPosition(visualLocalPosition);
							text.CalculateLayout();
						});
						text.Text = node.label;
						// Share the label/icon sort group with the existing delayed hover override.
						var textSort = text.GetComponent<Aura.SortGroup>();
						if (textSort == null) textSort = text.gameObject.AddComponent<Aura.SortGroup>();
						textSort.RenderOverOpaqueGeometry = true;
						textSort.RenderQueue = ringQueue + 2;
						textSort.SortingOrder = ringDepth + 1;
						textSort.enabled = true;
						// Nova passes its capped layout size to TMP as the available text bounds.
						float textWidth = labelWidth / Mathf.Max(0.0001f, Mathf.Abs(text.transform.localScale.x));
						float textHeight = labelHeight / Mathf.Max(0.0001f, Mathf.Abs(text.transform.localScale.y));
						text.Size.X = textWidth;
						text.Size.Y = textHeight;
						text.SizeMinMax.X.Min = 0f;
						text.SizeMinMax.X.Max = textWidth;
						text.SizeMinMax.Y.Min = 0f;
						text.SizeMinMax.Y.Max = textHeight;
						text.TMP.fontSizeMax = text.TMP.fontSize;
						text.TMP.fontSizeMin = 0.01f;
						text.TMP.enableAutoSizing = true;
						text.TMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
						text.TMP.overflowMode = TMPro.TextOverflowModes.Truncate;
						text.AutoSize.X = Aura.AutoSize.Shrink;
						text.AutoSize.Y = Aura.AutoSize.Shrink;
						text.TMP.alignment = TMPro.TextAlignmentOptions.Center;
						text.Alignment.X = 0;
						text.Alignment.Y = 0;
						if (isLeaf)
						{
							text.transform.localRotation = Quaternion.identity;
							var textTracker = text.GetComponent<TrackCamera>();
							if (textTracker == null) textTracker = text.gameObject.AddComponent<TrackCamera>();
							textTracker.enabled = true;
							textTracker.LockScreenAxes = true;
							textTracker.LeafText = text;
							// CenterAnchor axes are tangent, extrusion, and radial respectively.
							textTracker.LeafLabelBounds = new Vector3(labelWidth, p.extrusionDepth * 0.8f, p.radialThickness * 0.8f);
						}
						else
						{
							var curved = text.gameObject.AddComponent<CurvedMenuText>();
							curved.Configure(text.TMP, rootGO.transform, midRadius, p.offsetDegrees + p.wedgeDegrees * 0.5f, faceZ);
						}
					}
				}
			}
			cursor += wedgeDeg;
		}
	}

	void EnsureButtonType(GameObject rootGO, MenuNode node)
	{
		if (rootGO == null) return;

		bool isLeaf = IsLeafNode(node);

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

	RadialMenuSfx GetRingSfx(int depth)
	{
		for (int i = Mathf.Min(depth, _ringRoots.Count - 1); i >= 0; i--)
			if (_ringRoots[i] != null) return _ringRoots[i].GetComponent<RadialMenuSfx>();
		return null;
	}

	Transform CreateRingRoot(int depth)
	{
		var go = new GameObject(string.IsNullOrEmpty(ringRootNamePrefix) ? $"Ring_{depth}" : ringRootNamePrefix + depth);
		go.SetActive(false);
		go.transform.SetParent(transform, false);
		var ringAudio = go.AddComponent<RadialMenuSfx>();
		ringAudio.CopySettingsFrom(GetComponent<RadialMenuSfx>());
		var ratchet = go.AddComponent<SfxRatchet>();
		ratchet.CopySettingsFrom(GetComponent<SfxRatchet>());
		go.transform.localPosition = useRings
			? transform.InverseTransformPoint(transform.position + ringOffsetBase + ringOffsetPerDepth * depth)
			: Vector3.zero;
		var gimbal = new GameObject("Gimbal").transform;
		gimbal.SetParent(go.transform, false);
		// Keep the lateral correction outside the ring's rotation and depth scaling.
		var contents = new GameObject("Contents").transform;
		contents.SetParent(gimbal, false);
		contents.gameObject.AddComponent<RadialRingRotation>().Initialize(this);
		var follower = gimbal.gameObject.AddComponent<ScreenPositionFollower>();
		follower.sourceCamera = Camera.main;
		follower.pivotReference = transform;
		foreach (var source in FindObjectsByType<ScreenPositionFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (source.gameObject.scene != gameObject.scene || source.name != "MoonViewSetup") continue;
			follower.sourceCamera = source.sourceCamera;
			// The moon follows Ring_0; rings retain their original alignment target.
			follower.pivotReference = _ringAlignmentPivot != null ? _ringAlignmentPivot : source.pivotReference;
			follower.pixelOffset = source.pixelOffset;
			follower.boundsPercentOffset = source.boundsPercentOffset;
			follower.limitBox = source.limitBox;
			follower.boxSize = source.boxSize;
			follower.limitDistance = source.limitDistance;
			follower.maximumDisplacement = source.maximumDisplacement;
			break;
		}
		follower.alignmentAnchor = gimbal;
		follower.placement = ScreenPositionFollower.PlacementMode.OriginDepthPlane;
		follower.elasticPercent = 25f;
		follower.moveX = true;
		follower.moveY = false;
		follower.moveZ = false;
		follower.CaptureOrigin();
		return go.transform;
	}

	void ClearRingsFromDepth(int depth, bool animate = true)
	{
		depth = Mathf.Max(0, depth);

		for (int i = _ringRoots.Count - 1; i >= depth; i--)
		{
			var t = _ringRoots[i];
			if (t != null)
			{
				if (animate && Application.isPlaying && t.gameObject.activeInHierarchy)
				{
					foreach (var button in t.GetComponentsInChildren<MenuButtonBase>(true)) button.Retire();
					foreach (var collider in t.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
					t.GetComponentInChildren<RadialRingRotation>(true).Collapse(() => DestroyGO(t.gameObject));
				}
				else DestroyGO(t.gameObject);
			}
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
		if (_ringMaterials.TryGetValue(go.transform, out var ringMaterial))
		{
			_ringMaterials.Remove(go.transform);
			if (Application.isPlaying) Destroy(ringMaterial);
			else DestroyImmediate(ringMaterial);
		}
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
		public string title;
		public int gen;
		public string description;
		public string gender;
		public string DisplayLabel
		{
			get
			{
				string infoTitle = string.IsNullOrWhiteSpace(title) ? label : title;
				if (string.IsNullOrWhiteSpace(gender)) return infoTitle;
				string symbols = string.Empty;
				foreach (string value in gender.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
				{
					if (string.Equals(value, "male", StringComparison.OrdinalIgnoreCase)) symbols += "\u2642";
					else if (string.Equals(value, "female", StringComparison.OrdinalIgnoreCase)) symbols += "\u2640";
				}
				return symbols.Length == 0 ? infoTitle : infoTitle + " " + symbols;
			}
		}
		public string image;
		internal string ImagePath;
		public string icon;
		public float weight = 1f;
		public List<MenuNode> children = new List<MenuNode>();
		public List<MenuNode> varients = new List<MenuNode>();
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
			AssignImagePaths(root, string.Empty);
			GroupLargePacks(root);
			return true;
		}

		static void GroupLargePacks(MenuNode parent)
		{
			foreach (var child in parent.children) GroupLargePacks(child);
			parent.children = GroupRange(parent.children, 0, parent.children.Count, parent.id, parent.icon);
		}


		static List<MenuNode> GroupRange(List<MenuNode> entries, int start, int count, string parentId, string icon)
		{
			if (count <= 12) return entries.GetRange(start, count);
			int groups = Math.Min(8, Math.Max(2, (count + 4) / 8));
			int size = count / groups;
			int extra = count % groups;
			var result = new List<MenuNode>(groups);
			int offset = start;
			for (int i = 0; i < groups; i++)
			{
				int length = size + (i < extra ? 1 : 0);
				int end = offset + length;
				result.Add(new MenuNode
				{
					id = parentId + "_range_" + (offset + 1) + "_" + end,
					label = (offset + 1) + "-" + end,
					icon = icon,
					children = GroupRange(entries, offset, length, parentId, icon)
				});
				offset = end;
			}
			return result;
		}

		static void AssignImagePaths(MenuNode parent, string prefix)
		{
			foreach (var node in parent.children)
			{
				string path = prefix + node.id;
				node.ImagePath = path + ".png";
				AssignImagePaths(node, path + "/");
			}
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
					if (key == "children" || key == "varients")
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

						if (key == "varients") node.varients = kids;
						else node.children = kids;
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
					node.label = Unquote(value).Replace("/n", "\n");
					return true;
				case "title":
					node.title = value == "~" || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
						? null : Unquote(value).Replace("/n", "\n");
					return true;
				case "gen":
					if (!int.TryParse(Unquote(value), out node.gen) || node.gen < 1)
					{
						error = $"Line {lineNumber}: gen must be a positive integer. Got '{value}'";
						return false;
					}
					return true;

				case "description":
					node.description = Unquote(value);
					return true;
				case "Gender":
				case "gender":
					node.gender = value == "~" || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
						? null : Unquote(value);
					return true;
				case "image":
					node.image = value == "~" || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
						? null : Unquote(value);
					return true;

				case "icon":
					// YAML refers directly to an entry UID in the assigned Icons registry.
					var iconValue = value == "~" || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
						? string.Empty : Unquote(value).Trim();
					node.icon = iconValue.ToLowerInvariant();
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
			if (s.StartsWith("'") && s.EndsWith("'"))
				return s.Substring(1, s.Length - 2).Replace("''", "'");
			if (s.StartsWith("\"") && s.EndsWith("\""))
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
