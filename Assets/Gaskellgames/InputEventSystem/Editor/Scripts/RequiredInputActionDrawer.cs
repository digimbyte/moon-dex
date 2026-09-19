#if UNITY_EDITOR
using System;
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Gaskellgames.InputEventSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(RequiredInputActionAttribute), true)]
    public class RequiredInputActionDrawer : GgPropertyDrawer
    {
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return propertyHeight;
            }
            
            if (!property.isExpanded)
            {
                return singleLineHeight + (standardSpacing * 2);
            }
            
            InputActionReference inputActionReference = property.objectReferenceValue as InputActionReference;
            return (inputActionReference == null
                ? (singleLineHeight + standardSpacing) * 3
                : ((singleLineHeight + standardSpacing) * 3) + standardSpacing) + standardSpacing;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI
        
        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                GgGUI.CustomPropertyField(position, property, label);
                return;
            }
            
            // draw header
            Rect dropdownRect = new Rect(position.x, position.y, position.width, position.height - GgGUI.standardSpacing);
            GgGUI.GetFoldoutPositionRects(dropdownRect, label, out GgFoldoutPositions foldoutPositions);
            GgGUI.DrawDropdownHeader(foldoutPositions, property, label, false, true);
            
            // draw object field
            RequiredInputActionAttribute attributeAsType = AttributeAsType<RequiredInputActionAttribute>();
            Color32 attributeColor = new Color32(attributeAsType.R, attributeAsType.G, attributeAsType.B, attributeAsType.A);
            GUI.backgroundColor = property.objectReferenceValue != null || property.hasMultipleDifferentValues ? defaultCache.guiBackgroundColor : attributeColor;
            bool changed = GgGUI.ObjectField(foldoutPositions.field, GUIContent.none, property.objectReferenceValue, out Object outputValue, typeof(InputActionReference), property.hasMultipleDifferentValues, false);
            InputActionReference inputActionReference = outputValue as InputActionReference;
            if (changed) { property.objectReferenceValue = !inputActionReference ? null : outputValue; }
            GUI.backgroundColor = defaultCache.guiBackgroundColor;
            
            // if not expanded: hide extra info
            if (!property.isExpanded) { return; }
            
            // show extra info ...
            if (property.objectReferenceValue)
            {
                // ... content
                GUI.enabled = false;
                EditorGUI.indentLevel++;
                
                Rect thisContent = foldoutPositions.content;
                thisContent.height = singleLineHeight;
                
                GUI.backgroundColor = IsCorrectInputActionType(attributeAsType, inputActionReference) ? defaultCache.guiBackgroundColor : attributeColor;
                GgGUI.EnumField(thisContent, new GUIContent("Type", "InputActionType"), inputActionReference.ToInputAction().type.ToInt(), out _, Enum.GetNames(typeof(InputActionType)), property.hasMultipleDifferentValues);
                
                thisContent.y += singleLineHeight + standardSpacing;
                GUI.backgroundColor = IsCorrectInputActionValueType(attributeAsType, inputActionReference) ? defaultCache.guiBackgroundColor : attributeColor;
                GgGUI.EnumField(thisContent, new GUIContent("Value Type", "InputActionValueType"), inputActionReference.ToInputAction().ValueType().ToInt(), out _, Enum.GetNames(typeof(InputActionValueType)), property.hasMultipleDifferentValues);
                
                EditorGUI.indentLevel--;
                GUI.backgroundColor = defaultCache.guiBackgroundColor;
                GUI.enabled = defaultCache.guiEnabled;
            }
            else
            {
                // ... warning
                Rect thisContent = foldoutPositions.content;
                thisContent.height = singleLineHeight * 2;
                EditorGUI.HelpBox(thisContent, "Warning: Reference object asset is null.", MessageType.Warning);
            }
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Methods
        
        private bool IsCorrectInputActionType(RequiredInputActionAttribute attributeAsType, InputActionReference inputActionReference)
        {
            if (!attributeAsType.requiredType) { return true; }
            return attributeAsType.inputActionType == inputActionReference.ToInputAction().type;
        }
        
        private bool IsCorrectInputActionValueType(RequiredInputActionAttribute attributeAsType, InputActionReference inputActionReference)
        {
            if (!attributeAsType.requiredControlType) { return true; }
            return attributeAsType.inputActionValueType == inputActionReference.ToInputAction().ValueType();
        }
        
        #endregion
        
    } // class end
}

#endif