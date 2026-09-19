using UnityEngine;

public sealed class RadialRingRotation : MonoBehaviour
{
    Quaternion _target;
    RadialMenuFromYaml _owner;

    internal void Initialize(RadialMenuFromYaml owner) => _owner = owner;

    void Awake() => _target = transform.localRotation;

    internal void Scroll(float steps)
    {
        if (_owner == null) return;
        _target *= Quaternion.AngleAxis(steps * _owner.wheelRotationDegrees, Vector3.forward);
    }

    void LateUpdate()
    {
        if (_owner == null) return;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, _target,
            1f - Mathf.Exp(-Mathf.Max(0.01f, _owner.wheelRotationResponse) * Time.deltaTime));
    }
}
