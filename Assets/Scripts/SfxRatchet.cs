using UnityEngine;

[DisallowMultipleComponent]
public sealed class SfxRatchet : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.25f;
    [Range(0f, 1f)] public float volumeDeviation = 0.02f;
    [Tooltip("Angular distance between ratchet teeth. Smaller spacing produces more ticks.")]
    [Range(1f, 90f)] public float degreesPerTick = 15f;
    [Range(0.1f, 10f)] public float maximumRate = 10f;
    [Range(0f, 0.5f)] public float spacingDeviation = 0.05f;
    [Range(0.1f, 3f)] public float minimumPitch = 0.85f;
    [Range(0.1f, 3f)] public float maximumPitch = 1.2f;
    [Range(0f, 1f)] public float pitchDeviation = 0.04f;

    const int VoiceCount = 8;
    readonly AudioSource[] _voices = new AudioSource[VoiceCount];
    readonly float[] _volumeOffsets = new float[VoiceCount];
    float _travel, _spacingVariation = 1f;
    double _lastStart = double.NegativeInfinity;

    void OnEnable() => ResetPlayback();

    internal void CopySettingsFrom(SfxRatchet baseline)
    {
        if (baseline == null) return;
        clip = baseline.clip;
        volume = baseline.volume;
        volumeDeviation = baseline.volumeDeviation;
        degreesPerTick = baseline.degreesPerTick;
        maximumRate = baseline.maximumRate;
        spacingDeviation = baseline.spacingDeviation;
        minimumPitch = baseline.minimumPitch;
        maximumPitch = baseline.maximumPitch;
        pitchDeviation = baseline.pitchDeviation;
        enabled = baseline.enabled;
    }

    public void Initialize()
    {
        if (!Application.isPlaying) return;
        for (int i = 0; i < VoiceCount; i++)
        {
            if (_voices[i] != null) continue;
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            _voices[i] = source;
        }
    }

    public void AdvanceRotation(float deltaDegrees, float degreesPerSecond, float maximumSpeed)
    {
        if (!float.IsFinite(deltaDegrees) || !float.IsFinite(degreesPerSecond) || !float.IsFinite(maximumSpeed)
            || maximumSpeed <= 0f || Mathf.Abs(degreesPerSecond) < 1f || deltaDegrees == 0f)
        {
            ResetPlayback();
            return;
        }
        if (!isActiveAndEnabled) return;
        Initialize();
        float speed = Mathf.Clamp01(Mathf.Abs(degreesPerSecond) / maximumSpeed);
        float spacing = Safe(degreesPerTick, 1f, 90f) * _spacingVariation;
        _travel += Mathf.Abs(deltaDegrees);
        if (_travel < spacing) return;
        // Crossed teeth are consumed now, including ticks dropped by the cap.
        // Time passing without rotation can never produce a tick.
        _travel %= spacing;
        float deviation = Safe(spacingDeviation, 0f, 0.5f);
        _spacingVariation = 1f + Random.Range(-deviation, deviation);
        double now = Time.unscaledTimeAsDouble;
        float cap = Safe(maximumRate, 0.1f, 10f);
        if (now - _lastStart < 1.0 / cap) return;
        _lastStart = now;
        float level = Safe(volume, 0f, 1f);
        if (clip == null || level == 0f) return;
        for (int i = 0; i < VoiceCount; i++)
        {
            var voice = _voices[i];
            if (voice == null || voice.isPlaying) continue;
            float pitchMax = Safe(maximumPitch, 0.1f, 3f);
            float pitchMin = Safe(minimumPitch, 0.1f, pitchMax);
            float pitchJitter = Safe(pitchDeviation, 0f, 1f);
            voice.pitch = Mathf.Clamp(Mathf.Lerp(pitchMin, pitchMax, speed)
                + Random.Range(-pitchJitter, pitchJitter), pitchMin, pitchMax);
            float volumeJitter = Safe(volumeDeviation, 0f, 1f);
            _volumeOffsets[i] = Random.Range(-volumeJitter, volumeJitter);
            voice.volume = Mathf.Clamp01(level + _volumeOffsets[i]);
            voice.clip = clip;
            voice.Play();
            break;
        }
    }

    void Update()
    {
        float level = Safe(volume, 0f, 1f);
        for (int i = 0; i < VoiceCount; i++)
            if (_voices[i] != null)
                _voices[i].volume = level == 0f ? 0f : Mathf.Clamp01(level + _volumeOffsets[i]);
    }

    static float Safe(float value, float min, float max) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : min;

    public void ResetPlayback()
    {
        _travel = 0f;
        _spacingVariation = 1f;
        // Retain last-start time so resets cannot bypass the playback cap.
        foreach (var voice in _voices)
            if (voice != null) voice.Stop();
    }

    void OnDisable() => ResetPlayback();

    void OnDestroy()
    {
        ResetPlayback();
        foreach (var voice in _voices)
            if (voice != null) Destroy(voice);
    }
}
