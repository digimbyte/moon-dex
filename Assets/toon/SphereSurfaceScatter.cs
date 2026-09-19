using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SphereSurfaceScatter3D : MonoBehaviour
{
	// --------------------------------------------------
	// SPHERE
	// --------------------------------------------------
	[Header("Sphere")]
	public float radius = 10f;

	// --------------------------------------------------
	// SCATTER / DENSITY
	// --------------------------------------------------
	[Header("Scatter")]
	public int spawnAttempts = 1200;
	public float minAngularSeparation = 6f;

	// --------------------------------------------------
	// NOISE
	// --------------------------------------------------
	[Header("3D Noise")]
	public float noiseScale = 2.5f;
	[Range(0f, 1f)]
	public float noiseThreshold = 0.5f;
	public int seed = 1337;

	// --------------------------------------------------
	// LIVE TRANSFORMS
	// --------------------------------------------------
	[Header("Live Transforms")]
	public Vector3 translationOffset = Vector3.zero;

	// --------------------------------------------------
	// PREFABS
	// --------------------------------------------------
	[Header("Prefabs")]
	public GameObject prefabA;
	public GameObject prefabB;
	public GameObject prefabC;
	public GameObject prefabD;

	// --------------------------------------------------
	// INTERNAL STATE
	// --------------------------------------------------
	[System.Serializable]
	private class ScatterPointData : MonoBehaviour
	{
		[HideInInspector] public Vector3 normalWS;
		[HideInInspector] public Vector3 baseScale; // ★ prefab-authored scale
	}

	int _lastSeed = int.MinValue;
	int _lastChildCount = -1;

	Vector3 _lastTranslationOffset;
	float _lastRadius;
	int _lastSeedForLive;

#if UNITY_EDITOR
	bool _regenQueued;

	void OnEnable()
	{
		QueueApplyLiveTransforms();
	}

	void OnValidate()
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode) return;
		if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

		spawnAttempts = Mathf.Max(0, spawnAttempts);
		minAngularSeparation = Mathf.Clamp(minAngularSeparation, 0f, 179f);
		radius = Mathf.Max(0.0001f, radius);
		noiseScale = Mathf.Max(0.0001f, noiseScale);

		if (seed != _lastSeed)
		{
			_lastSeed = seed;
			QueueRegenerate();
		}

		QueueApplyLiveTransforms();
	}

	void Update()
	{
		if (Application.isPlaying) return;
		if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

		if (HasLiveChanged())
			ApplyLiveTransforms();
	}

	void QueueRegenerate()
	{
		if (_regenQueued) return;
		_regenQueued = true;

		EditorApplication.delayCall += () =>
		{
			_regenQueued = false;
			if (this == null) return;
			if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
			Regenerate();
		};
	}

	void QueueApplyLiveTransforms()
	{
		EditorApplication.delayCall += () =>
		{
			if (this == null) return;
			if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
			ApplyLiveTransforms();
		};
	}
#endif

	bool HasLiveChanged()
	{
		if (transform.childCount != _lastChildCount) return true;
		if (_lastTranslationOffset != translationOffset) return true;
		if (!Mathf.Approximately(_lastRadius, radius)) return true;
		if (_lastSeedForLive != seed) return true;
		return false;
	}

#if UNITY_EDITOR
	[ContextMenu("Regenerate Scatter")]
	void Regenerate()
	{
		ClearChildren();
		Spawn();
		ApplyLiveTransforms();
	}

	void ClearChildren()
	{
		while (transform.childCount > 0)
			DestroyImmediate(transform.GetChild(0).gameObject);
	}
#endif

	// --------------------------------------------------
	// SPAWN LOGIC
	// --------------------------------------------------
	void Spawn()
	{
		if (!prefabA && !prefabB && !prefabC && !prefabD)
			return;

		Random.InitState(seed);
		float minDot = Mathf.Cos(minAngularSeparation * Mathf.Deg2Rad);

		Vector3 noiseOffset = new Vector3(seed * 11.3f, seed * 7.7f, seed * 3.1f);

		for (int attempt = 0; attempt < spawnAttempts; attempt++)
		{
			Vector3 cubePoint = new Vector3(
				Random.Range(-1f, 1f),
				Random.Range(-1f, 1f),
				Random.Range(-1f, 1f)
			);

			if (cubePoint == Vector3.zero)
				continue;

			Vector3 normalWS = cubePoint.normalized;

			float noise = Perlin3D(normalWS * noiseScale + noiseOffset);
			if (noise < noiseThreshold)
				continue;

			bool tooClose = false;
			for (int i = 0; i < transform.childCount; i++)
			{
				var other = transform.GetChild(i).GetComponent<ScatterPointData>();
				if (other && Vector3.Dot(normalWS, other.normalWS) > minDot)
				{
					tooClose = true;
					break;
				}
			}
			if (tooClose)
				continue;

			GameObject prefab = PickPrefab();
			if (!prefab) continue;

#if UNITY_EDITOR
			GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
#else
			GameObject go = Instantiate(prefab, transform);
#endif
			var data = go.GetComponent<ScatterPointData>();
			if (!data) data = go.AddComponent<ScatterPointData>();
			data.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
			data.normalWS = normalWS;

			// ★ Capture prefab scale and apply ±10% deterministic deviation
			data.baseScale = go.transform.localScale;

			float t = HashToUnit(seed, transform.childCount);
			float scaleMul = Mathf.Lerp(0.9f, 1.1f, t);
			go.transform.localScale = data.baseScale * scaleMul;
		}
	}

	// --------------------------------------------------
	// LIVE TRANSFORM APPLICATION
	// --------------------------------------------------
	void ApplyLiveTransforms()
	{
		if (!IsValid(translationOffset)) return;
		if (float.IsNaN(radius) || float.IsInfinity(radius)) return;

		_lastChildCount = transform.childCount;
		_lastTranslationOffset = translationOffset;
		_lastRadius = radius;
		_lastSeedForLive = seed;

		Vector3 offsetWS = transform.rotation * translationOffset;

		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child = transform.GetChild(i);
			var data = child.GetComponent<ScatterPointData>();
			if (!data) continue;

			Vector3 normalWS = data.normalWS.normalized;
			float spin = HashToAngle(seed, i);

			child.position = transform.position + normalWS * radius + offsetWS;

			Quaternion align = Quaternion.FromToRotation(Vector3.up, normalWS);
			Quaternion spinQ = Quaternion.AngleAxis(spin, normalWS);
			child.rotation = spinQ * align;

			// ★ DO NOT TOUCH SCALE HERE
		}
	}

	// --------------------------------------------------
	// HELPERS
	// --------------------------------------------------
	GameObject PickPrefab()
	{
		int r = Random.Range(0, 4);
		return r switch
		{
			0 => prefabA,
			1 => prefabB,
			2 => prefabC,
			3 => prefabD,
			_ => null
		};
	}

	static float HashToAngle(int seed, int index)
	{
		unchecked
		{
			int h = seed;
			h = h * 31 + index;
			h ^= (h << 13);
			h ^= (h >> 17);
			h ^= (h << 5);
			return (h & 0xFFFF) / 65535f * 360f;
		}
	}

	// ★ new helper
	static float HashToUnit(int seed, int index)
	{
		unchecked
		{
			int h = seed;
			h = h * 31 + index;
			h ^= (h << 13);
			h ^= (h >> 17);
			h ^= (h << 5);
			return (h & 0xFFFF) / 65535f;
		}
	}

	static float Perlin3D(Vector3 p)
	{
		float xy = Mathf.PerlinNoise(p.x, p.y);
		float yz = Mathf.PerlinNoise(p.y, p.z);
		float zx = Mathf.PerlinNoise(p.z, p.x);
		float yx = Mathf.PerlinNoise(p.y, p.x);
		float zy = Mathf.PerlinNoise(p.z, p.y);
		float xz = Mathf.PerlinNoise(p.x, p.z);
		return (xy + yz + zx + yx + zy + xz) / 6f;
	}

	static bool IsValid(Vector3 v)
	{
		return !(float.IsNaN(v.x) || float.IsInfinity(v.x) ||
				 float.IsNaN(v.y) || float.IsInfinity(v.y) ||
				 float.IsNaN(v.z) || float.IsInfinity(v.z));
	}
}
