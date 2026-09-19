using System;
using System.Reflection;
using UnityEngine;

namespace Animator.Runtime
{
    internal static class PropertyPath
    {
        // Supports: Field/Property chains with optional ".x/.y/.z/.w" for Vector2/3/4, Quaternion, Color
        // Example: "someStruct.position.x"
        public static bool TryGetMemberChain(Type rootType, string path, out MemberInfo[] chain)
        {
            chain = null;
            if (rootType == null || string.IsNullOrWhiteSpace(path)) return false;

            var parts = path.Split('.');
            var members = new MemberInfo[parts.Length];

            Type current = rootType;
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i];

                // Special swizzle members handled later in evaluate (x/y/z/w/r/g/b/a)
                // Here we still treat them as "members" by storing null marker and keeping type tracking.
                if (IsSwizzle(name))
                {
                    members[i] = null;
                    current = SwizzleType(current, name);
                    if (current == null) return false;
                    continue;
                }

                var m = (MemberInfo)current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null)
                    m = (MemberInfo)current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (m == null) return false;

                members[i] = m;
                current = GetMemberType(m);
                if (current == null) return false;
            }

            chain = members;
            return true;
        }

        public static Type GetLeafType(Type rootType, MemberInfo[] chain, string path)
        {
            if (rootType == null || chain == null) return null;
            var parts = path.Split('.');
            Type current = rootType;
            for (int i = 0; i < parts.Length; i++)
            {
                var m = chain[i];
                if (m == null)
                {
                    current = SwizzleType(current, parts[i]);
                    continue;
                }
                current = GetMemberType(m);
            }
            return current;
        }

        public static object GetValue(object root, MemberInfo[] chain, string path)
        {
            var parts = path.Split('.');
            object currentObj = root;
            Type currentType = root.GetType();

            for (int i = 0; i < parts.Length; i++)
            {
                var m = chain[i];
                string part = parts[i];

                if (m == null)
                {
                    currentObj = GetSwizzle(currentObj, currentType, part);
                    currentType = currentObj?.GetType();
                    continue;
                }

                currentObj = GetMemberValue(currentObj, m);
                currentType = currentObj?.GetType();
                if (currentObj == null) break;
            }

            return currentObj;
        }

        public static bool SetValue(object root, MemberInfo[] chain, string path, object leafValue)
        {
            var parts = path.Split('.');
            if (parts.Length == 0) return false;

            // Walk down collecting containers so we can write-back through value-type structs.
            object[] containers = new object[parts.Length];
            Type[] types = new Type[parts.Length];
            MemberInfo[] usedMembers = new MemberInfo[parts.Length];

            object currentObj = root;
            Type currentType = root.GetType();

            for (int i = 0; i < parts.Length; i++)
            {
                containers[i] = currentObj;
                types[i] = currentType;
                usedMembers[i] = chain[i];

                if (chain[i] == null)
                {
                    // Swizzle: read the intermediate scalar (value-type writing back handled later)
                    currentObj = GetSwizzle(currentObj, currentType, parts[i]);
                    currentType = currentObj?.GetType();
                    continue;
                }

                currentObj = GetMemberValue(currentObj, chain[i]);
                currentType = currentObj?.GetType();
            }

            // Set leaf, then write-back up the chain.
            object valueToWrite = leafValue;

            for (int i = parts.Length - 1; i >= 0; i--)
            {
                var container = containers[i];
                var containerType = types[i];
                var member = usedMembers[i];
                var part = parts[i];

                if (member == null)
                {
                    // Swizzle set into previous container object which must be a struct (Vector/Quat/Color)
                    valueToWrite = SetSwizzle(container, containerType, part, valueToWrite);
                    continue;
                }

                // Set member on container
                if (!SetMemberValue(container, member, valueToWrite))
                    return false;

                // Next write-back should be the container itself (in case it was a struct in a parent)
                valueToWrite = container;

                // For reference types, once we've set the member, upstream doesn't need rewrite,
                // but for simplicity we keep walking to support nested structs.
            }

            return true;
        }

        static bool IsSwizzle(string s)
        {
            return s == "x" || s == "y" || s == "z" || s == "w" || s == "r" || s == "g" || s == "b" || s == "a";
        }

        static Type SwizzleType(Type baseType, string swizzle)
        {
            if (baseType == typeof(Vector2) || baseType == typeof(Vector3) || baseType == typeof(Vector4)) return typeof(float);
            if (baseType == typeof(Quaternion)) return typeof(float);
            if (baseType == typeof(Color)) return typeof(float);
            return null;
        }

        static object GetSwizzle(object obj, Type type, string swizzle)
        {
            if (obj == null) return null;

            if (type == typeof(Vector2))
            {
                var v = (Vector2)obj;
                return swizzle == "x" ? v.x : v.y;
            }
            if (type == typeof(Vector3))
            {
                var v = (Vector3)obj;
                return swizzle switch { "x" => v.x, "y" => v.y, "z" => v.z, _ => 0f };
            }
            if (type == typeof(Vector4))
            {
                var v = (Vector4)obj;
                return swizzle switch { "x" => v.x, "y" => v.y, "z" => v.z, "w" => v.w, _ => 0f };
            }
            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)obj;
                return swizzle switch { "x" => q.x, "y" => q.y, "z" => q.z, "w" => q.w, _ => 0f };
            }
            if (type == typeof(Color))
            {
                var c = (Color)obj;
                return swizzle switch { "r" => c.r, "g" => c.g, "b" => c.b, "a" => c.a, _ => 0f };
            }

            return null;
        }

        static object SetSwizzle(object container, Type type, string swizzle, object scalar)
        {
            float f = Convert.ToSingle(scalar);

            if (type == typeof(Vector2))
            {
                var v = (Vector2)container;
                if (swizzle == "x") v.x = f; else v.y = f;
                return v;
            }
            if (type == typeof(Vector3))
            {
                var v = (Vector3)container;
                if (swizzle == "x") v.x = f;
                else if (swizzle == "y") v.y = f;
                else if (swizzle == "z") v.z = f;
                return v;
            }
            if (type == typeof(Vector4))
            {
                var v = (Vector4)container;
                if (swizzle == "x") v.x = f;
                else if (swizzle == "y") v.y = f;
                else if (swizzle == "z") v.z = f;
                else if (swizzle == "w") v.w = f;
                return v;
            }
            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)container;
                if (swizzle == "x") q.x = f;
                else if (swizzle == "y") q.y = f;
                else if (swizzle == "z") q.z = f;
                else if (swizzle == "w") q.w = f;
                return q;
            }
            if (type == typeof(Color))
            {
                var c = (Color)container;
                if (swizzle == "r") c.r = f;
                else if (swizzle == "g") c.g = f;
                else if (swizzle == "b") c.b = f;
                else if (swizzle == "a") c.a = f;
                return c;
            }

            return container;
        }

        static Type GetMemberType(MemberInfo m)
        {
            if (m is PropertyInfo pi) return pi.PropertyType;
            if (m is FieldInfo fi) return fi.FieldType;
            return null;
        }

        static object GetMemberValue(object obj, MemberInfo m)
        {
            if (obj == null) return null;
            if (m is PropertyInfo pi) return pi.GetValue(obj);
            if (m is FieldInfo fi) return fi.GetValue(obj);
            return null;
        }

        static bool SetMemberValue(object obj, MemberInfo m, object value)
        {
            try
            {
                if (m is PropertyInfo pi)
                {
                    if (!pi.CanWrite) return false;
                    pi.SetValue(obj, value);
                    return true;
                }
                if (m is FieldInfo fi)
                {
                    fi.SetValue(obj, value);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }
    }
}
