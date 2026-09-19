using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool lockYAxis = false;
    [SerializeField] private bool debugMode = false; // Show debug info

    public enum Mode
    {
        ScreenAligned,       // Plane parallel to screen; up = camera.up
        ViewpointOriented,   // Look at camera position; up = camera.up
        WorldUpLockedYaw,    // Yaw only around world Y; up = Vector3.up
        TrueScreenSpace      // Compensates for perspective distortion - always appears as 2D GUI
    }

    [SerializeField] private Mode mode = Mode.TrueScreenSpace;
    [SerializeField] private bool useLateUpdate = true;          // If false, uses Update
    [SerializeField] private bool useOnWillRenderObject = false; // For multi-camera or to run last
    [SerializeField] private bool respectCameraRoll = true;      // If false, uses world up instead of camera up
    [SerializeField] private bool invertFacingY = false;          // Rotate 180° around local Y (fix reversed textures)

    private Transform camT;
    
    void OnEnable()  { InitializeCamera(); }
    void OnValidate() { InitializeCamera(); }

    void Start()
    {
        InitializeCamera();
    }
    
    private void InitializeCamera()
    {
        // If no camera is assigned, use the main camera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (debugMode) Debug.Log($"Billboard {name}: Using Camera.main");
        }
        
        if (targetCamera != null)
        {
            camT = targetCamera.transform;
            if (debugMode) Debug.Log($"Billboard {name}: Initialized with camera {targetCamera.name}");
        }
        else
        {
            Debug.LogWarning($"Billboard {name}: No camera found!");
        }
    }
    
    void Update()
    {
        if (useOnWillRenderObject) return;
        if (targetCamera == null || camT == null) InitializeCamera();
        if (!useLateUpdate && camT != null) ApplyFor(targetCamera);
    }

    void LateUpdate()
    {
        if (useOnWillRenderObject) return;
        if (targetCamera == null || camT == null) InitializeCamera();
        if (useLateUpdate && camT != null) ApplyFor(targetCamera);
    }

    private void UpdateBillboard()
    {
        ApplyFor(targetCamera);
    }
    
    // Called for each camera that renders this object
    void OnWillRenderObject()
    {
        if (!useOnWillRenderObject) return;
        var cam = Camera.current;
        if (cam != null) ApplyFor(cam);
    }

    private void ApplyFor(Camera cam)
    {
        if (cam == null)
        {
            InitializeCamera();
            cam = targetCamera;
            if (cam == null) return;
        }
        var cT = cam.transform;

        Vector3 pos = transform.position;
        Vector3 toCam = cT.position - pos;
        Vector3 up = respectCameraRoll ? cT.up : Vector3.up;
        Vector3 forward;

        var effectiveMode = lockYAxis ? Mode.WorldUpLockedYaw : mode;

        switch (effectiveMode)
        {
            case Mode.ScreenAligned:
                forward = -cT.forward;
                break;
            case Mode.ViewpointOriented:
                forward = toCam.sqrMagnitude > 1e-8f ? toCam.normalized : -cT.forward;
                break;
            case Mode.WorldUpLockedYaw:
                forward = Vector3.ProjectOnPlane(toCam, Vector3.up);
                if (forward.sqrMagnitude < 1e-8f)
                    forward = Vector3.ProjectOnPlane(-cT.forward, Vector3.up);
                forward.Normalize();
                up = Vector3.up;
                break;
            case Mode.TrueScreenSpace:
                // Calculate perspective-corrected orientation for true 2D appearance
                CalculatePerspectiveCorrectedOrientation(cam, cT, pos, out forward, out up);
                break;
            default:
                forward = -cT.forward;
                break;
        }

        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 1e-8f)
        {
            up = Vector3.up;
            right = Vector3.Cross(up, forward);
        }
        right.Normalize();
        up = Vector3.Cross(forward, right).normalized;

        // Compute final rotation, with optional 180° local Y flip
        Quaternion finalRot = Quaternion.LookRotation(forward, up);
        if (invertFacingY)
        {
            finalRot *= Quaternion.Euler(0f, 180f, 0f);
        }
        transform.rotation = finalRot;

        if (debugMode && Time.frameCount % 120 == 0)
            Debug.Log($"{name}: mode={effectiveMode}, up={up}, forward={forward}");
    }

    private void CalculatePerspectiveCorrectedOrientation(Camera cam, Transform cT, Vector3 pos, out Vector3 forward, out Vector3 up)
    {
        // Get screen position in normalized coordinates (-1 to 1)
        Vector3 screenPos = cam.WorldToViewportPoint(pos);
        Vector2 normalizedScreen = new Vector2((screenPos.x - 0.5f) * 2.0f, (screenPos.y - 0.5f) * 2.0f);
        
        // Calculate the angle this screen position represents in the camera's FOV
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float aspect = cam.aspect;
        
        // Calculate the actual world-space angles for this screen position
        float verticalAngle = normalizedScreen.y * fovRad * 0.5f;
        float horizontalAngle = normalizedScreen.x * Mathf.Atan(Mathf.Tan(fovRad * 0.5f) * aspect);
        
        // Create rotation corrections to counteract perspective distortion
        Quaternion verticalCorrection = Quaternion.AngleAxis(-verticalAngle * Mathf.Rad2Deg, cT.right);
        Quaternion horizontalCorrection = Quaternion.AngleAxis(-horizontalAngle * Mathf.Rad2Deg, cT.up);
        
        // Base orientation facing camera
        Vector3 toCamera = (cT.position - pos).normalized;
        Vector3 baseUp = respectCameraRoll ? cT.up : Vector3.up;
        
        // Apply corrections to counteract the perspective distortion
        Quaternion correctionRotation = horizontalCorrection * verticalCorrection;
        Vector3 correctedUp = correctionRotation * baseUp;
        Vector3 correctedForward = correctionRotation * (-cT.forward);
        
        // For extreme off-center positions, blend between corrected and direct camera facing
        float distortionFactor = Mathf.Clamp01(normalizedScreen.magnitude);
        
        if (distortionFactor > 0.01f) // Only apply correction if significantly off-center
        {
            // The key insight: rotate the "up" direction to counteract screen-space distortion
            // This ensures horizontal lines in the texture appear horizontal on screen
            
            // Calculate how much the perspective projection "tilts" the object
            float perspectiveTilt = horizontalAngle;
            
            // Counter-rotate by this amount around the forward axis
            Quaternion antiDistortionRotation = Quaternion.AngleAxis(perspectiveTilt * Mathf.Rad2Deg, toCamera);
            correctedUp = antiDistortionRotation * baseUp;
            
            forward = toCamera;
            up = correctedUp;
        }
        else
        {
            // Center of screen - no correction needed
            forward = toCamera;
            up = baseUp;
        }
        
        // Ensure orthogonality
        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 1e-8f)
        {
            up = Vector3.up;
            right = Vector3.Cross(up, forward);
        }
        right.Normalize();
        up = Vector3.Cross(forward, right).normalized;
        
        if (debugMode && Time.frameCount % 120 == 0)
        {
            Debug.Log($"{name}: TrueScreenSpace - screenPos: {normalizedScreen}, angles: H={horizontalAngle * Mathf.Rad2Deg:F1}°, V={verticalAngle * Mathf.Rad2Deg:F1}°");
        }
    }

}
