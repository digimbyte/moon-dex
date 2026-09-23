using TMPro;
using UnityEngine;

// Subscribe before Nova copies TMP vertices into its renderer.
[DefaultExecutionOrder(-10000)]
public sealed class CurvedMenuText : MonoBehaviour
{
    TMP_Text _text;
    Transform _ring;
    float _radius, _angle, _faceZ;
    bool _subscribed;
    Camera _camera;
    Matrix4x4 _lastRingMatrix, _lastTextMatrix;
    Vector3 _lastCameraPosition;
    Quaternion _lastCameraRotation;
    float _direction = -1f;
    float _opacity = 1f;

    public void Configure(TMP_Text text, Transform ring, float radius, float angle, float faceZ)
    {
        _text = text;
        _ring = ring;
        _radius = radius;
        _angle = angle * Mathf.Deg2Rad;
        _faceZ = faceZ;
        Subscribe();
    }

    void OnEnable() => Subscribe();
    void Subscribe()
    {
        if (_text == null || _subscribed) return;
        _text.OnPreRenderText += Curve;
        _subscribed = true;
    }
    void OnDisable()
    {
        if (_text != null && _subscribed) _text.OnPreRenderText -= Curve;
        _subscribed = false;
    }

    void LateUpdate()
    {
        if (_text == null || _ring == null) return;
        if (_camera == null) _camera = Camera.main;
        var cameraPosition = _camera != null ? _camera.transform.position : Vector3.zero;
        var cameraRotation = _camera != null ? _camera.transform.rotation : Quaternion.identity;
        float desiredDirection = _direction;
        if (_camera != null)
        {
            // Compare the arc tangent with camera-right, including the ring's rotation.
            Vector3 tangent = _ring.TransformVector(new Vector3(-Mathf.Sin(_angle), Mathf.Cos(_angle), 0f)).normalized;
            float alignment = Vector3.Dot(tangent, _camera.transform.right);
            // A small dead band keeps the swap stable when the tangent is vertical.
            if (alignment > 0.1f) desiredDirection = 1f;
            else if (alignment < -0.1f) desiredDirection = -1f;
        }
        float previousOpacity = _opacity;
        float previousDirection = _direction;
        _opacity = Mathf.MoveTowards(_opacity, desiredDirection == _direction ? 1f : 0f, Time.deltaTime / 0.1f);
        if (_opacity == 0f) _direction = desiredDirection;
        var ringMatrix = _ring.localToWorldMatrix;
        var textMatrix = transform.localToWorldMatrix;
        if (ringMatrix == _lastRingMatrix && textMatrix == _lastTextMatrix && cameraPosition == _lastCameraPosition
            && cameraRotation == _lastCameraRotation && previousOpacity == _opacity && previousDirection == _direction) return;
        _lastRingMatrix = ringMatrix;
        _lastTextMatrix = textMatrix;
        _lastCameraPosition = cameraPosition;
        _lastCameraRotation = cameraRotation;
        // Regenerate straight source vertices before bending; never bend yesterday's mesh.
        _text.ForceMeshUpdate();
    }

    void Curve(TMP_TextInfo info)
    {
        if (_ring == null || info.characterCount == 0) return;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            minX = Mathf.Min(minX, c.bottomLeft.x);
            maxX = Mathf.Max(maxX, c.topRight.x);
            // Center the visible glyphs, not the font's ascender/descender padding.
            minY = Mathf.Min(minY, c.bottomLeft.y);
            maxY = Mathf.Max(maxY, c.topRight.y);
        }
        if (float.IsInfinity(minX)) return;
        if (_camera == null) _camera = Camera.main;
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        Vector3 arcOrigin = _ring.TransformPoint(new Vector3(
            Mathf.Cos(_angle) * _radius, Mathf.Sin(_angle) * _radius, _faceZ));
			// Nova can reposition this root-level text block during layout. Use its
			// actual world anchor; the text no longer inherits the icon block.
			Vector3 anchorOffset = transform.position - arcOrigin;
        // Swap the reading direction only while the label is fully faded.
        float direction = _direction;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            float glyphX = (c.bottomLeft.x + c.topRight.x) * 0.5f;
            float angle = _angle + direction * (glyphX - centerX) / _radius;
            var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            var tangent = new Vector3(-radial.y, radial.x, 0f) * direction;
            Vector3 center = _ring.TransformPoint(radial * _radius + Vector3.forward * _faceZ) + anchorOffset;
            Vector3 right = _ring.TransformVector(tangent);
            Vector3 up = _ring.TransformVector(-radial * direction);
            float heightScale = up.magnitude;
            if (_camera != null)
            {
                Vector3 facing = Vector3.ProjectOnPlane(_camera.transform.position - center, right.normalized);
                if (facing.sqrMagnitude > 0.000001f)
                    up = Vector3.Cross(right.normalized, facing.normalized) * heightScale;
            }
            var vertices = info.meshInfo[c.materialReferenceIndex].vertices;
            for (int corner = 0; corner < 4; corner++)
            {
                int index = c.vertexIndex + corner;
                Vector3 original = vertices[index];
                // Glyph centers remain on the ring. Only tilt around its tangent.
                Vector3 point = center + right * (original.x - glyphX) + up * (original.y - centerY);
                vertices[index] = transform.InverseTransformPoint(point);
                var colors = info.meshInfo[c.materialReferenceIndex].colors32;
                Color32 color = colors[index];
                color.a = (byte)Mathf.RoundToInt(color.a * Mathf.SmoothStep(0f, 1f, _opacity));
                colors[index] = color;
            }
        }
    }
}
