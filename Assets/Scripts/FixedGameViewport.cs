using UnityEngine;

/// <summary>
/// Keeps the game rendering directly to a fixed 1280x720 backbuffer.
/// This intentionally does not use a RenderTexture or resize individual UI elements.
/// </summary>
public sealed class FixedGameViewport : MonoBehaviour
{
	const int TargetWidth = 1280;
	const int TargetHeight = 720;
	const float TargetAspect = 16f / 9f;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Install()
	{
		var existing = FindFirstObjectByType<FixedGameViewport>();
		if (existing != null) return;

		var host = new GameObject(nameof(FixedGameViewport));
		DontDestroyOnLoad(host);
		host.AddComponent<FixedGameViewport>();
	}

	void Awake()
	{
		DontDestroyOnLoad(gameObject);
		Application.runInBackground = true;
		ApplyResolution();
	}

	void Update()
	{
		if (Screen.width != TargetWidth || Screen.height != TargetHeight)
		{
			ApplyResolution();
		}

		var mainCamera = Camera.main;
		if (mainCamera == null) return;

		mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
		mainCamera.aspect = TargetAspect;
	}

	static void ApplyResolution()
	{
		Screen.SetResolution(TargetWidth, TargetHeight, FullScreenMode.Windowed);
	}
}
