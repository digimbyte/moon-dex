using UnityEngine;

namespace Animator.Runtime
{
    public static class TweenHelpers
    {
        // These are optional convenience creators for code-driven authoring (NOT required for the SO board).
        public static TweenClip Clip(float startTime, float duration, TweenValue to, ClipFromMode fromMode = ClipFromMode.SnapshotAtStart, TweenValue from = default, AnimationCurve easing = null)
        {
            return new TweenClip
            {
                startTime = Mathf.Max(0f, startTime),
                duration = Mathf.Max(0f, duration),
                fromMode = fromMode,
                fromValue = from,
                toValue = to,
                easing = easing
            };
        }

        public static TweenTrack TrackTransform(string bindingKey, string propertyPath, TweenValueType valueType)
        {
            return new TweenTrack
            {
                bindingKey = bindingKey,
                targetType = TrackTargetType.Transform,
                componentTypeName = "UnityEngine.Transform",
                propertyPath = propertyPath,
                valueType = valueType
            };
        }

        public static TweenTrack TrackComponent(string bindingKey, string componentTypeName, string propertyPath, TweenValueType valueType)
        {
            return new TweenTrack
            {
                bindingKey = bindingKey,
                targetType = TrackTargetType.Component,
                componentTypeName = componentTypeName,
                propertyPath = propertyPath,
                valueType = valueType
            };
        }
    }
}
