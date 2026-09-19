using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[AddComponentMenu("Splines/Spline Sync")]
public class SplineSync : MonoBehaviour
{
	[Header("Targets")]
	[Tooltip("Only SplineTracer components are allowed here.")]
	[SerializeField] private SplineTracer[] targets = Array.Empty<SplineTracer>();

	[SerializeField] private bool autoFindInChildren = false;
	[SerializeField] private bool includeInactive = true;

	[Header("Driver")]
	[Range(0f, 1f)]
	[SerializeField] private float percentage = 0.5f;

	[Tooltip("Smoothing response time in seconds. Zero uses the default 0.25-second response.")]
	[Min(0f)]
	[SerializeField] private float smoothTime = 0.25f;

	[Header("Behaviour")]
	[SerializeField] private bool applyInEditMode = false;

	[Tooltip("If true, refreshes each tracer immediately after changing its range.")]
	[SerializeField] private bool refreshImmediately = true;

	private float _currentPercentage;
	private float _velocity;

	public float Percentage
	{
		get => percentage;
		set => percentage = value;
	}

	public SplineTracer[] Targets
	{
		get => targets;
		set => targets = value ?? Array.Empty<SplineTracer>();
	}

	private void OnEnable()
	{
		if (autoFindInChildren)
			RefreshTargets();

		_currentPercentage = percentage;
		_velocity = 0f;
		if (Application.isPlaying)
			ApplyToTargets(true);
	}

	private void OnValidate()
	{
		if (autoFindInChildren)
			RefreshTargets();

		if (!Application.isPlaying) _currentPercentage = percentage;
		// Do not apply in edit mode to avoid conflicting with tracer edit-time state.
	}

	private void Update()
	{
		if (!Application.isPlaying && !applyInEditMode)
			return;

		if (autoFindInChildren)
			RefreshTargets();

		if (!Application.isPlaying)
		{
			_currentPercentage = percentage;
		}
		else
		{
			_currentPercentage = Mathf.SmoothDamp(
				_currentPercentage,
				percentage,
				ref _velocity,
				smoothTime > 0f ? smoothTime : 0.25f
			);
		}

		ApplyToTargets(false);
	}

	[ContextMenu("Refresh Targets")]
	public void RefreshTargets()
	{
		targets = includeInactive
			? GetComponentsInChildren<SplineTracer>(true)
			: GetComponentsInChildren<SplineTracer>(false);
	}

	[ContextMenu("Apply Now")]
	public void ApplyNow()
	{
		_currentPercentage = percentage;
		_velocity = 0f;
		ApplyToTargets(true);
	}

	public void SetPercentage(float value, bool snap = false)
	{
		percentage = value;

		if (snap)
		{
			_currentPercentage = percentage;
			_velocity = 0f;
			ApplyToTargets(true);
		}
	}

	private void ApplyToTargets(bool forceRefresh)
	{
		if (targets == null || targets.Length == 0)
			return;

		for (int i = 0; i < targets.Length; i++)
		{
			SplineTracer tracer = targets[i];
			if (tracer == null)
				continue;

			float oldStart = tracer.StartPercent;
			float oldEnd = tracer.EndPercent;

			float width = oldEnd - oldStart;
			float newPosition = _currentPercentage;

			bool changed =
				!Mathf.Approximately(tracer.Position, newPosition) ||
				!Mathf.Approximately(tracer.GetWidthPercent(), width);

			tracer.Position = newPosition;
			tracer.SetWidthPercent(width);

			if (refreshImmediately && (forceRefresh || changed))
				tracer.RefreshNow();

#if UNITY_EDITOR
			if (!Application.isPlaying && changed)
			{
				EditorUtility.SetDirty(tracer);
				if (tracer.gameObject != null)
					EditorUtility.SetDirty(tracer.gameObject);
			}
#endif
		}

#if UNITY_EDITOR
		if (!Application.isPlaying)
			EditorUtility.SetDirty(this);
#endif
	}
}
