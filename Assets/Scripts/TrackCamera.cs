using UnityEngine;

/// <summary>
/// Faces the camera while preserving the position supplied by the menu anchor.
/// </summary>
public class TrackCamera : MonoBehaviour
{
    [Tooltip("Transform to face. If null, attempts to resolve Camera.main.")]
    public Transform Target;

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
