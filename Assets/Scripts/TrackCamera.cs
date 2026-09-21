using UnityEngine;

/// <summary>
/// Faces the camera while preserving the position supplied by the menu anchor.
/// </summary>
[DefaultExecutionOrder(10000)]
public class TrackCamera : MonoBehaviour
{
    [Tooltip("Transform to face. If null, attempts to resolve Camera.main.")]
    public Transform Target;

    internal bool LockScreenAxes { get; set; }
    internal Nova.TextBlock LeafText { get; set; }
    internal Vector3 LeafLabelBounds { get; set; }
    internal MeshFilter LeafHitMesh { get; set; }
    Vector3[] _leafVertices;
    Vector3[] _screenVertices;
    int[] _leafTriangles;

    [Tooltip("Also match rotation of target.")]
    public bool FollowRotation = false;

    [Tooltip("How quickly this transform interpolates to the target (higher = snappier).")]
    public float LerpSpeed = 20f;

    [Tooltip("World up direction used when orienting to face the camera.")]
    public Vector3 UpDirection = Vector3.up;

    void Start()
    {
        if (Target == null)
        {
            // Prefer Camera.main; fall back to a tagged main camera or any Camera in the scene.
            if (Camera.main != null)
            {
                Target = Camera.main.transform;
            }
            else
            {
                var tagged = GameObject.FindWithTag("MainCamera");
                if (tagged != null)
                    Target = tagged.GetComponent<Camera>()?.transform;

                if (Target == null)
                {
                    var anyCam = FindAnyObjectByType<Camera>();
                    if (anyCam != null)
                        Target = anyCam.transform;
                }

                if (Target == null)
                    Debug.LogWarning("TrackCamera: no Camera found in scene; disabling behaviour.", this);
            }
        }
    }

    void LateUpdate()
    {
        if (LockScreenAxes)
        {
            // Match the rendered camera exactly: no arc tangent, roll correction, or lag.
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
            else if (Target != null) transform.rotation = Target.rotation;
            var viewer = camera != null ? camera.transform : Target;
            if (viewer != null && transform.parent != null)
            {
                // Keep the authored anchor position; label visibility is handled by render order.
                Vector3 position = transform.position;
                if (LeafText != null)
                {
                    Vector3 tangent = transform.parent.TransformVector(Vector3.right * LeafLabelBounds.x);
                    Vector3 depth = transform.parent.TransformVector(Vector3.up * LeafLabelBounds.y);
                    Vector3 radial = transform.parent.TransformVector(Vector3.forward * LeafLabelBounds.z);
                    float width = Mathf.Abs(Vector3.Dot(tangent, viewer.right)) + Mathf.Abs(Vector3.Dot(depth, viewer.right)) + Mathf.Abs(Vector3.Dot(radial, viewer.right));
                    float height = Mathf.Abs(Vector3.Dot(tangent, viewer.up)) + Mathf.Abs(Vector3.Dot(depth, viewer.up)) + Mathf.Abs(Vector3.Dot(radial, viewer.up));
                    if (camera != null && LeafHitMesh != null)
                        FitLeafOutline(camera, position, ref width, ref height);
                    width = Mathf.Max(0.001f, width / Mathf.Max(0.0001f, Mathf.Abs(LeafText.transform.lossyScale.x)));
                    height = Mathf.Max(0.001f, height / Mathf.Max(0.0001f, Mathf.Abs(LeafText.transform.lossyScale.y)));
                    LeafText.SizeMinMax.X.Max = width;
                    LeafText.SizeMinMax.Y.Max = height;
                    LeafText.Size.X = width;
                    LeafText.Size.Y = height;
                }
            }
            return;
        }
        if (Target == null)
            return;

        float t = Mathf.Clamp01(LerpSpeed * Time.deltaTime);
        // Always orient to face the camera unless the user explicitly requests matching the camera rotation.
        if (FollowRotation)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Target.rotation, t);
        }
        else
        {
            Vector3 toCamera = Target.position - transform.position;
            if (toCamera.sqrMagnitude > Mathf.Epsilon)
            {
                // Nova/TMP text is viewed from its local -Z side.
                Quaternion look = Quaternion.LookRotation(-toCamera, UpDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, t);
            }
        }
    }

    void FitLeafOutline(Camera camera, Vector3 center, ref float width, ref float height)
    {
        if (_leafVertices == null)
        {
            _leafVertices = LeafHitMesh.sharedMesh.vertices;
            _leafTriangles = LeafHitMesh.sharedMesh.triangles;
            _screenVertices = new Vector3[_leafVertices.Length];
        }
        for (int i = 0; i < _leafVertices.Length; i++)
            _screenVertices[i] = camera.WorldToScreenPoint(LeafHitMesh.transform.TransformPoint(_leafVertices[i]));

        // Fit the single-line text's shape, rather than shrinking an unnecessarily tall square.
        Vector2 preferred = LeafText.TMP.GetPreferredValues(LeafText.Text);
        float aspect = Mathf.Max(1f, preferred.x / Mathf.Max(0.001f, preferred.y));
        height = Mathf.Min(height, width / aspect);
        float low = 0f, high = 1f;
        for (int step = 0; step < 8; step++)
        {
            float scale = (low + high) * 0.5f;
            bool fits = true;
            for (int y = -1; y <= 1 && fits; y++)
            for (int x = -1; x <= 1 && fits; x++)
            {
                Vector3 point = center + camera.transform.right * (x * width * scale * 0.5f)
                    + camera.transform.up * (y * height * scale * 0.5f);
                fits = InsideLeaf(camera.WorldToScreenPoint(point), camera.nearClipPlane);
            }
            if (fits) low = scale;
            else high = scale;
        }
        width *= low * 0.9f;
        height *= low * 0.9f;
    }

    bool InsideLeaf(Vector3 point, float nearPlane)
    {
        if (point.z <= nearPlane) return false;
        for (int i = 0; i < _leafTriangles.Length; i += 3)
        {
            Vector3 a = _screenVertices[_leafTriangles[i]];
            Vector3 b = _screenVertices[_leafTriangles[i + 1]];
            Vector3 c = _screenVertices[_leafTriangles[i + 2]];
            if (a.z <= nearPlane || b.z <= nearPlane || c.z <= nearPlane) continue;
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < 0.0001f) continue;
            float u = Cross(b - point, c - point) / area;
            float v = Cross(c - point, a - point) / area;
            if (u >= 0f && v >= 0f && u + v <= 1f) return true;
        }
        return false;
    }

    static float Cross(Vector3 a, Vector3 b) => a.x * b.y - a.y * b.x;
}
