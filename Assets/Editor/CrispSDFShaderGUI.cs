using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class CrispSDFShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();
        base.OnGUI(editor, properties);
        if (!EditorGUI.EndChangeCheck()) return;
        foreach (Material material in editor.targets)
        {
            ValidateMaterial(material);
            TMPro_EventManager.ON_MATERIAL_PROPERTY_CHANGED(true, material);
        }
    }

    public override void ValidateMaterial(Material material)
    {
        // Match TMP's padding calculation without allowing it to couple the
        // letter weight to the border width. These are not visual effect toggles.
        material.EnableKeyword("RATIOS_OFF");
        material.EnableKeyword("UNDERLAY_ON");
        material.SetFloat("_ScaleRatioA", 1);
        material.SetFloat("_ScaleRatioB", 1);
        material.SetFloat("_ScaleRatioC", 1);
        material.SetFloat("_OutlineSoftness", 0);
        material.SetFloat("_UnderlayDilate", 0);
    }
}
