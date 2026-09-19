using System;
using UnityEngine;

namespace Animator.Runtime
{
    [Serializable]
    public struct TweenValue
    {
        public TweenValueType type;

        public float floatValue;
        public Vector3 vector3Value;
        public Quaternion quaternionValue;
        public Color colorValue;

        public static TweenValue FromFloat(float v) => new TweenValue { type = TweenValueType.Float, floatValue = v };
        public static TweenValue FromVector3(Vector3 v) => new TweenValue { type = TweenValueType.Vector3, vector3Value = v };
        public static TweenValue FromQuaternion(Quaternion v) => new TweenValue { type = TweenValueType.Quaternion, quaternionValue = v };
        public static TweenValue FromColor(Color v) => new TweenValue { type = TweenValueType.Color, colorValue = v };

        public override string ToString()
        {
            return type switch
            {
                TweenValueType.Float => floatValue.ToString(),
                TweenValueType.Vector3 => vector3Value.ToString(),
                TweenValueType.Quaternion => quaternionValue.eulerAngles.ToString(),
                TweenValueType.Color => colorValue.ToString(),
                _ => "<Unknown>",
            };
        }
    }

    public enum TweenValueType
    {
        Float = 0,
        Vector3 = 1,
        Quaternion = 2,
        Color = 3,
    }
}
