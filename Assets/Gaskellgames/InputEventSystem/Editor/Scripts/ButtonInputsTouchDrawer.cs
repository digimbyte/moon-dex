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

    [CustomPropertyDrawer(typeof(ButtonInputsTouch), true)]
    public class ButtonInputsTouchDrawer : PropertyDrawer
    {
        #region variables

        private SerializedProperty keypressed;
        private SerializedProperty keytouched;
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Property Height

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorExtensions.singleLine + EditorExtensions.standardSpacing);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnGUI

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // get reference to SerializeFields
            keypressed = property.FindPropertyRelative("keypressed");
            keytouched = property.FindPropertyRelative("keytouched");
            
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);

            // cache positional values
            float labelWidth = EditorGUIUtility.labelWidth - (EditorGUI.indentLevel * EditorExtensions.singleLine);
            float propertyWidth = (position.width - (labelWidth + EditorExtensions.standardSpacing)) * 0.5f;
            float propertyPositionL = position.x + labelWidth;
            float propertyPositionR = propertyPositionL + propertyWidth + EditorExtensions.standardSpacing;
            
            Rect labelRect = new Rect(position.x, position.y, labelWidth, EditorExtensions.singleLine);
            Rect propertyRectL = new Rect(propertyPositionL, position.y, propertyWidth, EditorExtensions.singleLine);
            Rect propertyRectR = new Rect(propertyPositionR, position.y, propertyWidth, EditorExtensions.singleLine);
            
            // draw property
            EditorGUI.PrefixLabel(labelRect, label);
            EditorGUIUtility.labelWidth = propertyWidth - EditorExtensions.singleLine;
            keypressed.boolValue = EditorGUI.ToggleLeft(propertyRectL, "keypressed", keypressed.boolValue);
            keytouched.boolValue = EditorGUI.ToggleLeft(propertyRectR, "keytouched", keytouched.boolValue);
            EditorGUIUtility.labelWidth = labelWidth;

            // close property
            EditorGUI.EndProperty();
        }

        #endregion

    } // class end 
}
#endif
#endif