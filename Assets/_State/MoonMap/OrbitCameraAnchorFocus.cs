using System.Collections;
using UnityEngine;

/// <summary>
/// Helper script for focusing the OrbitCameraController onto a specific anchor transform.
/// While focusing, user orbit input is disabled. Once the camera pivot is within a small
/// angular threshold of the anchor rotation, control is returned to the user.
///
/// Attach this to your "pip" or anchor object and wire up the OrbitCameraController
/// plus the anchor Transform you want to focus on.
/// </summary>
public class OrbitCameraAnchorFocus : MonoBehaviour
{
	[Header("References")]
	public OrbitCameraController orbitController;
	public Transform anchor;

	[Header("Focus Settings")]
	[Tooltip("Degrees per second to rotate the orbit rig toward the anchor using RotateTo.")]
	public float focusSpeed = 180f;

	[Tooltip("Angular threshold in degrees to consider focus complete.")]
	public float angleEpsilon = 0.1f;

	[Tooltip("Description for this anchor point.")]
	public string description = "";

	[Tooltip("Optional logical zone name used for loading or signaling. If empty, the GameObject name will be used.")]
	public string ZoneName = "";

	/// <summary>
	/// Optional: automatically focus on this GameObject's transform if no anchor is set.
	/// </summary>
	public bool useSelfAsDefaultAnchor = true;

	// Mouse interaction state so we only trigger focus when a full click
	// (MouseDown + MouseUp) happens on this collider, and cancel when the
	// cursor leaves while pressed.
	bool pointerDownInside;

	Coroutine focusRoutine;

	public void Populate(string zoneName, string zoneDesc)
	{
		ZoneName = zoneName;
		description = zoneDesc;
	}
	
	void Reset()
	{
		if (orbitController == null)
		{
			orbitController = FindAnyObjectByType<OrbitCameraController>();
		}

		if (anchor == null && useSelfAsDefaultAnchor)
		{
			anchor = transform;
		}
	}

	/// <summary>
	/// Begin focusing the orbit camera on the configured anchor.
	/// </summary>
	public void Focus()
	{
		if (anchor == null && useSelfAsDefaultAnchor)
		{
			anchor = transform;
		}

		if (orbitController == null || anchor == null)
			return;

		if (focusRoutine != null)
		{
			StopCoroutine(focusRoutine);
		}

		focusRoutine = StartCoroutine(FocusRoutine());
	}

	// Simple 3D click hook: requires a Collider and uses Unity's built-in
	// OnMouse* callbacks. We only trigger Focus when a full click (down+up)
	// happens on this same collider; if the mouse leaves while pressed, we
	// cancel the pending click.
	void OnMouseDown()
	{
		pointerDownInside = true;
	}

	void OnMouseExit()
	{
		// Mouse left the collider while pressed – cancel this click sequence.
		pointerDownInside = false;
	}

	void OnMouseUp()
	{
		if (pointerDownInside)
		{
			// Mouse was pressed and released on this pip without leaving the collider.
			Focus();
		}

		pointerDownInside = false;
	}

	/// <summary>
	/// Convenience overload to focus on a specific anchor at call time.
	/// </summary>
	public void FocusOn(Transform targetAnchor)
	{
		anchor = targetAnchor;
		Focus();
	}

	IEnumerator FocusRoutine()
	{
		Transform pivot = GetOrbitPivot();
		if (pivot == null || orbitController == null)
		{
			yield break;
		}

		if (anchor == null && useSelfAsDefaultAnchor)
		{
			anchor = transform;
		}

		if (anchor == null)
		{
			yield break;
		}

		// Compute the desired yaw/pitch for the orbit controller so it can perform its own
		// smooth rotation rather than this script directly rotating the pivot.
		Quaternion targetWorldRot = anchor.rotation;
		Quaternion targetLocalRot = pivot.parent != null
			? Quaternion.Inverse(pivot.parent.rotation) * targetWorldRot
			: targetWorldRot;

		Vector3 euler = targetLocalRot.eulerAngles;
		float targetYaw = euler.y;
		float targetPitch = NormalizeAngle(euler.x);
		// Clamp pitch to the same limits used by the controller so our completion test matches reality.
		targetPitch = Mathf.Clamp(targetPitch, orbitController.southPoleLimit, orbitController.northPoleLimit);
		// Build a clamped local rotation that matches what RotateTo will actually aim for.
		Quaternion clampedLocalRot = Quaternion.Euler(targetPitch, targetYaw, 0f);

		// Lock user input; ask the controller to ease toward the target. No per-anchor timeout.
		orbitController.SetControlsLocked(true, null);
		orbitController.RotateTo(targetYaw, targetPitch, focusSpeed);

		while (anchor != null)
		{
			float localAngle = Quaternion.Angle(pivot.localRotation, clampedLocalRot);
			if (localAngle <= angleEpsilon)
			{
				break;
			}

			yield return null;
		}

		if (anchor != null)
		{
			Transform pivotNow = GetOrbitPivot();
			if (pivotNow != null)
			{
				pivotNow.localRotation = clampedLocalRot;
			}
		}

		orbitController.SyncAnglesFromPivot();
		orbitController.SetControlsLocked(false, 0f);

		focusRoutine = null;
	}

	Transform GetOrbitPivot()
	{
		if (orbitController == null)
			return null;

		if (orbitController.orbitPivot != null)
			return orbitController.orbitPivot;

		if (orbitController.useParentAsPivot && orbitController.transform.parent != null)
			return orbitController.transform.parent;

		return orbitController.transform;
	}

	// Local copy of the angle normalisation used by OrbitCameraController so we
	// compute pitch in the same way when converting from anchor rotation to yaw/pitch.
	float NormalizeAngle(float angle)
	{
		if (angle > 180f) angle -= 360f;
		return angle;
	}
}
