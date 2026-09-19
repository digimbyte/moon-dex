using UnityEngine;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(Camera))]
public sealed class BillboardRenderController : MonoBehaviour
{
	[Header("SOURCE")]
	public Camera sourceCamera;      // A
	public Transform sourceAnchor;   // B

	[Header("TARGET (subject center)")]
	public Transform targetAnchor;   // D

	[Header("SUBJECT SIZE")]
	[Tooltip("World-space radius of the subject to fit in-frame.")]
	[Min(0.0001f)] public float subjectRadius = 1f;

	[Header("RUNTIME")]
	public bool updateEveryFrame = true;
	[Min(0.001f)] public float updateInterval = 0.02f;

	[Header("DEBUG")]
	public bool debugLog = false;

	private Camera _cam;
	private float _nextUpdateTime;

	private const float EPS = 1e-6f;

	void Awake()
	{
		_cam = GetComponent<Camera>();
	}

	void LateUpdate()
	{
		if (!Application.isPlaying) return;
		if (!IsValid()) return;

		if (updateEveryFrame || Time.time >= _nextUpdateTime)
		{
			MirrorPose_WorldOffset();
			ApplyFovFromRadiusOnly();
			_nextUpdateTime = Time.time + updateInterval;
		}
	}

	private bool IsValid()
	{
		if (_cam == null) _cam = GetComponent<Camera>();
		return _cam != null && sourceCamera != null && sourceAnchor != null && targetAnchor != null;
	}

	// World-space only: desiredPos = D + (A - B)
	private void MirrorPose_WorldOffset()
	{
		Transform srcCamT = sourceCamera.transform;

		Vector3 worldOffset = srcCamT.position - sourceAnchor.position;
		Vector3 desiredWorldPos = targetAnchor.position + worldOffset;

		SetWorldPositionRobust(transform, desiredWorldPos);

		// Always face the subject so it's +Z (forward)
		Vector3 toTarget = targetAnchor.position - transform.position;
		if (toTarget.sqrMagnitude > EPS)
			transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
	}

	// Trig: fit bounding sphere radius R at forward depth D
	private void ApplyFovFromRadiusOnly()
	{
		_cam.orthographic = false;

		float R = Mathf.Max(subjectRadius, EPS);

		Vector3 toCenter = targetAnchor.position - transform.position;

		// Depth along forward axis (projection-relevant distance)
		float D = Vector3.Dot(transform.forward, toCenter);

		// If subject is behind camera, don't touch FOV.
		if (D <= EPS)
		{
			if (debugLog)
				Debug.LogWarning($"[BillboardRenderController] FOV skipped: subject behind camera. forwardDepth={D:F4}");
			return;
		}

		float aspect = Mathf.Max(_cam.aspect, 0.0001f);

		// Need tan(v/2) >= max(R/D, R/(aspect*D)) to fit in both height and width.
		float reqTanHalfV = Mathf.Max(R / D, R / (aspect * D));

		float vFov = 2f * Mathf.Atan(reqTanHalfV) * Mathf.Rad2Deg;
		vFov = Mathf.Clamp(vFov, 1f, 179f);

		_cam.fieldOfView = vFov;

		if (debugLog)
			Debug.Log($"[BillboardRenderController] APPLY FOV R={R:F3} D={D:F3} aspect={aspect:F3} fov={vFov:F2}");
	}

	private static void SetWorldPositionRobust(Transform t, Vector3 worldPos)
	{
		Transform p = t.parent;
		if (p == null)
		{
			t.position = worldPos;
			return;
		}

		t.localPosition = p.worldToLocalMatrix.MultiplyPoint3x4(worldPos);
	}

	void OnValidate()
	{
		subjectRadius = Mathf.Max(0.0001f, subjectRadius);
		updateInterval = Mathf.Max(0.001f, updateInterval);
	}
}
