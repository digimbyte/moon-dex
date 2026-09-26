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
    internal Aura.TextBlock LeafText { get; set; }
    internal Vector3 LeafLabelBounds { get; set; }

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
                if (LeafText != null)
                {
                    // Fit the full wedge bounds, independent of missing shell faces.
                    Vector3 tangent = transform.parent.TransformVector(Vector3.right * LeafLabelBounds.x);
                    Vector3 depth = transform.parent.TransformVector(Vector3.up * LeafLabelBounds.y);
                    Vector3 radial = transform.parent.TransformVector(Vector3.forward * LeafLabelBounds.z);
                    float width = Mathf.Abs(Vector3.Dot(tangent, viewer.right)) + Mathf.Abs(Vector3.Dot(depth, viewer.right)) + Mathf.Abs(Vector3.Dot(radial, viewer.right));
                    float height = Mathf.Abs(Vector3.Dot(tangent, viewer.up)) + Mathf.Abs(Vector3.Dot(depth, viewer.up)) + Mathf.Abs(Vector3.Dot(radial, viewer.up));
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

}
