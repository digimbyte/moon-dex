using UnityEngine;
using System.Collections;
using Gaskellgames.CameraSystem;

public class OrbitCameraController : MonoBehaviour
{
	// ----------------------------------------------------
	// REFERENCES
	// ----------------------------------------------------
	[Header("Runtime Camera")]
	public Camera controlledCamera;

	[Header("Orbit Rig")]
	public Transform orbitPivot;
	public bool useParentAsPivot = true;
	public bool rotatePivotInsteadOfCamera = true;

	Transform originalParent;

	// ----------------------------------------------------
	// STORED STATE
	// ----------------------------------------------------
	Vector3 originalLocalPos;
	Quaternion originalLocalRot;
	float originalFov;
	Quaternion originalPivotLocalRot;

	bool isBound;

	CameraRig cameraRig;

	// ----------------------------------------------------
	// ORBIT LIMITS
	// ----------------------------------------------------
	[Header("Orbit Limits")]
	public float northPoleLimit = 80f;
	public float southPoleLimit = -80f;

	// ----------------------------------------------------
	// FEEL
	// ----------------------------------------------------
	[Header("Orbit Feel")]
	public float rotationSpeed = 180f;
	public float dragSpeed = 0.25f;
	public float friction = 6f;

	// ----------------------------------------------------
	// ZOOM (FOV)
	// ----------------------------------------------------
	[Header("Zoom")]
	public float minFov = 25f;
	public float maxFov = 70f;
	public float zoomSpeed = 20f;
	public float fovSmooth = 8f;

	[Tooltip("When Max Zoom Lock is enabled, force the FOV to this value (ignores minFov).")]
	public float lockedZoomFov = 5f;

	public bool maxZoomLock = false;

	// ----------------------------------------------------
	// INTERNAL
	// ----------------------------------------------------
	float yaw;
	float pitch;

	float yawVelocity;
	float pitchVelocity;

	float targetFov;
	float lastFreeFov;

	// Drag state so we can ignore the first noisy frame of every click+drag sequence
	bool ignoreCurrentMouseDragFrame;

	// When controls are locked, user input is disabled; programmatic rotation still runs.
	bool controlsLocked;
	float lockTimeoutSeconds = 5f;
	float lockTimeoutTimer;

	// ----------------------------------------------------
	// UPDATE
	// ----------------------------------------------------
	void Update()
	{
		EnsureBound();
		if (!isBound) return;
		// Lock timeout lives on the controller (not per-anchor). If it expires, unlock input.
		if (controlsLocked && lockTimeoutSeconds > 0f)
		{
			lockTimeoutTimer -= Time.deltaTime;
			if (lockTimeoutTimer <= 0f)
			{
				controlsLocked = false;
				lockTimeoutTimer = 0f;
				// zero velocities so user input resumes smoothly
				yawVelocity = 0f;
				pitchVelocity = 0f;
			}
		}

		// When controls are locked we only block user input; programmatic
		// rotations (RotateTo / RotateRoutine) still update yaw/pitch and
		// are applied every frame.
		if (!controlsLocked)
		if (!controlsLocked)
		{
			HandleInput();
		}

		ApplyRotation();
		ApplyZoom();
	}

	// ----------------------------------------------------
	// CAMERA BIND / RELEASE
	// ----------------------------------------------------

	/// <summary>
	/// Takes control of a camera and stores its initial state
	/// </summary>
	public void BindCamera(Camera cam)
	{
		if (cam == null) return;

		controlledCamera = cam;
		isBound = false;
		EnsureBound();
	}

	void EnsureBound()
	{
		if (isBound) return;

		// Cache references
		if (cameraRig == null)
			cameraRig = GetComponent<CameraRig>();

		if (controlledCamera == null)
			controlledCamera = Camera.main;

		// Store original camera state (only used when rotating camera directly)
		if (controlledCamera != null)
		{
			originalParent = controlledCamera.transform.parent;
			originalLocalPos = controlledCamera.transform.localPosition;
			originalLocalRot = controlledCamera.transform.localRotation;
		}

		// Store original pivot rotation + initialize yaw/pitch from pivot rotation
		Transform pivot = GetOrbitPivot();
		if (pivot != null)
		{
			originalPivotLocalRot = pivot.localRotation;

			Vector3 euler = pivot.localEulerAngles;
			yaw = euler.y;
			pitch = NormalizeAngle(euler.x);
		}

		// Initialize zoom target from the active rig lens if possible (CameraBrain overwrites Camera.fieldOfView)
		float currentFov = GetCurrentFov();
		float clampedFov = ClampFov(currentFov);
		targetFov = clampedFov;
		lastFreeFov = targetFov;
		originalFov = clampedFov;

		isBound = true;
	}

	Transform GetOrbitPivot()
	{
		if (orbitPivot != null) return orbitPivot;
		if (useParentAsPivot && transform.parent != null) return transform.parent;
		return transform;
	}

	float GetCurrentFov()
	{
		if (cameraRig != null)
			return cameraRig.Lens.verticalFOV;

		if (controlledCamera != null)
			return controlledCamera.fieldOfView;

		return Mathf.Clamp(60f, minFov, maxFov);
	}

	void SetCurrentFov(float fov)
	{
		if (cameraRig != null)
		{
			CameraLens lens = cameraRig.Lens;
			lens.verticalFOV = fov;
			cameraRig.Lens = lens;
			return;
		}

		if (controlledCamera != null)
			controlledCamera.fieldOfView = fov;
	}

	/// <summary>
	/// Smoothly restores camera to original state and releases it
	/// </summary>
	public void RestoreAndRelease(float duration = 0.5f)
	{
		if (!isBound || controlledCamera == null) return;

		StopAllCoroutines();
		StartCoroutine(RestoreRoutine(duration));
	}

	IEnumerator RestoreRoutine(float duration)
	{
		float t = 0f;

		Transform pivot = GetOrbitPivot();
		Quaternion startPivotRot = pivot != null ? pivot.localRotation : Quaternion.identity;

		Transform camT = controlledCamera != null ? controlledCamera.transform : null;
		Vector3 startPos = camT != null ? camT.localPosition : Vector3.zero;
		Quaternion startRot = camT != null ? camT.localRotation : Quaternion.identity;
		float startFov = GetCurrentFov();

		while (t < 1f)
		{
			t += Time.deltaTime / duration;

			if (rotatePivotInsteadOfCamera && pivot != null)
			{
				pivot.localRotation = Quaternion.Slerp(startPivotRot, originalPivotLocalRot, t);
			}
			else if (camT != null)
			{
				camT.localPosition = Vector3.Lerp(startPos, originalLocalPos, t);
				camT.localRotation = Quaternion.Slerp(startRot, originalLocalRot, t);
			}

			SetCurrentFov(Mathf.Lerp(startFov, originalFov, t));

			yield return null;
		}

		// snap to final
		if (rotatePivotInsteadOfCamera && pivot != null)
		{
			pivot.localRotation = originalPivotLocalRot;
		}
		else if (camT != null)
		{
			camT.SetParent(originalParent);
			camT.localPosition = originalLocalPos;
			camT.localRotation = originalLocalRot;
		}

		SetCurrentFov(originalFov);

		controlledCamera = null;
		isBound = false;
	}

	// ----------------------------------------------------
	// INPUT
	// ----------------------------------------------------
	void HandleInput()
	{
		if (maxZoomLock) return;

		float h = Input.GetAxisRaw("Horizontal");
		float v = Input.GetAxisRaw("Vertical");

		yawVelocity += h * rotationSpeed * Time.deltaTime;
		pitchVelocity -= v * rotationSpeed * Time.deltaTime;

		// For mouse orbit we want to treat each click+drag as a sequence.
		// The frame where the button goes down often has a huge delta when
		// the user clicks back into the Game view from elsewhere.
		// We ignore that frame's delta for *every* new drag sequence.
		if (Input.GetMouseButtonDown(0))
		{
			ignoreCurrentMouseDragFrame = true;
		}

		if (Input.GetMouseButton(0))
		{
			if (ignoreCurrentMouseDragFrame)
			{
				// Consume this frame without applying rotation.
				ignoreCurrentMouseDragFrame = false;
			}
			else
			{
				yawVelocity += Input.GetAxis("Mouse X") * rotationSpeed * dragSpeed;
				pitchVelocity -= Input.GetAxis("Mouse Y") * rotationSpeed * dragSpeed;
			}
		}

		if (Input.touchCount == 1)
		{
			Touch t = Input.GetTouch(0);
			if (t.phase == TouchPhase.Moved)
			{
				yawVelocity += t.deltaPosition.x * dragSpeed;
				pitchVelocity -= t.deltaPosition.y * dragSpeed;
			}
		}

		float scroll = Input.mouseScrollDelta.y;
		if (Mathf.Abs(scroll) > 0.01f)
		{
			targetFov -= scroll * zoomSpeed;
			targetFov = Mathf.Clamp(targetFov, minFov, maxFov);
			lastFreeFov = targetFov;
		}

		if (Input.touchCount == 2)
		{
			Touch a = Input.GetTouch(0);
			Touch b = Input.GetTouch(1);

			float prev = (a.position - a.deltaPosition - (b.position - b.deltaPosition)).magnitude;
			float curr = (a.position - b.position).magnitude;

			float delta = curr - prev;
			targetFov -= delta * zoomSpeed * 0.02f;
			targetFov = Mathf.Clamp(targetFov, minFov, maxFov);
			lastFreeFov = targetFov;
		}
	}

	// ----------------------------------------------------
	// ROTATION
	// ----------------------------------------------------
	void ApplyRotation()
	{
		yaw += yawVelocity;
		pitch += pitchVelocity;

		pitch = Mathf.Clamp(pitch, southPoleLimit, northPoleLimit);

		yawVelocity = Mathf.Lerp(yawVelocity, 0f, friction * Time.deltaTime);
		pitchVelocity = Mathf.Lerp(pitchVelocity, 0f, friction * Time.deltaTime);

		Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

		if (rotatePivotInsteadOfCamera)
		{
			Transform pivot = GetOrbitPivot();
			if (pivot != null)
				pivot.localRotation = rot;
		}
		else if (controlledCamera != null)
		{
			controlledCamera.transform.localRotation = rot;
		}
	}

	// ----------------------------------------------------
	// ZOOM
	// ----------------------------------------------------
	void ApplyZoom()
	{
		targetFov = ClampFov(targetFov);
		float desiredFov = maxZoomLock ? lockedZoomFov : targetFov;

		if (!maxZoomLock)
			lastFreeFov = targetFov;

		float current = GetCurrentFov();
		SetCurrentFov(Mathf.Lerp(current, desiredFov, fovSmooth * Time.deltaTime));
	}

	// ----------------------------------------------------
	// POLISHED API
	// ----------------------------------------------------

	/// <summary>
	/// Lock or unlock user-driven orbit controls (input). Programmatic rotation still applies.
	/// Optional timeoutOverride controls auto-unlock; pass null to use default, 0 to disable.
	/// </summary>
	public void SetControlsLocked(bool locked, float? timeoutOverride = null)
	{
		controlsLocked = locked;

		if (locked)
		{
			// Reset motion so we don't have residual velocity when unlocking
			yawVelocity = 0f;
			pitchVelocity = 0f;

			if (timeoutOverride.HasValue)
				lockTimeoutTimer = timeoutOverride.Value;
			else
				lockTimeoutTimer = lockTimeoutSeconds;
		}
		else
		{
			lockTimeoutTimer = 0f;
		}
	}

	/// <summary>
	/// Sync internal yaw/pitch state from the current orbit pivot rotation.
	/// Useful after manually rotating the pivot (e.g. via an external focus/anchor script)
	/// so that user input continues smoothly from the new orientation.
	/// </summary>
	public void SyncAnglesFromPivot()
	{
		Transform pivot = GetOrbitPivot();
		if (pivot == null) return;

		Vector3 euler = pivot.localEulerAngles;
		yaw = euler.y;
		pitch = NormalizeAngle(euler.x);

		yawVelocity = 0f;
		pitchVelocity = 0f;
	}

	/// <summary>
	/// Smoothly rotate to the given yaw/pitch at a given angular speed (degrees per second).
	/// </summary>
	public void RotateTo(float targetYaw, float targetPitch, float speedDegPerSecond = 180f)
	{
		if (!isBound) return;

		targetPitch = Mathf.Clamp(targetPitch, southPoleLimit, northPoleLimit);
		StopAllCoroutines();
		StartCoroutine(RotateRoutine(targetYaw, targetPitch, speedDegPerSecond));
	}

	IEnumerator RotateRoutine(float ty, float tp, float speedDegPerSecond)
	{
		// Rotate until we're within a very small angular threshold so "focus" moves can
		// hand back control when basically aligned.
		const float epsilon = 0.01f;

		while (
			Mathf.Abs(Mathf.DeltaAngle(yaw, ty)) > epsilon ||
			Mathf.Abs(pitch - tp) > epsilon
		)
		{
			// Interpret speedDegPerSecond as the approximate rate to cover 90 degrees.
			// This yields an ease-in/out feel closer to the drag-based friction of the orbit.
			float lerpFactor = Mathf.Clamp01(speedDegPerSecond * Time.deltaTime / 90f);
			yaw = Mathf.LerpAngle(yaw, ty, lerpFactor);
			pitch = Mathf.Lerp(pitch, tp, lerpFactor);
			yield return null;
		}
	}

	public void SetMaxZoomLock(bool enabled)
	{
		maxZoomLock = enabled;

		if (!enabled)
			targetFov = lastFreeFov;
	}

	// ----------------------------------------------------
	// UTIL
	float ClampFov(float fov)
	{
		return Mathf.Clamp(fov, minFov, maxFov);
	}
	// ----------------------------------------------------
	float NormalizeAngle(float angle)
	{
		if (angle > 180f) angle -= 360f;
		return angle;
	}
}
