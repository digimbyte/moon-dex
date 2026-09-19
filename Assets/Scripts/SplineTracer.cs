using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Serialization;

[ExecuteAlways]
[AddComponentMenu("Splines/SplineTracer")]
public class SplineTracer : MonoBehaviour
{
	public enum SplineEndBehavior
	{
		Clamp = 0,
		Overdrive = 1,
		SmoothLoop = 2,
		SharpLoop = 3,
		Teleport = 4
	}

	[Header("Spline")]
	[SerializeField] private SplineContainer splineContainer;

	[Tooltip("Objects will be positioned in this exact order along the spline.")]
	[SerializeField] private GameObject[] items = Array.Empty<GameObject>();

	[Tooltip("Reverse the item order before placing them on the spline.")]
	[SerializeField] private bool reverseOrder = false;

	[Header("Placement Range")]
	[FormerlySerializedAs("startPercent")]
	[SerializeField] private float position = 0.5f;

	[FormerlySerializedAs("endPercent")]
	[SerializeField] private float width = 1f;

	[Tooltip("If true, items are spaced by arc length (curve aware / adaptive).\nIf false, items are spaced by normalized spline t (linear spacing).")]
	[SerializeField] private bool adaptiveSpacing = true;

	[Header("Reorder Smoothing")]
	[Tooltip("0 = instant teleport. 1 = takes 1 second to slide to new assigned slot.")]
	[Min(0f)]
	[SerializeField] private float reorderDuration = 0f;

	[Header("Motion")]
	[Tooltip("How fast the whole item layout moves along the spline segment.")]
	[SerializeField] private float move = 0f;

	[Tooltip("If true, 'move' is interpreted as meters per second.\nIf false, 'move' is normalized segment units per second.")]
	[SerializeField] private bool moveInMetersPerSecond = false;

	[Header("End Behavior")]
	[SerializeField] private SplineEndBehavior endBehavior = SplineEndBehavior.Clamp;

	[Tooltip("Used by SmoothLoop and SharpLoop. This is the virtual bridge size beyond each end before re-entering the other side.")]
	[Min(0.0001f)]
	[SerializeField] private float loopBridgeSize = 0.1f;

	[Tooltip("Used by Teleport. Small stability buffer around wrap points.")]
	[Min(0f)]
	[SerializeField] private float teleportEndBuffer = 0.0001f;

	[Header("Orientation")]
	[SerializeField] private bool alignToSpline = false;

	[Tooltip("If true, uses spline up vector. If false, uses world up.")]
	[SerializeField] private bool useSplineUp = true;

	[Header("Runtime")]
	[Tooltip("Apply placement during edit mode as well.")]
	[SerializeField] private bool updateInEditMode = true;

	[Tooltip("Samples used to build the arc-length lookup table for adaptive spacing and meter movement.")]
	[Range(16, 2048)]
	[SerializeField] private int arcLengthSamples = 256;

	[Serializable]
	private class TracedItem
	{
		public GameObject go;
		public float currentLocalT;
		public float targetLocalT;
		public bool initialized;
	}

	private readonly Dictionary<GameObject, TracedItem> _tracked = new();
	private readonly List<GameObject> _orderedValidItems = new();

	private float[] _sampleT = Array.Empty<float>();
	private float[] _sampleDistance = Array.Empty<float>();
	private float _splineWorldLength;

	private float _segmentOffsetNormalized;

	public GameObject[] Items
	{
		get => items;
		set
		{
			items = value ?? Array.Empty<GameObject>();
			RebuildTargets(true);
		}
	}

	public float Position
	{
		get
		{
			if (Mathf.Abs(position) > 1f)
				return position * 0.01f;

			return position;
		}
		set
		{
			if (Mathf.Abs(position) > 1f)
				position = value * 100f;
			else
				position = value;
		}
	}

	public float Width
	{
		get => width;
		set => width = value;
	}

	public void SetWidthPercent(float wPercent)
	{
		width = WidthPercentToStored(wPercent);
	}

	public float GetWidthPercent()
	{
		return WidthToPercent();
	}

	private float WidthToPercent()
	{
		float w = width;
		if (Mathf.Abs(w) > 1f)
		{
			float len = _splineWorldLength;
			if (len <= 0f && splineContainer != null)
				len = Mathf.Max(0.0001f, splineContainer.CalculateLength());

			if (len <= 0f)
				return w; // fallback

			return w / len;
		}

		return w;
	}

	private float WidthPercentToStored(float wPercent)
	{
		// Preserve existing unit intent: if current stored width looked like meters (>1), store meters.
		if (Mathf.Abs(width) > 1f)
		{
			float len = _splineWorldLength;
			if (len <= 0f && splineContainer != null)
				len = Mathf.Max(0.0001f, splineContainer.CalculateLength());

			return wPercent * Mathf.Max(0.0001f, len);
		}

		return wPercent;
	}

	public float StartPercent
	{
		get
		{
			float wPerc = WidthToPercent();
			float posNorm = Position;
			if (wPerc >= 0f)
				return posNorm;

			return posNorm + wPerc;
		}
		set
		{
			float currentEnd = EndPercent;
			float newStart = value;
			float wPerc = currentEnd - newStart;

			width = WidthPercentToStored(wPerc);
			Position = (wPerc >= 0f) ? newStart : currentEnd;
		}
	}

	public float EndPercent
	{
		get
		{
			float wPerc = WidthToPercent();
			float posNorm = Position;
			if (wPerc >= 0f)
				return posNorm + wPerc;

			return posNorm;
		}
		set
		{
			float currentStart = StartPercent;
			float newEnd = value;
			float wPerc = newEnd - currentStart;

			width = WidthPercentToStored(wPerc);
			Position = (wPerc >= 0f) ? currentStart : newEnd;
		}
	}

	public void RefreshNow()
	{
		RebuildTargets(true);
		ApplyTransforms(0f, true);
	}

	private void OnEnable()
	{
		Warmup();
		RebuildTargets(true);
		ApplyTransforms(0f, true);
	}

	private void Start()
	{
		Warmup();
		RebuildTargets(true);
		ApplyTransforms(0f, true);
	}

	private void OnValidate()
	{
		loopBridgeSize = Mathf.Max(0.0001f, loopBridgeSize);
		teleportEndBuffer = Mathf.Max(0f, teleportEndBuffer);

		Warmup();
		RebuildTargets(true);

		if (!Application.isPlaying)
			ApplyTransforms(0f, true);
	}

	private void Update()
	{
		if (splineContainer == null)
			return;

		if (!Application.isPlaying && !updateInEditMode)
			return;

		float dt = Application.isPlaying ? Time.deltaTime : 0f;

		Warmup();
		RebuildTargets(false);
		AdvanceMotion(dt);
		ApplyTransforms(dt, false);
	}

	private void Warmup()
	{
		if (splineContainer == null)
			return;

		splineContainer.Warmup();
		RebuildArcLengthTable();
	}

	private void RebuildArcLengthTable()
	{
		int samples = Mathf.Max(16, arcLengthSamples);

		if (_sampleT.Length != samples + 1)
		{
			_sampleT = new float[samples + 1];
			_sampleDistance = new float[samples + 1];
		}

		_splineWorldLength = Mathf.Max(0.0001f, splineContainer.CalculateLength());

		Vector3 prev = splineContainer.EvaluatePosition(0f);
		_sampleT[0] = 0f;
		_sampleDistance[0] = 0f;

		float cumulative = 0f;

		for (int i = 1; i <= samples; i++)
		{
			float t = i / (float)samples;
			Vector3 pos = splineContainer.EvaluatePosition(t);

			cumulative += Vector3.Distance(prev, pos);

			_sampleT[i] = t;
			_sampleDistance[i] = cumulative;

			prev = pos;
		}

		_splineWorldLength = Mathf.Max(0.0001f, cumulative);
	}

	private void RebuildTargets(bool resetMissing)
	{
		_orderedValidItems.Clear();

		if (items == null)
			items = Array.Empty<GameObject>();

		for (int i = 0; i < items.Length; i++)
		{
			if (items[i] != null)
				_orderedValidItems.Add(items[i]);
		}

		if (reverseOrder)
			_orderedValidItems.Reverse();

		if (resetMissing)
		{
			var toRemove = new List<GameObject>();
			foreach (var kvp in _tracked)
			{
				if (!_orderedValidItems.Contains(kvp.Key))
					toRemove.Add(kvp.Key);
			}

			for (int i = 0; i < toRemove.Count; i++)
				_tracked.Remove(toRemove[i]);
		}

		int count = _orderedValidItems.Count;
		if (count == 0)
			return;

		for (int i = 0; i < count; i++)
		{
			GameObject go = _orderedValidItems[i];
			float targetLocalT = GetSlotLocalT(i, count);

			if (!_tracked.TryGetValue(go, out TracedItem traced))
			{
				traced = new TracedItem
				{
					go = go,
					currentLocalT = targetLocalT,
					targetLocalT = targetLocalT,
					initialized = true
				};

				_tracked.Add(go, traced);
			}
			else
			{
				traced.targetLocalT = targetLocalT;

				if (!traced.initialized)
				{
					traced.currentLocalT = targetLocalT;
					traced.initialized = true;
				}
			}
		}
	}

	private void AdvanceMotion(float dt)
	{
		if (Mathf.Approximately(move, 0f) || dt <= 0f)
			return;

		float sectionLengthNormalized = Mathf.Abs(EndPercent - StartPercent);
		if (sectionLengthNormalized <= 0.000001f)
			return;

		float deltaNormalized;

		if (moveInMetersPerSecond)
		{
			float sectionLengthMeters = GetVirtualSectionWorldLength(StartPercent, EndPercent);
			if (sectionLengthMeters <= 0.000001f)
				return;

			deltaNormalized = (move * dt) / sectionLengthMeters;
		}
		else
		{
			deltaNormalized = move * dt;
		}

		_segmentOffsetNormalized += deltaNormalized;

		if (endBehavior == SplineEndBehavior.Teleport)
			NormalizeTeleportOffset();
	}

	private void NormalizeTeleportOffset()
	{
		float cycle = 1f;
		if (Mathf.Abs(_segmentOffsetNormalized) > 10000f)
			_segmentOffsetNormalized = Mathf.Repeat(_segmentOffsetNormalized, cycle);
	}

	private void ApplyTransforms(float dt, bool forceInstant)
	{
		float directionSign = GetSegmentDirectionSign();

		foreach (var kvp in _tracked)
		{
			TracedItem traced = kvp.Value;
			if (traced.go == null)
				continue;

			if (forceInstant || reorderDuration <= 0f || dt <= 0f)
			{
				traced.currentLocalT = traced.targetLocalT;
			}
			else
			{
				float step = dt / reorderDuration;
				traced.currentLocalT = Mathf.Lerp(traced.currentLocalT, traced.targetLocalT, step);

				if (Mathf.Abs(traced.currentLocalT - traced.targetLocalT) < 0.0001f)
					traced.currentLocalT = traced.targetLocalT;
			}

			float localTWithMotion = traced.currentLocalT + _segmentOffsetNormalized;

			bool ok;
			float3 pos;
			float3 tangent;
			float3 up;

			if (adaptiveSpacing)
			{
				float startDistance = PercentToAdaptiveVirtualDistance(StartPercent);
				float endDistance = PercentToAdaptiveVirtualDistance(EndPercent);
				float targetDistance = Mathf.Lerp(startDistance, endDistance, localTWithMotion);

				ok = EvaluateAdaptiveVirtualDistance(targetDistance, directionSign, out pos, out tangent, out up);
			}
			else
			{
				float rawPercent = Mathf.Lerp(StartPercent, EndPercent, localTWithMotion);
				ok = EvaluateLinearPercent(rawPercent, directionSign, out pos, out tangent, out up);
			}

			if (!ok)
				continue;

			Transform tr = traced.go.transform;
			tr.position = pos;

			if (alignToSpline)
			{
				Vector3 forward = ((Vector3)tangent).normalized;
				if (forward.sqrMagnitude > 0.000001f)
				{
					Vector3 upDir = useSplineUp ? ((Vector3)up).normalized : Vector3.up;
					if (upDir.sqrMagnitude <= 0.000001f)
						upDir = Vector3.up;

					tr.rotation = Quaternion.LookRotation(forward, upDir);
				}
			}
		}
	}

	private float GetSlotLocalT(int index, int count)
	{
		if (count <= 1)
			return 0f;

		float ratio = index / (float)(count - 1);

		if (!adaptiveSpacing)
			return ratio;

		return ratio;
	}

	private float GetSegmentDirectionSign()
	{
		float delta = EndPercent - StartPercent;
		if (Mathf.Approximately(delta, 0f))
			return 1f;

		return Mathf.Sign(delta);
	}

	private float GetVirtualSectionWorldLength(float aPercent, float bPercent)
	{
		if (adaptiveSpacing)
		{
			float a = PercentToAdaptiveVirtualDistance(aPercent);
			float b = PercentToAdaptiveVirtualDistance(bPercent);
			return Mathf.Abs(b - a);
		}

		float aT = Mathf.Clamp01(aPercent);
		float bT = Mathf.Clamp01(bPercent);

		float aD = GetDistanceAtT(aT);
		float bD = GetDistanceAtT(bT);

		float outsideA = Mathf.Abs(aPercent - aT) * _splineWorldLength;
		float outsideB = Mathf.Abs(bPercent - bT) * _splineWorldLength;

		return Mathf.Abs(bD - aD) + outsideA + outsideB;
	}

	private float PercentToAdaptiveVirtualDistance(float percent)
	{
		if (percent < 0f)
			return percent * _splineWorldLength;

		if (percent > 1f)
			return _splineWorldLength + ((percent - 1f) * _splineWorldLength);

		return GetDistanceAtT(percent);
	}

	private bool EvaluateLinearPercent(float rawPercent, float directionSign, out float3 pos, out float3 tangent, out float3 up)
	{
		pos = default;
		tangent = default;
		up = new float3(0f, 1f, 0f);

		switch (endBehavior)
		{
			case SplineEndBehavior.Clamp:
				{
					float t = Mathf.Clamp01(rawPercent);
					return EvaluateInsidePercent(t, directionSign, out pos, out tangent, out up);
				}

			case SplineEndBehavior.Overdrive:
				{
					if (rawPercent >= 0f && rawPercent <= 1f)
						return EvaluateInsidePercent(rawPercent, directionSign, out pos, out tangent, out up);

					if (rawPercent < 0f)
					{
						if (!EvaluateInsidePercent(0f, directionSign, out pos, out tangent, out up))
							return false;

						float overshootMeters = (-rawPercent) * _splineWorldLength;
						Vector3 dir = -((Vector3)tangent).normalized;
						pos += (float3)(dir * overshootMeters);
						tangent = (float3)dir;
						return true;
					}
					else
					{
						if (!EvaluateInsidePercent(1f, directionSign, out pos, out tangent, out up))
							return false;

						float overshootMeters = (rawPercent - 1f) * _splineWorldLength;
						Vector3 dir = ((Vector3)tangent).normalized;
						pos += (float3)(dir * overshootMeters);
						tangent = (float3)dir;
						return true;
					}
				}

			case SplineEndBehavior.Teleport:
				{
					float t = RepeatWithBuffer(rawPercent, 1f, teleportEndBuffer);
					return EvaluateInsidePercent(t, directionSign, out pos, out tangent, out up);
				}

			case SplineEndBehavior.SharpLoop:
				{
					return EvaluateClosedPercent(rawPercent, directionSign, false, out pos, out tangent, out up);
				}

			case SplineEndBehavior.SmoothLoop:
				{
					return EvaluateClosedPercent(rawPercent, directionSign, true, out pos, out tangent, out up);
				}
		}

		return false;
	}

	private bool EvaluateAdaptiveVirtualDistance(float rawDistance, float directionSign, out float3 pos, out float3 tangent, out float3 up)
	{
		pos = default;
		tangent = default;
		up = new float3(0f, 1f, 0f);

		switch (endBehavior)
		{
			case SplineEndBehavior.Clamp:
				{
					float d = Mathf.Clamp(rawDistance, 0f, _splineWorldLength);
					return EvaluateInsideDistance(d, directionSign, out pos, out tangent, out up);
				}

			case SplineEndBehavior.Overdrive:
				{
					if (rawDistance >= 0f && rawDistance <= _splineWorldLength)
						return EvaluateInsideDistance(rawDistance, directionSign, out pos, out tangent, out up);

					if (rawDistance < 0f)
					{
						if (!EvaluateInsideDistance(0f, directionSign, out pos, out tangent, out up))
							return false;

						Vector3 dir = -((Vector3)tangent).normalized;
						pos += (float3)(dir * (-rawDistance));
						tangent = (float3)dir;
						return true;
					}
					else
					{
						if (!EvaluateInsideDistance(_splineWorldLength, directionSign, out pos, out tangent, out up))
							return false;

						Vector3 dir = ((Vector3)tangent).normalized;
						pos += (float3)(dir * (rawDistance - _splineWorldLength));
						tangent = (float3)dir;
						return true;
					}
				}

			case SplineEndBehavior.Teleport:
				{
					float bufferMeters = teleportEndBuffer * _splineWorldLength;
					float d = RepeatWithBuffer(rawDistance, _splineWorldLength, bufferMeters);
					return EvaluateInsideDistance(d, directionSign, out pos, out tangent, out up);
				}

			case SplineEndBehavior.SharpLoop:
				{
					return EvaluateClosedDistance(rawDistance, directionSign, false, out pos, out tangent, out up);
				}

			case SplineEndBehavior.SmoothLoop:
				{
					return EvaluateClosedDistance(rawDistance, directionSign, true, out pos, out tangent, out up);
				}
		}

		return false;
	}

	private bool EvaluateInsidePercent(float t, float directionSign, out float3 pos, out float3 tangent, out float3 up)
	{
		t = Mathf.Clamp01(t);

		if (!splineContainer.Evaluate(t, out pos, out tangent, out up))
			return false;

		if (directionSign < 0f)
			tangent = -tangent;

		return true;
	}

	private bool EvaluateInsideDistance(float distance, float directionSign, out float3 pos, out float3 tangent, out float3 up)
	{
		float t = GetTAtDistance(Mathf.Clamp(distance, 0f, _splineWorldLength));
		return EvaluateInsidePercent(t, directionSign, out pos, out tangent, out up);
	}

	private bool EvaluateClosedPercent(float rawPercent, float directionSign, bool smooth, out float3 pos, out float3 tangent, out float3 up)
	{
		pos = default;
		tangent = default;
		up = new float3(0f, 1f, 0f);

		float bridge = Mathf.Max(0.0001f, loopBridgeSize);
		float cycle = 1f + bridge;
		float c = Mathf.Repeat(rawPercent, cycle);

		if (c <= 1f)
			return EvaluateInsidePercent(c, directionSign, out pos, out tangent, out up);

		float alpha = (c - 1f) / bridge;
		return EvaluateBridgePercent(alpha, directionSign, smooth, out pos, out tangent, out up);
	}

	private bool EvaluateClosedDistance(float rawDistance, float directionSign, bool smooth, out float3 pos, out float3 tangent, out float3 up)
	{
		pos = default;
		tangent = default;
		up = new float3(0f, 1f, 0f);

		float bridgeMeters = Mathf.Max(0.0001f, loopBridgeSize * _splineWorldLength);
		float cycle = _splineWorldLength + bridgeMeters;
		float c = Mathf.Repeat(rawDistance, cycle);

		if (c <= _splineWorldLength)
			return EvaluateInsideDistance(c, directionSign, out pos, out tangent, out up);

		float alpha = (c - _splineWorldLength) / bridgeMeters;
		return EvaluateBridgePercent(alpha, directionSign, smooth, out pos, out tangent, out up);
	}

	private bool EvaluateBridgePercent(float alpha, float directionSign, bool smooth, out float3 pos, out float3 tangent, out float3 up)
	{
		pos = default;
		tangent = default;
		up = new float3(0f, 1f, 0f);

		if (!EvaluateInsidePercent(1f, directionSign, out float3 endPos, out float3 endTan, out float3 endUp))
			return false;

		if (!EvaluateInsidePercent(0f, directionSign, out float3 startPos, out float3 startTan, out float3 startUp))
			return false;

		alpha = Mathf.Clamp01(alpha);

		Vector3 p0 = endPos;
		Vector3 p1 = startPos;
		Vector3 m0 = ((Vector3)endTan).normalized;
		Vector3 m1 = ((Vector3)startTan).normalized;

		float chord = Vector3.Distance(p0, p1);
		float tangentScale = Mathf.Max(chord, 0.0001f) * 0.5f;

		if (smooth)
		{
			Vector3 hermitePos = Hermite(
				p0,
				p0 + (m0 * tangentScale),
				p1,
				p1 - (m1 * tangentScale),
				alpha
			);

			Vector3 hermiteTan = HermiteTangent(
				p0,
				p0 + (m0 * tangentScale),
				p1,
				p1 - (m1 * tangentScale),
				alpha
			).normalized;

			pos = hermitePos;
			tangent = hermiteTan;
			up = math.normalize(math.lerp(endUp, startUp, alpha));
			return true;
		}
		else
		{
			pos = math.lerp(endPos, startPos, alpha);

			Vector3 dir = (p1 - p0);
			if (dir.sqrMagnitude <= 0.000001f)
				dir = m0;

			tangent = dir.normalized;
			up = math.normalize(math.lerp(endUp, startUp, alpha));
			return true;
		}
	}

	private static float RepeatWithBuffer(float value, float length, float buffer)
	{
		if (length <= 0f)
			return 0f;

		float wrapped = Mathf.Repeat(value, length);

		if (buffer > 0f)
		{
			if (wrapped >= length - buffer)
				wrapped = 0f;
			else if (wrapped <= buffer)
				wrapped = 0f;
		}

		return wrapped;
	}

	private static Vector3 Hermite(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float tt = t * t;
		float ttt = tt * t;

		return
			((-0.5f * ttt) + (tt) - (0.5f * t)) * p0 +
			((1.5f * ttt) - (2.5f * tt) + 1.0f) * p1 +
			((-1.5f * ttt) + (2.0f * tt) + (0.5f * t)) * p2 +
			((0.5f * ttt) - (0.5f * tt)) * p3;
	}

	private static Vector3 HermiteTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float tt = t * t;

		return
			((-1.5f * tt) + (2f * t) - 0.5f) * p0 +
			((4.5f * tt) - (5f * t)) * p1 +
			((-4.5f * tt) + (4f * t) + 0.5f) * p2 +
			((1.5f * tt) - t) * p3;
	}

	private float GetDistanceAtT(float t)
	{
		t = Mathf.Clamp01(t);

		if (_sampleT.Length == 0)
			return 0f;

		for (int i = 1; i < _sampleT.Length; i++)
		{
			if (t <= _sampleT[i])
			{
				float t0 = _sampleT[i - 1];
				float t1 = _sampleT[i];
				float d0 = _sampleDistance[i - 1];
				float d1 = _sampleDistance[i];

				float lerp = Mathf.InverseLerp(t0, t1, t);
				return Mathf.Lerp(d0, d1, lerp);
			}
		}

		return _sampleDistance[_sampleDistance.Length - 1];
	}

	private float GetTAtDistance(float distance)
	{
		if (_sampleDistance.Length == 0)
			return 0f;

		distance = Mathf.Clamp(distance, 0f, _sampleDistance[_sampleDistance.Length - 1]);

		for (int i = 1; i < _sampleDistance.Length; i++)
		{
			if (distance <= _sampleDistance[i])
			{
				float d0 = _sampleDistance[i - 1];
				float d1 = _sampleDistance[i];
				float t0 = _sampleT[i - 1];
				float t1 = _sampleT[i];

				float lerp = Mathf.InverseLerp(d0, d1, distance);
				return Mathf.Lerp(t0, t1, lerp);
			}
		}

		return 1f;
	}

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		if (splineContainer == null)
			return;

		Gizmos.color = Color.cyan;

		float directionSign = GetSegmentDirectionSign();

		if (adaptiveSpacing)
		{
			float startD = PercentToAdaptiveVirtualDistance(StartPercent);
			float endD = PercentToAdaptiveVirtualDistance(EndPercent);

			if (EvaluateAdaptiveVirtualDistance(startD, directionSign, out float3 startPos, out _, out _) &&
				EvaluateAdaptiveVirtualDistance(endD, directionSign, out float3 endPos, out _, out _))
			{
				Gizmos.DrawSphere(startPos, 0.08f);
				Gizmos.DrawSphere(endPos, 0.08f);
				Gizmos.DrawLine(startPos, endPos);
			}
		}
		else
		{
			if (EvaluateLinearPercent(StartPercent, directionSign, out float3 startPos, out _, out _) &&
				EvaluateLinearPercent(EndPercent, directionSign, out float3 endPos, out _, out _))
			{
				Gizmos.DrawSphere(startPos, 0.08f);
				Gizmos.DrawSphere(endPos, 0.08f);
				Gizmos.DrawLine(startPos, endPos);
			}
		}
	}
#endif
}