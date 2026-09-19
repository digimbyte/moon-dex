#if GASKELLGAMES
using UnityEngine;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [AddComponentMenu("Gaskellgames/Input Event System/GMK Visualiser")]
    public class GMKVisualiser : GgMonoBehaviour
    {
        #region Variables
        
        [SerializeField, Required]
        [Tooltip("Default: 1, 2, 3, 4 / D-Pad")]
        private GMKInputController GMKController;

        [SerializeField, Required]
        [Tooltip("Reference to a RectTransform used for logic.")]
        private RectTransform l2Start;

        [SerializeField, Required]
        [Tooltip("Reference to a RectTransform used for logic.")]
        private RectTransform l2End;

        [SerializeField, Required]
        [Tooltip("Reference to a RectTransform used for logic.")]
        private RectTransform r2Start;

        [SerializeField, Required]
        [Tooltip("Reference to a RectTransform used for logic.")]
        private RectTransform r2End;

        [SerializeField, Required]
        [Tooltip("Reference to a RectTransform used for logic.")]
        private RectTransform l3Center;

        [SerializeField, Required]
        [Tooltip("Reference to a RectTransform used for logic.")]
        private RectTransform r3Center;
        
        [SerializeField, Range(0, 1)]
        [Tooltip("The dead-zone value used to offset any drift")]
        private float deadzone = 0.01f;
        
        [SerializeField, Min(0)]
        [Tooltip("The movement distance in UI space of the sticks.")]
        private float stickTravel;
        
        [SerializeField]
        [Tooltip("Default: 1, 2, 3, 4 / D-Pad")]
        private VisualiserIcons dUp;
        
        [SerializeField]
        [Tooltip("Default: 1, 2, 3, 4 / D-Pad")]
        private VisualiserIcons dLeft;
        
        [SerializeField]
        [Tooltip("Default: 1, 2, 3, 4 / D-Pad")]
        private VisualiserIcons dRight;
        
        [SerializeField]
        [Tooltip("Default: 1, 2, 3, 4 / D-Pad")]
        private VisualiserIcons dDown;

        [SerializeField]
        [Tooltip("Default: Space / Button South")]
        private VisualiserIcons south;
        
        [SerializeField]
        [Tooltip("Default: L Ctrl / Button East")]
        private VisualiserIcons east;
        
        [SerializeField]
        [Tooltip("Default: L Alt / Button West")]
        private VisualiserIcons west;
        
        [SerializeField]
        [Tooltip("Default: C / Button North")]
        private VisualiserIcons north;
        
        [SerializeField]
        [Tooltip("Default: L Shift / Left Shoulder")]
        private VisualiserIcons l1;

        [SerializeField]
        [Tooltip("Default: F / Right Shoulder")]
        private VisualiserIcons r1;
        
        [SerializeField]
        [Tooltip("Default: R Mouse / L Trigger")]
        private VisualiserIcons l2;
        
        [SerializeField]
        [Tooltip("Default: L Mouse / R Trigger")]
        private VisualiserIcons r2;
        
        [SerializeField]
        [Tooltip("Default: Q / Left Stick")]
        private VisualiserIcons l3;
        
        [SerializeField]
        [Tooltip("Default: E / Right Stick")]
        private VisualiserIcons r3;
        
        [SerializeField]
        [Tooltip("Default: Esc / Start")]
        private VisualiserIcons start;
        
        [SerializeField]
        [Tooltip("Default: P / Select")]
        private VisualiserIcons select;
        
        [SerializeField]
        [Tooltip("Default: Middle Mouse / Touchpad")]
        private VisualiserIcons touchpad;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Game Loop

        private void LateUpdate()
        {
            if (!GMKController) { return; }
            GMKInputs inputs = GMKController.Inputs;

            HandleActiveStates(inputs);
            HandleMovement(inputs);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Methods
        
        private void HandleActiveStates(GMKInputs inputs)
        {
            dUp.SetActive(inputs.dPad.y > deadzone);
            dLeft.SetActive(inputs.dPad.x > deadzone);
            dRight.SetActive(inputs.dPad.y < -deadzone);
            dDown.SetActive(inputs.dPad.x < -deadzone);
            south.SetActive(inputs.south.keypressed);
            east.SetActive(inputs.east.keypressed);
            west.SetActive(inputs.west.keypressed);
            north.SetActive(inputs.north.keypressed);
            l1.SetActive(inputs.leftShoulder.keypressed);
            r1.SetActive(inputs.rightShoulder.keypressed);
            select.SetActive(inputs.select.keypressed);
            start.SetActive(inputs.start.keypressed);
            l3.SetActive(inputs.leftStickPress.keypressed || Mathf.Abs(inputs.leftStick.x) > deadzone || Mathf.Abs(inputs.leftStick.y) > deadzone);
            r3.SetActive(inputs.rightStickPress.keypressed || Mathf.Abs(inputs.rightStick.x) > deadzone || Mathf.Abs(inputs.rightStick.y) > deadzone);
            touchpad.SetActive(inputs.touchpadPress.keypressed);
            l2.SetActive(inputs.leftTrigger > deadzone);
            r2.SetActive(inputs.rightTrigger > deadzone);
        }
        
        private void HandleMovement(GMKInputs inputs)
        {
            l2.image.rectTransform.position = Vector3.Lerp(l2Start.position, l2End.position, inputs.leftTrigger);
            r2.image.rectTransform.position = Vector3.Lerp(r2Start.position, r2End.position, inputs.rightTrigger);
            l3.image.rectTransform.position = l3Center.position + new Vector3(inputs.LeftStickClamped.x * stickTravel, inputs.LeftStickClamped.y * stickTravel, 0);
            r3.image.rectTransform.position = r3Center.position + new Vector3(inputs.RightStickClamped.x * stickTravel, inputs.RightStickClamped.y * stickTravel, 0);
        }
        
        #endregion
        
    } // class end
}
#endif