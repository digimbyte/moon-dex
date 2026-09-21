using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to the procedural mesh child (e.g. "WedgeMesh").
/// Ensures there's a MeshCollider and forwards simple mouse / pointer events to MenuButtonBase.
/// Implements both legacy `OnMouse*` and `IPointer*` handlers so scenes using
/// the EventSystem + PhysicsRaycaster or the legacy input both work.
/// </summary>
[DisallowMultipleComponent]
public sealed class RadialMenuMeshEvents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IScrollHandler
{
	[SerializeField] private MenuButtonBase menu;
	internal MenuButtonBase Button => menu != null && menu.isActiveAndEnabled ? menu : null;

	bool UsesFallbackInput
	{
		get
		{
			if (menu == null || menu.Owner == null) return false;
			var forwarder = menu.Owner.GetComponent<RadialMenuInputForwarder>();
			return forwarder != null && forwarder.isActiveAndEnabled && forwarder.enableFallback;
		}
	}

	public void Initialize(MenuButtonBase target)
	{
		menu = target;
		EnsureMeshCollider();
	}

	void OnEnable()
	{
		// In case the mesh was rebuilt after Initialize()
		EnsureMeshCollider();
	}

	void EnsureMeshCollider()
	{
		var mf = GetComponent<MeshFilter>();
		if (mf == null || mf.sharedMesh == null) return;

		var mc = GetComponent<MeshCollider>();
		if (mc != null && mc.sharedMesh != null) return;
		if (mc == null) mc = gameObject.AddComponent<MeshCollider>();

		// Force refresh. MeshCollider can cache the mesh.
		mc.sharedMesh = null;
		mc.sharedMesh = mf.sharedMesh;

		Debug.Log($"[RadialMenuMeshEvents] MeshCollider set on '{gameObject.name}'", menu);
	}

	// Legacy input events (keeps current behavior)
	void OnMouseEnter()
	{
		if (UsesFallbackInput) return;
		Debug.Log($"[RadialMenuMeshEvents] OnMouseEnter on '{gameObject.name}' -> menu='{menu?.NodeId}'", menu);
		menu?.OnMeshHoverEnter(this);
	}

	void OnMouseExit()
	{
		if (UsesFallbackInput) return;
		Debug.Log($"[RadialMenuMeshEvents] OnMouseExit on '{gameObject.name}' -> menu='{menu?.NodeId}'", menu);
		menu?.OnMeshHoverExit(this);
	}

	void OnMouseDown()
	{
		if (UsesFallbackInput) return;
#if ENABLE_LEGACY_INPUT_MANAGER
		if (BlocksPointer(Input.mousePosition)) return;
#elif ENABLE_INPUT_SYSTEM
		if (UnityEngine.InputSystem.Mouse.current != null && BlocksPointer(UnityEngine.InputSystem.Mouse.current.position.ReadValue())) return;
#endif
		Debug.Log($"[RadialMenuMeshEvents] OnMouseDown on '{gameObject.name}' -> menu='{menu?.NodeId}'", menu);
		menu?.OnMeshClicked(this);
	}

	// EventSystem pointer handlers (for projects using Graphic/Physics raycasters or the new Input System)
	public void OnPointerEnter(PointerEventData eventData)
	{
		if (UsesFallbackInput) return;
		Debug.Log($"[RadialMenuMeshEvents] OnPointerEnter on '{gameObject.name}' -> menu='{menu?.NodeId}'", menu);
		menu?.OnMeshHoverEnter(this);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (UsesFallbackInput) return;
		Debug.Log($"[RadialMenuMeshEvents] OnPointerExit on '{gameObject.name}' -> menu='{menu?.NodeId}'", menu);
		menu?.OnMeshHoverExit(this);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (UsesFallbackInput) return;
		if (BlocksPointer(eventData.position)) return;
		Debug.Log($"[RadialMenuMeshEvents] OnPointerClick on '{gameObject.name}' -> menu='{menu?.NodeId}'", menu);
		menu?.OnMeshClicked(this);
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (UsesFallbackInput || Button == null) return;
		menu.GetComponentInParent<RadialRingRotation>()?.Scroll(eventData.scrollDelta.y);
	}

	bool BlocksPointer(Vector2 position)
	{
		var camera = Camera.main;
		return menu != null && menu.Owner != null && camera != null
			&& menu.Owner.BlocksRingInput(camera.ScreenPointToRay(position));
	}
}
