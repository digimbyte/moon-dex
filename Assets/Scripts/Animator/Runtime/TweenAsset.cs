using System;
using System.Collections.Generic;
using UnityEngine;

namespace Animator.Runtime
{
    [CreateAssetMenu(menuName = "Tween Board/Tween Asset", fileName = "TweenAsset")]
    public sealed class TweenAsset : ScriptableObject
    {
        public List<TweenTrack> tracks = new List<TweenTrack>();

        [Min(0f)]
        public float lengthSeconds = 0f;

        public void RecomputeLength()
        {
            float max = 0f;
            for (int i = 0; i < tracks.Count; i++)
            {
                var t = tracks[i];
                if (t == null) continue;
                max = Mathf.Max(max, t.GetTrackEndTime());
            }
            lengthSeconds = max;
        }
    }

    [Serializable]
    public sealed class TweenTrack
    {
        [Tooltip("Lookup key used to resolve a scene object from TweenBindings.")]
        public string bindingKey = "";

        public TrackTargetType targetType = TrackTargetType.Transform;

        [Tooltip("If targetType = Component, this is the assembly-qualified name OR a short name Unity can resolve.\nExample: UnityEngine.Camera or MyNamespace.MyComponent")]
        public string componentTypeName = "UnityEngine.Transform";

        [Tooltip("Property or field path to animate.\nExamples: position, localPosition, rotation, localRotation, fieldOfView, alpha, someStruct.x")]
        public string propertyPath = "position";

        public TweenValueType valueType = TweenValueType.Vector3;

        public List<TweenClip> clips = new List<TweenClip>();

        public float GetTrackEndTime()
        {
            float max = 0f;
            for (int i = 0; i < clips.Count; i++)
            {
                var c = clips[i];
                max = Mathf.Max(max, c.startTime + Mathf.Max(0f, c.duration));
            }
            return max;
        }
    }

    public enum TrackTargetType
    {
        Transform = 0,
        Component = 1,
    }

    public enum ClipFromMode
    {
        SnapshotAtStart = 0,
        ExplicitFromValue = 1,
    }

    [Serializable]
    public sealed class TweenClip
    {
        [Min(0f)] public float startTime = 0f;
        [Min(0f)] public float duration = 0.25f;

        public ClipFromMode fromMode = ClipFromMode.SnapshotAtStart;

        [Tooltip("Used only when FromMode = ExplicitFromValue")]
        public TweenValue fromValue;

        public TweenValue toValue;

        [Tooltip("Optional. If null, t is linear.")]
        public AnimationCurve easing;

        [Tooltip("If true, this clip will be ignored (handy for toggling).")]
        public bool muted = false;
    }
}
