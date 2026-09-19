using System;
using System.Collections.Generic;
using UnityEngine;

public class CraterCreator : MonoBehaviour
{
	public enum RingTopType { FlatTop, PointyTop }

	[Header("Prefabs")]
	[Tooltip("Segment prefabs authored as flat-top orientation.")]
	public GameObject[] flatTopSegments;

	[Tooltip("Segment prefabs authored as pointy-top orientation.")]
	public GameObject[] pointyTopSegments;

	[Tooltip("Optional feature prefabs to attach to a segment (e.g., vents, spires, debris, etc.).")]
	public GameObject[] featurePrefabs;

	[Header("Ring Settings")]
	public RingTopType ringType = RingTopType.FlatTop;

	[Tooltip("How many segments in the ring. 6 = 60 degrees per segment.")]
	[Range(1, 64)]
	public int segments = 6;

	[Tooltip("Radius from spawner origin to place each segment.")]
	public float radius = 50f;

	[Tooltip("Optional extra yaw offset for the entire ring (degrees).")]
	public float ringYawOffset = 0f;

	[Tooltip("If true, each segment is rotated to face outward.")]
	public bool faceOutward = true;

	[Tooltip("If true, each segment gets a feature (chosen per rules below).")]
	public bool enableFeatures = true;

	[Header("Feature Placement")]
	[Tooltip("If >= 0, put feature only on this segment index. If -1, use random selection.")]
	public int featureSegmentIndex = -1;

	[Tooltip("If true and featureSegmentIndex == -1, pick one random segment for the feature.")]
	public bool pickOneRandomFeatureSegment = true;

	[Tooltip("Chance a segment gets a feature when pickOneRandomFeatureSegment == false (0..1).")]
	[Range(0f, 1f)]
	public float perSegmentFeatureChance = 0.15f;

	[Tooltip("Local offset for the feature relative to its segment.")]
	public Vector3 featureLocalOffset = Vector3.zero;

	[Tooltip("Local rotation for the feature relative to its segment.")]
	public Vector3 featureLocalEuler = Vector3.zero;

	[Tooltip("If true, feature gets a random yaw rotation (0..360) on top of featureLocalEuler.")]
	public bool randomizeFeatureYaw = true;

	[Header("Lifecycle")]
	[Tooltip("If true, destroys previously spawned ring children before spawning again.")]
	public bool clearPreviousChildren = true;

	[Tooltip("Optional parent transform for spawned segments (defaults to this.transform).")]
	public Transform segmentsParentOverride;

	[ContextMenu("Spawn Ring")]
	public void SpawnRing()
	{
		var parent = segmentsParentOverride ? segmentsParentOverride : transform;

		if (clearPreviousChildren)
			ClearSpawnedChildren(parent);

		var segmentPrefabs = GetSegmentArray(ringType);
		if (segmentPrefabs == null || segmentPrefabs.Length == 0)
		{
			Debug.LogError($"[{nameof(CraterCreator)}] No segment prefabs assigned for {ringType}.");
			return;
		}

		if (segments <= 0)
		{
			Debug.LogError($"[{nameof(CraterCreator)}] segments must be > 0.");
			return;
		}

		// Decide which segment(s) will get features
		HashSet<int> featureIndices = new HashSet<int>();
		if (enableFeatures && featurePrefabs != null && featurePrefabs.Length > 0)
		{
			if (featureSegmentIndex >= 0)
			{
				featureIndices.Add(Mod(featureSegmentIndex, segments));
			}
			else if (pickOneRandomFeatureSegment)
			{
				featureIndices.Add(UnityEngine.Random.Range(0, segments));
			}
			else
			{
				for (int i = 0; i < segments; i++)
				{
					if (UnityEngine.Random.value <= perSegmentFeatureChance)
						featureIndices.Add(i);
				}
			}
		}

		float stepDeg = 360f / segments; // if segments=6 -> 60 degrees

		for (int i = 0; i < segments; i++)
		{
			float yaw = (i * stepDeg) + ringYawOffset;
			Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

			Vector3 localOffset = rot * (Vector3.forward * radius);
			Vector3 pos = transform.position + localOffset;

			GameObject segPrefab = segmentPrefabs[UnityEngine.Random.Range(0, segmentPrefabs.Length)];
			if (!segPrefab) continue;

			Quaternion segRot;
			if (faceOutward)
			{
				// outward = from center to segment
				Vector3 outward = (pos - transform.position);
				outward.y = 0f;
				if (outward.sqrMagnitude < 0.0001f) outward = Vector3.forward;
				segRot = Quaternion.LookRotation(outward.normalized, Vector3.up);
			}
			else
			{
				segRot = rot;
			}

			var seg = Instantiate(segPrefab, pos, segRot, parent);
			seg.name = $"{segPrefab.name}_Seg_{i:00}";

			// Optional: attach a feature to this segment
			if (featureIndices.Contains(i))
			{
				SpawnFeatureOnSegment(seg.transform);
			}
		}
	}

	void SpawnFeatureOnSegment(Transform segment)
	{
		if (!segment) return;
		if (featurePrefabs == null || featurePrefabs.Length == 0) return;

		var featurePrefab = featurePrefabs[UnityEngine.Random.Range(0, featurePrefabs.Length)];
		if (!featurePrefab) return;

		var feature = Instantiate(featurePrefab, segment);
		feature.name = $"{featurePrefab.name}_Feature";

		feature.transform.localPosition = featureLocalOffset;

		Quaternion baseRot = Quaternion.Euler(featureLocalEuler);
		if (randomizeFeatureYaw)
			baseRot = baseRot * Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

		feature.transform.localRotation = baseRot;
	}

	GameObject[] GetSegmentArray(RingTopType type)
	{
		return type == RingTopType.FlatTop ? flatTopSegments : pointyTopSegments;
	}

	void ClearSpawnedChildren(Transform parent)
	{
		if (!parent) return;

		// Only delete children under the chosen parent (safe for iterating).
		for (int i = parent.childCount - 1; i >= 0; i--)
		{
			var child = parent.GetChild(i);
#if UNITY_EDITOR
			if (!Application.isPlaying)
				DestroyImmediate(child.gameObject);
			else
				Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
		}
	}

	static int Mod(int x, int m)
	{
		int r = x % m;
		return r < 0 ? r + m : r;
	}
}
