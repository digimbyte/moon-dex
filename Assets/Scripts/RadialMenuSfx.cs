using UnityEngine;

[DisallowMultipleComponent]
public sealed class RadialMenuSfx : MonoBehaviour
{
    public AudioClip scrollClip;
    public AudioClip selectionClip;
    public AudioClip backClip;
    [Range(0f, 1f)] public float selectionVolume = 0.25f;
    [Range(0f, 1f)] public float backVolume = 0.25f;
    const int VoiceCount = 16;
    readonly AudioSource[] _voices = new AudioSource[VoiceCount];
    readonly bool[] _backVoices = new bool[VoiceCount];
    readonly ulong[] _started = new ulong[VoiceCount];
    ulong _sequence;

    void Awake()
    {
        if (!Application.isPlaying) return;
        for (int i = 0; i < VoiceCount; i++)
            if (_voices[i] == null) _voices[i] = CreateVoice();
    }

    internal void CopySettingsFrom(RadialMenuSfx baseline)
    {
        if (baseline == null) return;
        scrollClip = baseline.scrollClip;
        selectionClip = baseline.selectionClip;
        backClip = baseline.backClip;
        selectionVolume = baseline.selectionVolume;
        backVolume = baseline.backVolume;
        enabled = baseline.enabled;
    }

    AudioSource CreateVoice()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    void Update()
    {
        for (int i = 0; i < VoiceCount; i++)
            if (_voices[i] != null)
                _voices[i].volume = Mathf.Clamp01(_backVoices[i] ? backVolume : selectionVolume);
    }

    public void PlaySelection() => Play(selectionClip, selectionVolume, false);
    public void PlayBack() => Play(backClip, backVolume, true);

    void Play(AudioClip clip, float volume, bool isBack)
    {
        if (!Application.isPlaying || !isActiveAndEnabled
            || clip == null || !float.IsFinite(volume) || volume <= 0f) return;
        int index = 0;
        for (int i = 0; i < VoiceCount; i++)
        {
            if (_voices[i] == null || !_voices[i].isPlaying)
            {
                index = i;
                break;
            }
            if (_started[i] < _started[index]) index = i;
        }
        var source = _voices[index];
        if (source == null) source = _voices[index] = CreateVoice();
        // Only replace an existing sound when every voice is occupied.
        // No pending queue: feedback always belongs to the current click.
        source.Stop();
        _backVoices[index] = isBack;
        _started[index] = ++_sequence;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
    }

    public void StopPlayback()
    {
        foreach (var voice in _voices)
            if (voice != null) voice.Stop();
    }

    void OnDisable() => StopPlayback();
    void OnDestroy()
    {
        StopPlayback();
        foreach (var voice in _voices)
            if (voice != null) Destroy(voice);
    }
}
