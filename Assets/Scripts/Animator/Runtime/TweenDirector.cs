using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Animator.Runtime
{
    public sealed class TweenDirector : MonoBehaviour
    {
        [Header("Asset")]
        public TweenAsset asset;

        [Header("Bindings")]
        public TweenBindings bindings;

        [Header("Playback")]
        public bool playOnStart = true;
        public bool loop = false;

        [Min(0f)]
        public float timeScale = 1f;

        [Tooltip("If true, logs warnings for invalid bindings/property paths.")]
        public bool verboseWarnings = true;

        public bool IsPlaying => _playing;
        public float TimeSeconds => _time;

        // Internal compiled track info
        private readonly List<CompiledTrack> _compiled = new List<CompiledTrack>();
        private bool _compiledValid = false;

        private bool _playing = false;
        private float _time = 0f;

        // For SnapshotAtStart: per-clip captured from value (first time we enter clip range)
        private readonly Dictionary<int, TweenValue> _snapshotFrom = new Dictionary<int, TweenValue>();

        private void Start()
        {
            if (playOnStart) Play(resetTime: true);
        }

        private void Update()
        {
            if (!_playing || asset == null) return;

            float dt = UnityEngine.Time.deltaTime * Mathf.Max(0f, timeScale);
            Evaluate(_time + dt);
        }

        public void Play(bool resetTime)
        {
            if (asset == null) return;

            if (!_compiledValid) Compile();
            if (!_compiledValid) return;

            _playing = true;
            if (resetTime) _time = 0f;

            // When restarting, clear snapshots so SnapshotAtStart recaptures correctly.
            if (resetTime) _snapshotFrom.Clear();
        }

        public void Stop()
        {
            _playing = false;
        }

        public void Seek(float timeSeconds)
        {
            if (asset == null) return;
            if (!_compiledValid) Compile();
            if (!_compiledValid) return;

            Evaluate(timeSeconds);
        }

        public void Compile()
        {
            _compiled.Clear();
            _compiledValid = false;
            _snapshotFrom.Clear();

            if (asset == null)
            {
                if (verboseWarnings) Debug.LogWarning("[TweenBoard] No TweenAsset assigned.", this);
                return;
            }

            if (bindings == null)
            {
                if (verboseWarnings) Debug.LogWarning("[TweenBoard] No TweenBindings assigned.", this);
                return;
            }

            for (int i = 0; i < asset.tracks.Count; i++)
            {
                var track = asset.tracks[i];
                if (track == null) continue;

                if (string.IsNullOrWhiteSpace(track.bindingKey))
                {
                    Warn($"Track {i} has empty bindingKey; skipping.");
                    continue;
                }

                object targetObj = null;
                Type targetType = null;

                if (track.targetType == TrackTargetType.Transform)
                {
                    if (!bindings.TryResolveTransform(track.bindingKey, out var tr) || tr == null)
                    {
                        Warn($"Track {i} binding '{track.bindingKey}' could not resolve Transform; skipping.");
                        continue;
                    }
                    targetObj = tr;
                    targetType = typeof(Transform);
                }
                else
                {
                    var compType = ResolveType(track.componentTypeName);
                    if (compType == null || !typeof(Component).IsAssignableFrom(compType))
                    {
                        Warn($"Track {i} binding '{track.bindingKey}' has invalid componentTypeName '{track.componentTypeName}'; skipping.");
                        continue;
                    }

                    if (!bindings.TryResolveComponent(track.bindingKey, compType, out var comp) || comp == null)
                    {
                        Warn($"Track {i} binding '{track.bindingKey}' could not resolve Component '{compType.Name}'; skipping.");
                        continue;
                    }

                    targetObj = comp;
                    targetType = compType;
                }

                if (!PropertyPath.TryGetMemberChain(targetType, track.propertyPath, out var chain))
                {
                    Warn($"Track {i} '{track.bindingKey}' invalid propertyPath '{track.propertyPath}' for type '{targetType.Name}'; skipping.");
                    continue;
                }

                // Ensure leaf type matches track valueType (basic validation)
                var leafType = PropertyPath.GetLeafType(targetType, chain, track.propertyPath);
                if (!IsCompatible(track.valueType, leafType))
                {
                    Warn($"Track {i} '{track.bindingKey}.{track.propertyPath}' leaf type '{leafType?.Name}' is not compatible with valueType '{track.valueType}'; skipping.");
                    continue;
                }

                var ct = new CompiledTrack
                {
                    trackIndex = i,
                    target = targetObj,
                    targetType = targetType,
                    memberChain = chain,
                    propertyPath = track.propertyPath,
                    valueType = track.valueType,
                    clips = track.clips
                };

                _compiled.Add(ct);
            }

            _compiledValid = _compiled.Count > 0;
            if (!_compiledValid)
                Warn("No valid tracks compiled. Check bindings and property paths.");
        }

        private void Evaluate(float newTime)
        {
            _time = Mathf.Max(0f, newTime);

            float length = Mathf.Max(0f, asset.lengthSeconds);
            if (length <= 0f)
            {
                // If author forgot to recompute length, fall back to max end time.
                length = 0f;
                for (int i = 0; i < asset.tracks.Count; i++)
                    length = Mathf.Max(length, asset.tracks[i]?.GetTrackEndTime() ?? 0f);
            }

            if (!loop && length > 0f && _time >= length)
            {
                _time = length;
                ApplyAtTime(_time);
                _playing = false;
                return;
            }

            if (loop && length > 0f)
            {
                // Loop time
                _time = _time % length;
                // snapshots should reset on wrap so SnapshotAtStart re-captures
                _snapshotFrom.Clear();
            }

            ApplyAtTime(_time);
        }

        private void ApplyAtTime(float t)
        {
            // For each track, find the best (latest-starting) active clip and apply it.
            for (int i = 0; i < _compiled.Count; i++)
            {
                var ct = _compiled[i];
                if (ct.target == null) continue;

                TweenClip active = null;
                int activeClipIndex = -1;
                float bestStart = float.NegativeInfinity;

                var clips = ct.clips;
                if (clips == null) continue;

                for (int c = 0; c < clips.Count; c++)
                {
                    var clip = clips[c];
                    if (clip == null || clip.muted) continue;

                    float start = Mathf.Max(0f, clip.startTime);
                    float dur = Mathf.Max(0f, clip.duration);
                    float end = start + dur;

                    if (dur <= 0f) continue;
                    if (t < start || t > end) continue;

                    if (start >= bestStart)
                    {
                        bestStart = start;
                        active = clip;
                        activeClipIndex = c;
                    }
                }

                if (active == null) continue;

                float clipStart = Mathf.Max(0f, active.startTime);
                float clipDur = Mathf.Max(0f, active.duration);

                float localT = Mathf.Clamp01((t - clipStart) / clipDur);
                float eased = EvaluateEasing(active.easing, localT);

                // Resolve 'from' value
                TweenValue from;
                int clipKey = (ct.trackIndex * 100000) + activeClipIndex;

                if (active.fromMode == ClipFromMode.ExplicitFromValue)
                {
                    from = active.fromValue;
                }
                else
                {
                    // Snapshot at clip start: capture once per clip instance per loop.
                    if (!_snapshotFrom.TryGetValue(clipKey, out from))
                    {
                        if (!TryReadCurrent(ct, out from))
                        {
                            // If we can't read, fall back to explicit if provided (even if mode is snapshot)
                            from = active.fromValue;
                        }
                        _snapshotFrom[clipKey] = from;
                    }
                }

                TweenValue to = active.toValue;

                // Lerp
                object leafValue = Interp(ct.valueType, from, to, eased);
                TryWrite(ct, leafValue);
            }
        }

        private bool TryReadCurrent(CompiledTrack ct, out TweenValue value)
        {
            value = default;
            try
            {
                object leaf = PropertyPath.GetValue(ct.target, ct.memberChain, ct.propertyPath);
                if (leaf == null) return false;

                switch (ct.valueType)
                {
                    case TweenValueType.Float:
                        value = TweenValue.FromFloat(Convert.ToSingle(leaf));
                        return true;
                    case TweenValueType.Vector3:
                        if (leaf is Vector3 v3) { value = TweenValue.FromVector3(v3); return true; }
                        if (leaf is Vector2 v2) { value = TweenValue.FromVector3(new Vector3(v2.x, v2.y, 0f)); return true; }
                        return false;
                    case TweenValueType.Quaternion:
                        if (leaf is Quaternion q) { value = TweenValue.FromQuaternion(q); return true; }
                        return false;
                    case TweenValueType.Color:
                        if (leaf is Color c) { value = TweenValue.FromColor(c); return true; }
                        return false;
                }
            }
            catch { }
            return false;
        }

        private bool TryWrite(CompiledTrack ct, object leafValue)
        {
            try
            {
                return PropertyPath.SetValue(ct.target, ct.memberChain, ct.propertyPath, leafValue);
            }
            catch (Exception ex)
            {
                Warn($"Failed to set '{ct.propertyPath}' on '{ct.targetType.Name}': {ex.Message}");
                return false;
            }
        }

        private static float EvaluateEasing(AnimationCurve curve, float t)
        {
            if (curve == null || curve.length == 0) return t;
            return curve.Evaluate(t);
        }

        private static object Interp(TweenValueType type, TweenValue from, TweenValue to, float t)
        {
            switch (type)
            {
                case TweenValueType.Float:
                    return Mathf.LerpUnclamped(from.floatValue, to.floatValue, t);
                case TweenValueType.Vector3:
                    return Vector3.LerpUnclamped(from.vector3Value, to.vector3Value, t);
                case TweenValueType.Quaternion:
                    // slerp gives best "camera blend" feel
                    return Quaternion.Slerp(from.quaternionValue, to.quaternionValue, t);
                case TweenValueType.Color:
                    return Color.LerpUnclamped(from.colorValue, to.colorValue, t);
                default:
                    return null;
            }
        }

        private static bool IsCompatible(TweenValueType vt, Type leafType)
        {
            if (leafType == null) return false;

            switch (vt)
            {
                case TweenValueType.Float:
                    return leafType == typeof(float) || leafType == typeof(double) || leafType == typeof(int);
                case TweenValueType.Vector3:
                    return leafType == typeof(Vector3) || leafType == typeof(Vector2);
                case TweenValueType.Quaternion:
                    return leafType == typeof(Quaternion);
                case TweenValueType.Color:
                    return leafType == typeof(Color);
                default:
                    return false;
            }
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            // 1) direct
            var t = Type.GetType(typeName);
            if (t != null) return t;

            // 2) try UnityEngine assembly for short names
            t = Type.GetType($"{typeName}, UnityEngine");
            if (t != null) return t;

            // 3) search loaded assemblies (more expensive but acceptable on compile call)
            var asms = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
            for (int i = 0; i < asms.Count; i++)
            {
                var found = asms[i].GetType(typeName);
                if (found != null) return found;
            }

            return null;
        }

        private void Warn(string msg)
        {
            if (!verboseWarnings) return;
            Debug.LogWarning("[TweenBoard] " + msg, this);
        }

        private sealed class CompiledTrack
        {
            public int trackIndex;
            public object target;
            public Type targetType;
            public MemberInfo[] memberChain;
            public string propertyPath;
            public TweenValueType valueType;
            public List<TweenClip> clips;
        }
    }
}
