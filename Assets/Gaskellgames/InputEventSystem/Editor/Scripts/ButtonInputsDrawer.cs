#if UNITY_EDITOR
#if GASKELLGAMES
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.InputEventSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(ButtonInputs), true)]
    public class ButtonInputsDrawer : PropertyDrawer
    {
        #region variables

        private SerializedProperty keypressed;
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Property Height

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorExtensions.singleLine + EditorExtensions.standardSpacing;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnGUI

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // get reference to SerializeFields
            keypressed = property.FindPropertyRelative("keypressed");
            
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);

            // cache positional values
            float labelWidth = EditorExtensions.labelWidthForIndentLevel;
            float propertyWidth = position.width - (labelWidth + EditorExtensions.standardSpacing);
            float propertyPosition = position.x + labelWidth;
            Rect labelRect = new Rect(position.x, position.y, labelWidth, EditorExtensions.singleLine);
            Rect propertyRect = new Rect(propertyPosition, position.y, propertyWidth, EditorExtensions.singleLine);
            
            // draw property
            EditorGUI.PrefixLabel(labelRect, label);
            EditorGUIUtility.labelWidth = propertyWidth - EditorExtensions.singleLine;
            keypressed.boolValue = EditorGUI.Toggle(propertyRect, new GUIContent(), keypressed.boolValue);
            EditorGUIUtility.labelWidth = labelWidth;

            // close property
            EditorGUI.EndProperty();
        }

        #endregion

    } // class end 
}
#endif
#endif