using UnityEngine;

public sealed class RadialRingRotation : MonoBehaviour
{
    Quaternion _target;
    RadialMenuFromYaml _owner;
    SfxRatchet _ratchet;
    float _velocity;
    bool _scrollDriven;
    Quaternion _selectionStart;
    float _selectionElapsed;
    bool _rotatingToSelection;
    Vector3 _scaleStart;
    Vector3 _scaleTarget = Vector3.one;
    float _scaleElapsed = 0.2f;
    Transform[] _enteringWedges;
    Vector3[] _wedgePositions;
    Vector3[] _wedgeCenters;
    Vector3[] _wedgeStartScales;
    bool _animatingWedges;
    bool _collapsing;
    float _entranceElapsed;
    System.Action _onCollapsed;

    internal void Collapse(System.Action onComplete)
    {
        StopMomentum();
        _collapsing = true;
        _animatingWedges = true;
        _entranceElapsed = 0f;
        for (int i = 0; i < _enteringWedges.Length; i++)
            _wedgeStartScales[i] = _enteringWedges[i].localScale;
        _rotatingToSelection = false;
        _target = transform.localRotation;
        _onCollapsed = onComplete;
        // Freeze the depth scale; only individual wedges animate out.
        _scaleElapsed = 0.2f;
    }

    internal void SetScale(Vector3 target)
    {
        if (!Application.isPlaying)
        {
            transform.localScale = _scaleTarget = target;
            return;
        }
        if (_scaleTarget == target) return;
        _scaleStart = transform.localScale;
        _scaleTarget = target;
        _scaleElapsed = 0f;
    }

    internal void GrowWedges()
    {
        if (!Application.isPlaying) return;
        _entranceElapsed = 0f;
        _animatingWedges = true;
        _collapsing = false;
        _enteringWedges = new Transform[transform.childCount];
        _wedgePositions = new Vector3[_enteringWedges.Length];
        _wedgeCenters = new Vector3[_enteringWedges.Length];
        _wedgeStartScales = new Vector3[_enteringWedges.Length];
        for (int i = 0; i < _enteringWedges.Length; i++)
        {
            var wedge = _enteringWedges[i] = transform.GetChild(i);
            _wedgePositions[i] = wedge.localPosition;
            var center = wedge.GetComponent<MenuButtonBase>()?.CenterAnchor;
            _wedgeCenters[i] = center != null
                ? transform.InverseTransformPoint(center.position) - wedge.localPosition
                : Vector3.zero;
            wedge.localScale = Vector3.zero;
            wedge.localPosition = _wedgePositions[i] + _wedgeCenters[i];
        }
    }

    internal void Initialize(RadialMenuFromYaml owner)
    {
        _owner = owner;
        _ratchet = GetComponentInParent<SfxRatchet>(true);
        StopMomentum();
    }

    internal void StopMomentum()
    {
        _velocity = 0f;
        _scrollDriven = false;
        if (_ratchet != null) _ratchet.ResetPlayback();
    }

    void OnDisable() => StopMomentum();

    void Awake() => _target = transform.localRotation;

    internal void Scroll(float steps)
    {
        if (_owner == null || !_owner.isActiveAndEnabled || _collapsing || !float.IsFinite(steps) || steps == 0f) return;
        _rotatingToSelection = false;
        float limit = Mathf.Max(1f, _owner.wheelMaximumSpeed);
        if (!float.IsFinite(limit) || !float.IsFinite(_owner.wheelImpulse)) return;
        double requested = _velocity + (double)steps * _owner.wheelImpulse;
        _velocity = (float)System.Math.Clamp(requested, -limit, limit);
        _scrollDriven = true;
    }

    internal void FaceCamera(Transform leafCenter, Camera camera)
    {
        if (leafCenter == null || camera == null) return;
        StopMomentum();
        Vector3 leafDirection = transform.InverseTransformPoint(leafCenter.position);
        Vector3 cameraDirection = transform.InverseTransformPoint(camera.transform.position);
        leafDirection.z = cameraDirection.z = 0f;
        // A camera directly on the ring axis has no nearer edge; use its screen-bottom side.
        if (cameraDirection.sqrMagnitude < 0.000001f)
        {
            cameraDirection = transform.InverseTransformDirection(-camera.transform.up);
            cameraDirection.z = 0f;
        }
        if (leafDirection.sqrMagnitude < 0.000001f || cameraDirection.sqrMagnitude < 0.000001f) return;
        float angle = Vector3.SignedAngle(leafDirection, cameraDirection, Vector3.forward);
        _selectionStart = transform.localRotation;
        _target = _selectionStart * Quaternion.AngleAxis(angle, Vector3.forward);
        _selectionElapsed = 0f;
        _rotatingToSelection = true;
    }

    void LateUpdate()
    {
        if (_scaleElapsed < 0.2f)
        {
            _scaleElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_scaleElapsed / 0.2f));
            transform.localScale = Vector3.Slerp(_scaleStart, _scaleTarget, progress);
            if (_scaleElapsed >= 0.2f) transform.localScale = _scaleTarget;
        }
        if (_animatingWedges)
        {
            _entranceElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_entranceElapsed / 0.2f));
            // Scale has no direction at zero, so interpolate its magnitude directly.
            for (int i = 0; i < _enteringWedges.Length; i++)
            {
                var wedge = _enteringWedges[i];
                if (wedge == null) continue;
                Vector3 scale = _collapsing
                    ? Vector3.Lerp(_wedgeStartScales[i], Vector3.zero, progress)
                    : Vector3.one * progress;
                wedge.localScale = scale;
                wedge.localPosition = _wedgePositions[i] + Vector3.Scale(_wedgeCenters[i], Vector3.one - scale);
            }
            if (_entranceElapsed >= 0.2f)
            {
                _animatingWedges = false;
                if (_onCollapsed != null)
                {
                    var complete = _onCollapsed;
                    _onCollapsed = null;
                    complete();
                    return;
                }
            }
        }
        if (_owner == null || !_owner.isActiveAndEnabled || _collapsing)
        {
            StopMomentum();
            return;
        }
        if (_rotatingToSelection)
        {
            _selectionElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_selectionElapsed / 0.2f);
            transform.localRotation = Quaternion.Slerp(_selectionStart, _target, progress);
            if (progress >= 1f) _rotatingToSelection = false;
            return;
        }
        float friction = float.IsFinite(_owner.wheelFriction) ? Mathf.Max(0.01f, _owner.wheelFriction) : 4f;
        float limit = float.IsFinite(_owner.wheelMaximumSpeed) ? Mathf.Max(1f, _owner.wheelMaximumSpeed) : 720f;
        _velocity = Mathf.Clamp(_velocity, -limit, limit);
        float dt = Time.unscaledDeltaTime;
        float decay = Mathf.Exp(-friction * dt);
        float deltaDegrees = _velocity * (1f - decay) / friction;
        transform.localRotation *= Quaternion.AngleAxis(deltaDegrees, Vector3.forward);
        _velocity *= decay;
        if (Mathf.Abs(_velocity) < 1f) _velocity = 0f;
        if (_ratchet != null) _ratchet.AdvanceRotation(_scrollDriven ? deltaDegrees : 0f, _velocity, limit);
    }
}
