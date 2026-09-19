using UnityEditor;
using UnityEngine;

namespace sc.splines.spawner.editor
{
    [FilePath("ProjectSettings/SplineSpawnerMaskLayers.asset", FilePathAttribute.Location.ProjectFolder)]
    public class MaskLayerSettings : ScriptableSingleton<MaskLayerSettings>
    {
        [SerializeField] private string[] layerNames;
        public string[] LayerNames => layerNames;

        private readonly string[] defaultLayerNames = new []
        {
            "Paths",
            "Roads",
            "Clearings",
            "Buildings",
            "Vegetation",
            "Water",
            "Terrain",
            "Obstacles",
            "Props",
            "Enemies",
            "Interactables",
            "Decor"
        };
        
        public void Reset()
        {
            layerNames = new string[32];

            for (int i = 0; i < defaultLayerNames.Length; i++)
            {
                layerNames[i] = defaultLayerNames[i];
            }

            for (int i = defaultLayerNames.Length; i < layerNames.Length; i++)
            {
                layerNames[i] = $"Layer {i+1}";
            }
        }
            
        internal void Save() { Save(true); }
        private void OnDisable() { Save(); }
    }

}