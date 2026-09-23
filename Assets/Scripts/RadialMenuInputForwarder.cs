using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Fallback input forwarder: performs a Physics.Raycast from the main camera
/// using the current pointer/touch position and forwards hover/click to `MenuButtonBase`.
/// Supports both the new Input System and the legacy Input Manager via defines.
/// </summary>
[DisallowMultipleComponent]
public class RadialMenuInputForwarder : MonoBehaviour
{
    [Tooltip("Enable the fallback physics raycast input (mouse/touch).")]
    public bool enableFallback = true;

    [Tooltip("Maximum distance for physics raycasts.")]
    public float maxDistance = 100f;

    Camera _cam;
    MenuButtonBase _hovered;

    void Awake()
    {
        _cam = Camera.main;
    }

    void Update()
    {
        if (!enableFallback) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector2 pointerPos;
        bool clicked = false;
        float scroll = 0f;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // New Input System
        if (Mouse.current != null)
        {
            pointerPos = Mouse.current.position.ReadValue();
            clicked = Mouse.current.leftButton.wasPressedThisFrame;
            scroll = Mouse.current.scroll.ReadValue().y;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)
                scroll /= 120f;
#endif
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pointerPos = Touchscreen.current.primaryTouch.position.ReadValue();
            clicked = true;
        }
        else if (Touchscreen.current != null)
        {
            pointerPos = Touchscreen.current.primaryTouch.position.ReadValue();
            clicked = false;
        }
        else
        {
            return;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        // Legacy Input
        pointerPos = Input.mousePosition;
        clicked = Input.GetMouseButtonDown(0);
        scroll = Input.mouseScrollDelta.y;
#else
        // No supported input system available in this build configuration.
        return;
#endif

        var ray = _cam.ScreenPointToRay(pointerPos);

        // Nova controls and the information panel own their pointer hit. Do not
        // also select a physics wedge behind them on the same mouse press.
        var owner = GetComponent<RadialMenuFromYaml>();
        if (owner != null && owner.BlocksRingInput(ray))
        {
            if (_hovered != null) _hovered.OnMeshHoverExit(null);
            _hovered = null;
            return;
        }

        if (Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            var events = hit.collider == null ? null : hit.collider.GetComponent<RadialMenuMeshEvents>();
            var mb = events == null ? null : events.Button;
            if (mb != null && (mb.Owner == null || mb.Owner.gameObject != gameObject)) mb = null;
            if (mb != _hovered)
            {
                if (_hovered != null) _hovered.OnMeshHoverExit(null);
                _hovered = mb;
                if (_hovered != null) _hovered.OnMeshHoverEnter(null);
            }

            if (mb != null && scroll != 0f)
                mb.GetComponentInParent<RadialRingRotation>()?.Scroll(scroll);

            if (clicked)
            {
                mb?.OnMeshClicked(null);
            }
        }
        else
        {
            if (_hovered != null)
            {
                _hovered.OnMeshHoverExit(null);
                _hovered = null;
            }
        }
    }

    void OnDisable()
    {
        if (_hovered != null)
        {
            _hovered.OnMeshHoverExit(null);
            _hovered = null;
        }
    }
}
