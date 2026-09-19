using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class ScreenPositionFollower : MonoBehaviour
{
    public enum PlacementMode { OriginDepthPlane, SurfaceIntersection, RayEndpoint }
    [Header("References")]
    public Camera sourceCamera;
    public Transform alignmentAnchor;
    [Tooltip("World-space object defining the camera ray and projected bounds offsets.")]
    public Transform pivotReference;
    [Header("Projected target offsets")]
    public Vector2 pixelOffset;
    [Tooltip("Percentage of the reference's projected renderer bounds. 100 means one full width/height.")]
    public Vector2 boundsPercentOffset;
    [Header("Placement")]
    public PlacementMode placement;
    [Min(0f)] public float rayLength = 100f;
    public LayerMask surfaceLayers = ~0;
    [Header("Camera-space movement axes (X right, Y up, Z forward)")]
    public bool moveX = true;
    public bool moveY = true;
    public bool moveZ = true;
    [Header("Origin limits")]
    public bool limitBox;
    public Vector3 boxSize = Vector3.one * 10f;
    public bool limitDistance;
    [Min(0f)] public float maximumDisplacement = 10f;
    [SerializeField, HideInInspector] Vector3 origin;
    [SerializeField, HideInInspector] Transform originObject;
    [SerializeField, HideInInspector] bool originCaptured;
    string lastProblem;
    Ray lastRay;
    Vector3 lastIntersection;
    bool hasRay, hasIntersection;
    Transform Root => transform;
    public Vector3 Origin => origin;

    void Awake() { if (!originCaptured || originObject != Root) CaptureOrigin(); }
    void LateUpdate() => Evaluate();

    [ContextMenu("Capture Origin")]
    public void CaptureOrigin()
    {
        originObject = Root;
        origin = Root.localPosition;
        originCaptured = true;
    }
    public void SetPivot(Transform value)
    {
        if (value != null && (value == transform || value.IsChildOf(transform)))
        {
            Fail("Pivot Reference must be outside this object's hierarchy.");
            return;
        }
        pivotReference = value;
    }

    void OnValidate()
    {
        if (pivotReference != null && (pivotReference == transform || pivotReference.IsChildOf(transform)))
        {
            pivotReference = null;
            Fail("Pivot Reference must be outside this object's hierarchy. Invalid reference cleared.");
        }
    }
    public bool SetAlignmentAnchor(Transform value)
    {
        if (value != null && value != Root && !value.IsChildOf(Root))
            return Fail("Alignment anchor must be the moved object or one of its descendants.");
        alignmentAnchor = value;
        return true;
    }

    public bool Evaluate()
    {
        hasRay = hasIntersection = false;
        var root = Root;
        var anchor = alignmentAnchor != null ? alignmentAnchor : root;
        if (anchor != root && !anchor.IsChildOf(root)) return Fail("Alignment anchor must belong to the moved hierarchy.");
        if (!originCaptured || originObject != root) CaptureOrigin();
        var cam = sourceCamera != null ? sourceCamera : Camera.main;
        if (cam == null || cam.pixelWidth <= 0 || cam.pixelHeight <= 0) return Fail("A valid camera is required.");
        Transform rayTarget = pivotReference;
        if (rayTarget == null) return Fail("Assign a world-space Pivot Reference to define the camera ray.");
        if (rayTarget == transform || rayTarget.IsChildOf(transform))
            return Fail("Pivot Reference must be outside this object's hierarchy.");
        Vector3 pixel = cam.WorldToScreenPoint(rayTarget.position);
        if (pixel.z <= 0f) return Fail("World-space ray target is behind the camera.");
        if (boundsPercentOffset != Vector2.zero && TryProjectedSize(cam, rayTarget, out var size))
        {
            pixel.x += size.x * boundsPercentOffset.x * 0.01f;
            pixel.y += size.y * boundsPercentOffset.y * 0.01f;
        }
        pixel.x += pixelOffset.x;
        pixel.y += pixelOffset.y;
        if (!Finite(pixel)) return Fail("Screen target is invalid.");
        lastRay = cam.ScreenPointToRay(pixel);
        hasRay = true;
        Vector3 target;
        if (placement == PlacementMode.OriginDepthPlane)
        {
            // Follow the pivot's screen position at the follower's own depth.
            Vector3 originWorld = root.parent != null ? root.parent.TransformPoint(origin) : origin;
            Vector3 pivot = originWorld + (anchor.position - root.position);
            var plane = new Plane(cam.transform.forward, pivot);
            if (!plane.Raycast(lastRay, out float distance) || distance < 0f) return Fail("Depth plane does not intersect the forward screen ray.");
            target = lastRay.GetPoint(distance);
        }
        else
        {
            float distance = Mathf.Max(0f, rayLength);
            if (placement == PlacementMode.SurfaceIntersection)
            {
                var hits = Physics.RaycastAll(lastRay, distance, surfaceLayers, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits)
                {
                    if (hit.transform == root || hit.transform.IsChildOf(root)) continue;
                    if (hit.transform == rayTarget || hit.transform.IsChildOf(rayTarget)) continue;
                    if (hit.distance < Vector3.Dot(rayTarget.position - lastRay.origin, lastRay.direction)) continue;
                    distance = Mathf.Min(distance, hit.distance);
                }
            }
            target = lastRay.GetPoint(distance);
        }
        if (!Finite(target)) return Fail("Ray intersection is invalid.");
        lastIntersection = target;
        hasIntersection = true;
        Vector3 worldDelta = target - anchor.position;
        Vector3 cameraDelta = Quaternion.Inverse(cam.transform.rotation) * worldDelta;
        if (!moveX) cameraDelta.x = 0f;
        if (!moveY) cameraDelta.y = 0f;
        if (!moveZ) cameraDelta.z = 0f;
        worldDelta = cam.transform.rotation * cameraDelta;
        Vector3 desired = root.localPosition + (root.parent != null ? root.parent.InverseTransformVector(worldDelta) : worldDelta);
        if (!TryConstrain(root.localPosition, desired, out var result)) return Fail("Current position is outside the origin limits. Position held.");
        if (!Finite(result)) return Fail("Constrained position is invalid.");
        root.localPosition = result;
        lastProblem = null;
        return true;
    }

    internal bool TryConstrain(Vector3 current, Vector3 desired, out Vector3 result)
    {
        // Limit the movement along its camera-constrained direction. Clamping
        // individual parent-local axes would introduce motion on locked screen axes.
        result = current;
        Vector3 start = current - origin;
        Vector3 delta = desired - current;
        float fraction = 1f;
        if (limitBox)
        {
            Vector3 half = new Vector3(Mathf.Abs(boxSize.x), Mathf.Abs(boxSize.y), Mathf.Abs(boxSize.z)) * 0.5f;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(start[axis]) > half[axis] + 0.00001f) return false;
                if (delta[axis] > 0f) fraction = Mathf.Min(fraction, (half[axis] - start[axis]) / delta[axis]);
                else if (delta[axis] < 0f) fraction = Mathf.Min(fraction, (-half[axis] - start[axis]) / delta[axis]);
            }
        }
        if (limitDistance)
        {
            float radius = Mathf.Max(0f, maximumDisplacement);
            if (start.sqrMagnitude > radius * radius + 0.00001f) return false;
            float a = delta.sqrMagnitude;
            if (a > 0f)
            {
                float b = Vector3.Dot(start, delta);
                float c = start.sqrMagnitude - radius * radius;
                float exit = (-b + Mathf.Sqrt(Mathf.Max(0f, b * b - a * c))) / a;
                fraction = Mathf.Min(fraction, exit);
            }
        }
        result = current + delta * Mathf.Clamp01(fraction);
        return true;
    }

    bool TryProjectedSize(Camera cam, Transform reference, out Vector2 size)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool found = false;
        foreach (var renderer in reference.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            Bounds bounds = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var projected = cam.WorldToScreenPoint(point);
                if (projected.z <= 0f || !Finite(projected)) { size = Vector2.zero; return false; }
                min = Vector2.Min(min, projected);
                max = Vector2.Max(max, projected);
                found = true;
            }
        }
        size = found ? max - min : Vector2.zero;
        return found;
    }
    bool Fail(string message)
    {
        if (lastProblem != message) Debug.LogWarning("[ScreenPositionFollower] " + message, this);
        lastProblem = message;
        return false;
    }
    static bool Finite(Vector3 value) => !(float.IsNaN(value.x) || float.IsInfinity(value.x)
        || float.IsNaN(value.y) || float.IsInfinity(value.y) || float.IsNaN(value.z) || float.IsInfinity(value.z));

    void OnDrawGizmosSelected()
    {
        var root = Root;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(alignmentAnchor != null ? alignmentAnchor.position : root.position, 0.05f);
        if (hasRay)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(lastRay.origin, hasIntersection ? lastIntersection : lastRay.GetPoint(rayLength));
        }
        if (hasIntersection) Gizmos.DrawWireSphere(lastIntersection, 0.05f);
        var previous = Gizmos.matrix;
        Gizmos.matrix = root.parent != null ? root.parent.localToWorldMatrix : Matrix4x4.identity;
        Vector3 center = originCaptured ? origin : root.localPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, 0.05f);
        if (limitBox) Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(boxSize.x), Mathf.Abs(boxSize.y), Mathf.Abs(boxSize.z)));
        if (limitDistance) Gizmos.DrawWireSphere(center, Mathf.Max(0f, maximumDisplacement));
        Gizmos.matrix = previous;
    }
}
