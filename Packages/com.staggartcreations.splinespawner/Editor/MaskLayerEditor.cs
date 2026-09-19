// Spline Spawner by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//  • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//  • Uploading this file to a public GitHub repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using sc.splines.spawner.runtime;

namespace sc.splines.spawner.editor
{
    public class MaskLayerEditor : Editor
    {
        public static int LayerCount => MaskLayerSettings.instance.LayerNames.Length;
            
        public static string IndexToName(int index)
        {
            return MaskLayerSettings.instance.LayerNames[index];
        }

        public static string LayersToReadableList(int layerMask)
        {
            if (layerMask == 0) return string.Empty;

            List<string> names = new List<string>();
            for (int i = 0; i < LayerCount; i++)
            {
                if (SplineSpawnerMask.ContainsLayer(layerMask, i))
                {
                    names.Add(IndexToName(i));
                }
            }

            if (names.Count == LayerCount) return "(Everything)";

            return names.Count > 0 ? $"({string.Join(", ", names)})" : string.Empty;
        }

        [CustomPropertyDrawer(typeof(SplineSpawnerMask.MaskLayerAttribute))]
        public class MaskLayerDrawer : PropertyDrawer
        {
            private const float width = 200f;
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    var maskAttribute = (SplineSpawnerMask.MaskLayerAttribute)attribute;
                    
                    float labelWidth = EditorGUIUtility.labelWidth;
                    
                    var fieldRect = new Rect(position.x, position.y,labelWidth + width, position.height);

                    if (maskAttribute.multiSelect)
                    {
                        property.intValue = EditorGUI.MaskField(fieldRect, label, property.intValue, MaskLayerSettings.instance.LayerNames);
                    }
                    else
                    {
                        property.intValue = EditorGUI.Popup(fieldRect, label.text, property.intValue, MaskLayerSettings.instance.LayerNames);
                    }

                    if (maskAttribute.showEditButton)
                    {
                        var buttonRect = new Rect(position.x + labelWidth + width, position.y, 45f, position.height);
                        if (GUI.Button(buttonRect, "Edit"))
                        {
                            MaskLayerSettingsEditor.OpenSettings();
                        }
                    }
                }
                else
                {
                    EditorGUI.LabelField(position, label.text, "The [MaskField] needs to be used with an integer.");
                }
            }
        }
        
        internal class MaskLayerSettingsEditor : SettingsProvider
        {
            private const string SettingsPath = "Project/Tags and Layers/Spline Spawner";
            
            public static void OpenSettings()
            {
                SettingsService.OpenProjectSettings(SettingsPath);
            }
            
            private MaskLayerSettingsEditor(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
                : base(path, scopes, keywords) { }
            
            public override void OnGUI(string searchContext)
            {
                var preferences = MaskLayerSettings.instance;
                int layerCount = MaskLayerSettings.instance.LayerNames.Length;

                EditorGUI.BeginChangeCheck();

                using(CreateSettingsWindowGUIScope())
                {
                    EditorGUILayout.LabelField("Masking layers", EditorStyles.boldLabel);
                    for (int i = 0; i < layerCount; i++)
                    {
                        preferences.LayerNames[i] =
                            EditorGUILayout.TextField($"Layer {i + 1}", preferences.LayerNames[i], GUILayout.MaxWidth(EditorGUIUtility.labelWidth + 300f));
                    }

                    if (GUILayout.Button("Reset to default", GUILayout.MaxWidth(150f)))
                    {
                        if (EditorUtility.DisplayDialog("Reset to default?",
                                "Are you sure you want to reset the layer names to default?", "Yes", "No"))
                        {
                            preferences.Reset();
                        }
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    for (int i = 0; i < layerCount; i++)
                    {
                        string value =  MaskLayerSettings.instance.LayerNames[i];

                        if (value == string.Empty) value = $"Layer {i+1}";

                        MaskLayerSettings.instance.LayerNames[i] = value;
                    }
                        
                    MaskLayerSettings.instance.Save();
                }
            }
            
            [SettingsProvider]
            public static SettingsProvider CreateAssetStoreToolsSettingProvider()
            {
                return new MaskLayerSettingsEditor(SettingsPath, SettingsScope.Project);
            }
            
            private IDisposable CreateSettingsWindowGUIScope()
            {
                var unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));
                var type = unityEditorAssembly.GetType("UnityEditor.SettingsWindow+GUIScope");
                return Activator.CreateInstance(type) as IDisposable;
            }
        }
    }
}
