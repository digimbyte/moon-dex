using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared base for YAML-driven buttons.
/// - Receives YAML node data via IRadialMenuItemHost.Bind
/// - After the procedural mesh + anchors are created, RadialMenuFromYaml calls PostBuildSetup()
/// - Hover/click are forwarded from the mesh child via RadialMenuMeshEvents
/// </summary>
public abstract class MenuButtonBase : MonoBehaviour, IRadialMenuItemHost
{
	[Header("Runtime (debug)")]
	[SerializeField] private bool debugLog;

	public RadialMenuFromYaml Owner { get; private set; }
	public int ParentDepth { get; private set; } = -1; // ring depth that spawned this item
	public int NodeDepth => ParentDepth + 1;

	public RadialMenuFromYaml.MenuNode Node { get; private set; }
	public string NodeId => Node?.id;
	public string Label => Node?.label;

	public bool HasChildren => Node != null && Node.children != null && Node.children.Count > 0;
	public bool IsLeaf => !HasChildren;

	public Transform MeshChild { get; private set; }
	public Transform CenterAnchor { get; private set; }
	public Transform StartCapAnchor { get; private set; }
	public Transform EndCapAnchor { get; private set; }

	public bool IsSelected { get; private set; }
	public bool IsLeafSelected { get; private set; }
	public bool IsActive { get; private set; }
	public bool IsHovered { get; private set; }

	[Header("Visual Feedback")]
	[SerializeField] private bool enableHoverScale = true;
	[SerializeField] private float hoverScale = 1.05f;
	[SerializeField] private float hoverLerpSpeed = 12f;
	[SerializeField] private bool enableClickOffset = true;
	[SerializeField] private float clickOffset = 0.02f;
	[SerializeField] private float clickResetDelay = 0.08f;

	[Header("Vertex Displacement")]
	[SerializeField] private bool enableVertexDisplacement = true;
	[SerializeField] private float vertexDisplacement = 0.02f;

	[SerializeField] private bool enableHoverTint = false;
	[SerializeField] private Color hoverTint = new Color(0.95f, 0.95f, 0.95f, 1f);
	[SerializeField] private bool enableClickTint = false;
	[SerializeField] private Color clickTint = new Color(0.85f, 0.85f, 0.85f, 1f);
	[SerializeField] private float tintLerpSpeed = 12f;

	// runtime visual state
	private Vector3 _meshOrigScale = Vector3.one;
	private Vector3 _meshTargetScale = Vector3.one;
	private Vector3 _meshOrigLocalPos = Vector3.zero;
	private Vector3 _meshTargetLocalPos = Vector3.zero;
	private bool _visualsInitialized = false;
	private Coroutine _clickResetCo;

	// tinting
	private Renderer _meshRenderer;
	private MaterialPropertyBlock _mpb;
	private Color _meshOrigColor = Color.white;
	private Color _meshTargetColor = Color.white;
	private Color _meshCurrentColor = Color.white;

	// vertex displacement
	private Mesh _runtimeMesh;
	private Vector3[] _origVerts;
	private Vector3[] _displacedVerts;
	private bool _vertsInitialized = false;
	private bool _retired;
	private Material _restingWedgeMaterial;
	private Material _hoverWedgeMaterial;
	private int _hoverRenderQueue;
	private float _hoverFrontTime = -1f;
	private Aura.SortGroup _iconSort;
	private Aura.SortGroup _textSort;
	private bool _labelSortCaptured;
	private readonly System.Collections.Generic.List<LabelSortState> _labelSortStates = new System.Collections.Generic.List<LabelSortState>();

	struct LabelSortState
	{
		public Aura.SortGroup Group;
		public bool Enabled;
		public bool RenderOverOpaque;
		public int SortingOrder;
		public int RenderQueue;
	}

	const float HoverDelaySeconds = 1f;
	const int HoverRenderQueueOffset = 2000;

	void OnDisable()
	{
		_hoverFrontTime = -1f;
		SetWedgeInFront(false);
		SetLabelInFront(false);
		IsHovered = false;
	}

	void OnDestroy()
	{
		SetWedgeInFront(false);
	}

	internal void Retire()
	{
		_hoverFrontTime = -1f;
		SetWedgeInFront(false);
		SetLabelInFront(false);
		_retired = true;
		StopAllCoroutines();
		_clickResetCo = null;
		_visualsInitialized = false;
		_vertsInitialized = false;
		_runtimeMesh = null;
		_origVerts = null;
		_displacedVerts = null;
		IsHovered = false;
	}

	public virtual void Bind(RadialMenuFromYaml.MenuNode node, int index, float startDeg, float wedgeDeg)
	{
		Node = node;
		if (debugLog)
			Debug.Log($"[{GetType().Name}] Bind id='{node?.id}' label='{node?.label}' children={node?.children?.Count ?? 0}", this);
		OnBound(node, index, startDeg, wedgeDeg);
	}

	protected virtual void OnBound(RadialMenuFromYaml.MenuNode node, int index, float startDeg, float wedgeDeg) { }

	/// <summary>
	/// Called by RadialMenuFromYaml after the mesh and anchors exist.
	/// </summary>
	public void PostBuildSetup(
		RadialMenuFromYaml owner,
		int parentDepth,
		Transform meshChild,
		Transform centerAnchor,
		Transform startCapAnchor,
		Transform endCapAnchor)
	{
		Owner = owner;
		ParentDepth = parentDepth;
		MeshChild = meshChild;
		CenterAnchor = centerAnchor;
		StartCapAnchor = startCapAnchor;
		EndCapAnchor = endCapAnchor;

		EnsureMeshInteraction();
		OnPostBuild();
		InitializeVisuals();
	}

	protected virtual void OnPostBuild() { }

	void InitializeVisuals()
	{
		if (MeshChild == null) return;
		_meshOrigScale = MeshChild.localScale;
		_meshTargetScale = _meshOrigScale;
		_meshOrigLocalPos = MeshChild.localPosition;
		_meshTargetLocalPos = _meshOrigLocalPos;

		// Setup renderer + property block for tinting
		_meshRenderer = MeshChild.GetComponent<MeshRenderer>();
		_mpb = new MaterialPropertyBlock();
		if (_meshRenderer != null)
		{
			var mat = _meshRenderer.sharedMaterial;
			if (mat != null)
			{
				if (mat.HasProperty("_Color")) _meshOrigColor = mat.color;
				else if (mat.HasProperty("_BaseColor")) _meshOrigColor = mat.GetColor("_BaseColor");
				else _meshOrigColor = Color.white;
			}
			_meshCurrentColor = _meshOrigColor;
			_meshTargetColor = _meshOrigColor;
			_meshRenderer.GetPropertyBlock(_mpb);
			_mpb.SetColor("_Color", _meshCurrentColor);
			_mpb.SetColor("_BaseColor", _meshCurrentColor);
			ApplyWedgeState();
			_meshRenderer.SetPropertyBlock(_mpb);
		}


		// Initialize optional vertex displacement buffers
		if (enableVertexDisplacement)
		{
			var mf = MeshChild.GetComponent<MeshFilter>();
			if (mf != null && mf.sharedMesh != null)
			{
				_runtimeMesh = mf.sharedMesh;
				_origVerts = _runtimeMesh.vertices;
				// ensure normals exist
				if (_runtimeMesh.normals == null || _runtimeMesh.normals.Length != _origVerts.Length)
					_runtimeMesh.RecalculateNormals();
				var norms = _runtimeMesh.normals;
				_displacedVerts = new Vector3[_origVerts.Length];
				for (int i = 0; i < _origVerts.Length; i++)
				{
					_displacedVerts[i] = _origVerts[i] + norms[i] * vertexDisplacement;
				}
				_vertsInitialized = true;
				Debug.Log($"[MenuButtonBase] Vertex displacement initialized for id='{NodeId}'", this);
			}
		}

		_visualsInitialized = true;
	}

	private void LateUpdate()
	{
		if (!_visualsInitialized || MeshChild == null) return;
		if (IsHovered && _hoverFrontTime >= 0f && Time.unscaledTime >= _hoverFrontTime)
		{
			_hoverFrontTime = -1f;
			SetWedgeInFront(true);
			SetLabelInFront(true);
		}
		float t = Mathf.Clamp01(hoverLerpSpeed * Time.deltaTime);
		MeshChild.localScale = Vector3.Lerp(MeshChild.localScale, _meshTargetScale, t);
		MeshChild.localPosition = Vector3.Lerp(MeshChild.localPosition, _meshTargetLocalPos, t);

		if (_meshRenderer != null && _mpb != null)
		{
			float ct = Mathf.Clamp01(tintLerpSpeed * Time.deltaTime);
			_meshCurrentColor = Color.Lerp(_meshCurrentColor, _meshTargetColor, ct);
			_meshRenderer.GetPropertyBlock(_mpb);
			_mpb.SetColor("_Color", _meshCurrentColor);
			_mpb.SetColor("_BaseColor", _meshCurrentColor);
			ApplyWedgeState();
			_meshRenderer.SetPropertyBlock(_mpb);
		}
	}

	void EnsureMeshInteraction()
	{
		if (MeshChild == null) return;

		var events = MeshChild.GetComponent<RadialMenuMeshEvents>();
		if (events == null) events = MeshChild.gameObject.AddComponent<RadialMenuMeshEvents>();
		events.Initialize(this);
	}

	// Called by RadialMenuMeshEvents
	public void OnMeshClicked(RadialMenuMeshEvents source)
	{
		if (_retired || !isActiveAndEnabled) return;
		if (Application.isPlaying && Owner != null && Owner.RingInputConsumed) return;
		if (debugLog) Debug.Log($"[MenuButtonBase] OnMeshClicked id='{NodeId}'", this);
		Owner?.HandleItemClicked(this);
		if (_retired || !isActiveAndEnabled) return;
		OnClickLocal();
	}

	// Called by RadialMenuMeshEvents
	public void OnMeshHoverEnter(RadialMenuMeshEvents source)
	{
		if (_retired || !isActiveAndEnabled || IsHovered) return;
		if (debugLog) Debug.Log($"[MenuButtonBase] OnMeshHoverEnter id='{NodeId}'", this);
		IsHovered = true;
		ApplyWedgeState();
		_hoverFrontTime = Time.unscaledTime + HoverDelaySeconds;
		OnHoverEnterLocal();
	}

	// Called by RadialMenuMeshEvents
	public void OnMeshHoverExit(RadialMenuMeshEvents source)
	{
		if (_retired || !isActiveAndEnabled) return;
		if (debugLog) Debug.Log($"[MenuButtonBase] OnMeshHoverExit id='{NodeId}'", this);
		IsHovered = false;
		ApplyWedgeState();
		_hoverFrontTime = -1f;
		SetWedgeInFront(false);
		SetLabelInFront(false);
		OnHoverExitLocal();
	}

	void SetWedgeInFront(bool elevated)
	{
		if (_meshRenderer == null) return;

		if (elevated)
		{
			if (_hoverWedgeMaterial != null) return;
			var material = _meshRenderer.sharedMaterial;
			if (material == null) return;

			_restingWedgeMaterial = material;
			// ScreenSpace controls inherit their root's overlay queue, including Back.
			// Elevate mesh, icon, and text below that root, in three consecutive slots.
			_hoverRenderQueue = Mathf.Min((int)RenderQueue.Overlay - 3, material.renderQueue + HoverRenderQueueOffset);
			_hoverWedgeMaterial = new Material(material)
			{
				name = $"{material.name} (Section Hover)",
				renderQueue = _hoverRenderQueue
			};
			if (_hoverWedgeMaterial.HasProperty("_ZTest"))
				_hoverWedgeMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
			if (_hoverWedgeMaterial.HasProperty("_ZWrite"))
				_hoverWedgeMaterial.SetInt("_ZWrite", 0);
			_meshRenderer.sharedMaterial = _hoverWedgeMaterial;
		}
		else if (_hoverWedgeMaterial != null)
		{
			_meshRenderer.sharedMaterial = _restingWedgeMaterial;
			if (Application.isPlaying) Destroy(_hoverWedgeMaterial);
			else DestroyImmediate(_hoverWedgeMaterial);
			_hoverWedgeMaterial = null;
			_restingWedgeMaterial = null;
			_hoverRenderQueue = 0;
		}
	}

	void CaptureLabelSortGroups()
	{
		if (_labelSortCaptured) return;
		var iconBlock = transform.Find("UIBlock2D");
		_iconSort = iconBlock == null ? null : iconBlock.GetComponent<Aura.SortGroup>();
		var textBlock = GetComponentInChildren<Aura.TextBlock>(true);
		_textSort = textBlock == null ? null : textBlock.GetComponent<Aura.SortGroup>();
		foreach (var group in GetComponentsInChildren<Aura.SortGroup>(true))
		{
			if (group == null) continue;
			_labelSortStates.Add(new LabelSortState
			{
				Group = group,
				Enabled = group.enabled,
				RenderOverOpaque = group.RenderOverOpaqueGeometry,
				SortingOrder = group.SortingOrder,
				RenderQueue = group.RenderQueue
			});
		}
		_labelSortCaptured = true;
	}

	void SetLabelInFront(bool elevated)
	{
		// Capture only when elevating, after ring setup has assigned label queues.
		// Disable/retire before the first hover must not cache prefab defaults.
		if (elevated) CaptureLabelSortGroups();
		else if (!_labelSortCaptured) return;
		for (int i = 0; i < _labelSortStates.Count; i++)
		{
			var state = _labelSortStates[i];
			var group = state.Group;
			if (group == null) continue;
			if (elevated)
			{
				group.RenderOverOpaqueGeometry = true;
				group.SortingOrder = group == _iconSort
					? short.MaxValue - 2
					: group == _textSort
						? short.MaxValue - 1
						: short.MaxValue - 3 + i;
				int contentQueue = group == _textSort ? _hoverRenderQueue + 2 : _hoverRenderQueue + 1;
				group.RenderQueue = Mathf.Min((int)RenderQueue.Overlay - 1, contentQueue);
				group.enabled = true;
			}
			else
			{
				group.enabled = state.Enabled;
				group.RenderOverOpaqueGeometry = state.RenderOverOpaque;
				group.SortingOrder = state.SortingOrder;
				group.RenderQueue = state.RenderQueue;
			}
		}
	}

	// State (set by RadialMenuFromYaml)
	public void SetSelected(bool selected)
	{
		IsSelected = selected;
		OnSelectedChanged(selected);
		ApplyWedgeState();
	}

	public void SetLeafSelected(bool selected)
	{
		IsLeafSelected = selected;
		ApplyWedgeState();
	}

	public void SetActive(bool active)
	{
		IsActive = active;
		OnActiveChanged(active);
		ApplyWedgeState();
	}

	void ApplyWedgeState()
	{
		if (_meshRenderer == null || _mpb == null) return;
		_mpb.SetFloat("_WedgeHovered", IsHovered ? 1f : 0f);
		_mpb.SetFloat("_WedgeSelected", IsSelected ? 1f : 0f);
		_mpb.SetFloat("_WedgeLeafSelected", IsLeafSelected ? 1f : 0f);
	}

	// ----- Override points for per-item visuals/behavior -----
	protected virtual void OnClickLocal()
	{
		// small press feedback
		if (!_visualsInitialized) return;
		if (enableClickOffset)
		{
			if (_clickResetCo != null) StopCoroutine(_clickResetCo);
			_meshTargetLocalPos = _meshOrigLocalPos + new Vector3(0f, 0f, -Mathf.Abs(clickOffset));
			_clickResetCo = StartCoroutine(ResetClickAfterDelay(clickResetDelay));
		}

		if (enableClickTint && _meshRenderer != null)
		{
			_meshTargetColor = clickTint;
			if (_clickResetCo == null) _clickResetCo = StartCoroutine(ResetClickAfterDelay(clickResetDelay));
		}

		// Apply a stronger vertex displacement on click if enabled
		if (enableVertexDisplacement && _vertsInitialized && _runtimeMesh != null)
		{
			var verts = new Vector3[_origVerts.Length];
			for (int i = 0; i < verts.Length; i++)
			{
				verts[i] = _origVerts[i] + (_runtimeMesh.normals[i] * (vertexDisplacement * 1.5f));
			}
			_runtimeMesh.vertices = verts;
			_runtimeMesh.RecalculateBounds();
		}
	}

	protected virtual void OnHoverEnterLocal()
	{
		if (!_visualsInitialized) return;
		if (enableHoverScale)
			_meshTargetScale = _meshOrigScale * Mathf.Max(0f, hoverScale);

		if (enableHoverTint && _meshRenderer != null)
			_meshTargetColor = hoverTint;

		// apply vertex displacement on hover
		if (enableVertexDisplacement && _vertsInitialized && _runtimeMesh != null)
		{
			_runtimeMesh.vertices = _displacedVerts;
			_runtimeMesh.RecalculateBounds();
		}
	}

	protected virtual void OnHoverExitLocal()
	{
		if (!_visualsInitialized) return;
		if (enableHoverScale)
			_meshTargetScale = _meshOrigScale;

		if (enableHoverTint && _meshRenderer != null)
			_meshTargetColor = _meshOrigColor;

		// restore original vertices
		if (enableVertexDisplacement && _vertsInitialized && _runtimeMesh != null)
		{
			_runtimeMesh.vertices = _origVerts;
			_runtimeMesh.RecalculateBounds();
		}
	}

	protected virtual void OnSelectedChanged(bool selected) { }
	protected virtual void OnActiveChanged(bool active) { }

	System.Collections.IEnumerator ResetClickAfterDelay(float delay)
	{
		yield return new WaitForSeconds(Mathf.Max(0f, delay));
		_meshTargetLocalPos = _meshOrigLocalPos;
		if (_meshRenderer != null)
			_meshTargetColor = _meshOrigColor;
		_clickResetCo = null;

		// restore original vertices after click
		if (enableVertexDisplacement && _vertsInitialized && _runtimeMesh != null)
		{
			_runtimeMesh.vertices = _origVerts;
			_runtimeMesh.RecalculateBounds();
		}
	}
}
