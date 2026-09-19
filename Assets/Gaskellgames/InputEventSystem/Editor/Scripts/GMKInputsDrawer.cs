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

    [CustomPropertyDrawer(typeof(GMKInputs), true)]
    public class GMKInputsDrawer : PropertyDrawer
    {
        #region variables

        private SerializedProperty south;
        private SerializedProperty east;
        private SerializedProperty west;
        private SerializedProperty north;
        private SerializedProperty leftShoulder;
        private SerializedProperty rightShoulder;
        private SerializedProperty select;
        private SerializedProperty start;
        private SerializedProperty leftStickPress;
        private SerializedProperty rightStickPress;
        private SerializedProperty touchpadPress;
        private SerializedProperty leftStick;
        private SerializedProperty rightStick;
        private SerializedProperty dPad;
        private SerializedProperty leftTrigger;
        private SerializedProperty rightTrigger;
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Property Height

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorExtensions.singleLine + EditorExtensions.standardSpacing) * 8;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnGUI

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // get reference to SerializeFields
            south = property.FindPropertyRelative("south");
            east = property.FindPropertyRelative("east");
            west = property.FindPropertyRelative("west");
            north = property.FindPropertyRelative("north");
            leftShoulder = property.FindPropertyRelative("leftShoulder");
            rightShoulder = property.FindPropertyRelative("rightShoulder");
            select = property.FindPropertyRelative("select");
            start = property.FindPropertyRelative("start");
            leftStickPress = property.FindPropertyRelative("leftStickPress");
            rightStickPress = property.FindPropertyRelative("rightStickPress");
            touchpadPress = property.FindPropertyRelative("touchpadPress");
            leftStick = property.FindPropertyRelative("leftStick");
            rightStick = property.FindPropertyRelative("rightStick");
            dPad = property.FindPropertyRelative("dPad");
            leftTrigger = property.FindPropertyRelative("leftTrigger");
            rightTrigger = property.FindPropertyRelative("rightTrigger");
            
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);

            // cache positional values
            float propertyWidth = (position.width - EditorExtensions.standardSpacing) * 0.5f;
            float propertyPositionL = position.x;
            float propertyPositionR = position.x + propertyWidth;
            Rect propertyRectL = new Rect(propertyPositionL, position.y, propertyWidth, EditorExtensions.singleLine);
            Rect propertyRectR = new Rect(propertyPositionR, position.y, propertyWidth, EditorExtensions.singleLine);
            
            // draw property
            EditorGUI.PropertyField(propertyRectL, south);
            EditorGUI.PropertyField(propertyRectR, east);
            propertyRectL.y += EditorExtensions.singleLine;
            propertyRectR.y += EditorExtensions.singleLine;
            EditorGUI.PropertyField(propertyRectL, west);
            EditorGUI.PropertyField(propertyRectR, north);
            propertyRectL.y += EditorExtensions.singleLine;
            propertyRectR.y += EditorExtensions.singleLine;
            EditorGUI.PropertyField(propertyRectL, leftShoulder);
            EditorGUI.PropertyField(propertyRectR, rightShoulder);
            propertyRectL.y += EditorExtensions.singleLine;
            propertyRectR.y += EditorExtensions.singleLine;
            EditorGUI.PropertyField(propertyRectL, select);
            EditorGUI.PropertyField(propertyRectR, start);
            propertyRectL.y += EditorExtensions.singleLine;
            propertyRectR.y += EditorExtensions.singleLine;
            EditorGUI.PropertyField(propertyRectL, leftStickPress);
            EditorGUI.PropertyField(propertyRectR, rightStickPress);
            propertyRectL.y += EditorExtensions.singleLine;
            propertyRectR.y += EditorExtensions.singleLine;
            EditorGUI.PropertyField(propertyRectL, touchpadPress);
            EditorGUILayout.Space(-(EditorExtensions.singleLine * 3) -(EditorExtensions.standardSpacing * 2));
            EditorGUILayout.PropertyField(leftTrigger);
            EditorGUILayout.PropertyField(rightTrigger);
            EditorGUILayout.PropertyField(leftStick);
            EditorGUILayout.PropertyField(rightStick);
            EditorGUILayout.PropertyField(dPad);

            // close property
            EditorGUI.EndProperty();
        }

        #endregion

    } // class end 
}
#endif
#endif