using System;
using System.Collections.Generic;
using UnityEngine;

namespace Animator.Runtime
{
    public sealed class TweenBindings : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public string key;
            public Transform transform;
            public List<Component> components = new List<Component>();
        }

        public List<Binding> bindings = new List<Binding>();

        public bool TryResolveTransform(string key, out Transform t)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i] != null && bindings[i].key == key)
                {
                    t = bindings[i].transform;
                    return t != null;
                }
            }
            t = null;
            return false;
        }

        public bool TryResolveComponent(string key, Type componentType, out Component c)
        {
            c = null;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null || b.key != key) continue;

                // try explicit list first
                if (b.components != null)
                {
                    for (int j = 0; j < b.components.Count; j++)
                    {
                        var comp = b.components[j];
                        if (comp == null) continue;
                        if (componentType.IsAssignableFrom(comp.GetType()))
                        {
                            c = comp;
                            return true;
                        }
                    }
                }

                // fallback: try to find on transform if present
                if (b.transform != null)
                {
                    var found = b.transform.GetComponent(componentType);
                    if (found != null)
                    {
                        c = found;
                        return true;
                    }
                }

                return false;
            }
            return false;
        }
    }
}
