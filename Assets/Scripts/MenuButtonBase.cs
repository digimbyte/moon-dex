using UnityEngine;

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

	internal void Retire()
	{
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
		float t = Mathf.Clamp01(hoverLerpSpeed * Time.deltaTime);
		MeshChild.localScale = Vector3.Lerp(MeshChild.localScale, _meshTargetScale, t);
		MeshChild.localPosition = Vector3.Lerp(MeshChild.localPosition, _meshTargetLocalPos, t);

		if (_meshRenderer != null)
		{
			float ct = Mathf.Clamp01(tintLerpSpeed * Time.deltaTime);
			_meshCurrentColor = Color.Lerp(_meshCurrentColor, _meshTargetColor, ct);
			_meshRenderer.GetPropertyBlock(_mpb);
			_mpb.SetColor("_Color", _meshCurrentColor);
			_mpb.SetColor("_BaseColor", _meshCurrentColor);
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
		if (debugLog) Debug.Log($"[MenuButtonBase] OnMeshClicked id='{NodeId}'", this);
		Owner?.HandleItemClicked(this);
		if (_retired || !isActiveAndEnabled) return;
		OnClickLocal();
	}

	// Called by RadialMenuMeshEvents
	public void OnMeshHoverEnter(RadialMenuMeshEvents source)
	{
		if (_retired || !isActiveAndEnabled) return;
		if (debugLog) Debug.Log($"[MenuButtonBase] OnMeshHoverEnter id='{NodeId}'", this);
		IsHovered = true;
		OnHoverEnterLocal();
	}

	// Called by RadialMenuMeshEvents
	public void OnMeshHoverExit(RadialMenuMeshEvents source)
	{
		if (_retired || !isActiveAndEnabled) return;
		if (debugLog) Debug.Log($"[MenuButtonBase] OnMeshHoverExit id='{NodeId}'", this);
		IsHovered = false;
		OnHoverExitLocal();
	}

	// State (set by RadialMenuFromYaml)
	public void SetSelected(bool selected)
	{
		IsSelected = selected;
		OnSelectedChanged(selected);
	}

	public void SetActive(bool active)
	{
		IsActive = active;
		OnActiveChanged(active);
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
