#if GASKELLGAMES
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AddComponentMenu("Gaskellgames/Input Event System/GMK Input Controller")]
    public class GMKInputController : GgMonoBehaviour
    {
        #region Variables
        
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Space / Button South")]
        private InputActionReference south; // button0

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: L Ctrl / Button East")]
        private InputActionReference east; // button1

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: L Alt / Button West")]
        private InputActionReference west; // button2

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: C / Button North")]
        private InputActionReference north; // button3

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: L Shift / Left Shoulder")]
        private InputActionReference leftShoulder; // button4

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: F / Right Shoulder")]
        private InputActionReference rightShoulder; // button5

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: P / Select")]
        private InputActionReference select; // button6

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Esc / Start")]
        private InputActionReference start; // button7

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Q / Left Stick Press")]
        private InputActionReference leftStickPress; // button8

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: E / Right Stick Press")]
        private InputActionReference rightStickPress; // button9

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Middle Mouse / Touchpad Press")]
        private InputActionReference touchpadPress; // button10

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector2)]
        [Tooltip("Default: WASD / Left Stick")]
        private InputActionReference leftStick; // axisXY

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector2)]
        [Tooltip("Default: Arrows / Right Stick")]
        private InputActionReference rightStick; // axis45

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector2)]
        [Tooltip("Default: 1, 2, 3, 4 / D-Pad")]
        private InputActionReference dPad; // axis67

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Float)]
        [Tooltip("Default: R Mouse / L Trigger")]
        private InputActionReference leftTrigger; // axis9

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Float)]
        [Tooltip("Default: L Mouse / R Trigger")]
        private InputActionReference rightTrigger; // axis10
        
        // -----
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'south' input changes.")]
        private GgEvent<bool> onSouthStateChanged; // button0
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'east' input changes.")]
        private GgEvent<bool> onEastStateChanged; // button1
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'west' input changes.")]
        private GgEvent<bool> onWestStateChanged; // button2
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'north' input changes.")]
        private GgEvent<bool> onNorthStateChanged; // button3
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'leftShoulder' input changes.")]
        private GgEvent<bool> onLeftShoulderStateChanged; // button4
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'rightShoulder' input changes.")]
        private GgEvent<bool> onRightShoulderStateChanged; // button5
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'start' input changes.")]
        private GgEvent<bool> onStartStateChanged; // button6
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'select' input changes.")]
        private GgEvent<bool> onSelectStateChanged; // button7
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'leftStickPress' input changes.")]
        private GgEvent<bool> onLeftStickPressStateChanged; // button8
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'rightStickPress' input changes.")]
        private GgEvent<bool> onRightStickPressStateChanged; // button9
        
        [SerializeField]
        [Tooltip("Event invoked when the state of 'touchpadPress' input changes.")]
        private GgEvent<bool> onTouchpadPressStateChanged; // button10
        
        [SerializeField]
        [Tooltip("Event invoked when the value of 'left stick' input changes.")]
        private GgEvent<Vector2> onLeftStickValueChanged; // axisXY
        
        [SerializeField]
        [Tooltip("Event invoked when the value of 'right stick' input changes.")]
        private GgEvent<Vector2> onRightStickValueChanged; // axis45
        
        [SerializeField]
        [Tooltip("Event invoked when the value of 'dPad' input changes.")]
        private GgEvent<Vector2> onDPadValueChanged; // axis67
        
        [SerializeField]
        [Tooltip("Event invoked when the value of 'left trigger' input changes.")]
        private GgEvent<float> onLeftTriggerValueChanged; // axis9
        
        [SerializeField]
        [Tooltip("Event invoked when the value of 'right trigger' input changes.")]
        private GgEvent<float> onRightTriggerValueChanged; // axis10
        
        // -----
        
        [SerializeField, ReadOnly]
        [Tooltip("The cached inputs for the current frame.")]
        private GMKInputs inputs;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Getter / Setter
        
        /// <summary>
        /// Get the cached inputs for the current frame.
        /// </summary>
        public GMKInputs Inputs => inputs;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Game Loop

        private void OnEnable()
        {
            // subscribe to input system update loop
            InputSystem.onAfterUpdate += InputSystem_OnAfterUpdate;
        }

        private void OnDisable()
        {
            // unsubscribe from input system update loop
            InputSystem.onAfterUpdate -= InputSystem_OnAfterUpdate;
        }

        private void InputSystem_OnAfterUpdate()
        {
            UpdateButtonInput();
            UpdateAxisInput();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Methods

        private void UpdateButtonInput()
        {
            if (south)
            {
                bool isPressed = south.action.IsPressed();
                if (inputs.south.keypressed != isPressed)
                {
                    onSouthStateChanged?.Invoke(isPressed);
                }
                inputs.south.keydown = south.action.WasPressedThisFrame();
                inputs.south.keypressed = isPressed;
                inputs.south.keyreleased = south.action.WasReleasedThisFrame();
                
            }

            if (east)
            {
                bool isPressed = east.action.IsPressed();
                if (inputs.east.keypressed != isPressed)
                {
                    onEastStateChanged?.Invoke(isPressed);
                }
                inputs.east.keydown = east.action.WasPressedThisFrame();
                inputs.east.keypressed = isPressed;
                inputs.east.keyreleased = east.action.WasReleasedThisFrame();
            }

            if (west)
            {
                bool isPressed = west.action.IsPressed();
                if (inputs.west.keypressed != isPressed)
                {
                    onWestStateChanged?.Invoke(isPressed);
                }
                inputs.west.keydown = west.action.WasPressedThisFrame();
                inputs.west.keypressed = isPressed;
                inputs.west.keyreleased = west.action.WasReleasedThisFrame();
            }

            if (north)
            {
                bool isPressed = north.action.IsPressed();
                if (inputs.north.keypressed != isPressed)
                {
                    onNorthStateChanged?.Invoke(isPressed);
                }
                inputs.north.keydown = north.action.WasPressedThisFrame();
                inputs.north.keypressed = isPressed;
                inputs.north.keyreleased = north.action.WasReleasedThisFrame();
            }

            if (leftShoulder)
            {
                bool isPressed = leftShoulder.action.IsPressed();
                if (inputs.leftShoulder.keypressed != isPressed)
                {
                    onLeftShoulderStateChanged?.Invoke(isPressed);
                }
                inputs.leftShoulder.keydown = leftShoulder.action.WasPressedThisFrame();
                inputs.leftShoulder.keypressed = isPressed;
                inputs.leftShoulder.keyreleased = leftShoulder.action.WasReleasedThisFrame();
            }

            if (rightShoulder)
            {
                bool isPressed = rightShoulder.action.IsPressed();
                if (inputs.rightShoulder.keypressed != isPressed)
                {
                    onRightShoulderStateChanged?.Invoke(isPressed);
                }
                inputs.rightShoulder.keydown = rightShoulder.action.WasPressedThisFrame();
                inputs.rightShoulder.keypressed = isPressed;
                inputs.rightShoulder.keyreleased = rightShoulder.action.WasReleasedThisFrame();
            }

            if (start)
            {
                bool isPressed = start.action.IsPressed();
                if (inputs.start.keypressed != isPressed)
                {
                    onStartStateChanged?.Invoke(isPressed);
                }
                inputs.start.keydown = start.action.WasPressedThisFrame();
                inputs.start.keypressed = isPressed;
                inputs.start.keyreleased = start.action.WasReleasedThisFrame();
            }

            if (select)
            {
                bool isPressed = select.action.IsPressed();
                if (inputs.select.keypressed != isPressed)
                {
                    onSelectStateChanged?.Invoke(isPressed);
                }
                inputs.select.keydown = select.action.WasPressedThisFrame();
                inputs.select.keypressed = isPressed;
                inputs.select.keyreleased = select.action.WasReleasedThisFrame();
            }

            if (leftStickPress)
            {
                bool isPressed = leftStickPress.action.IsPressed();
                if (inputs.leftStickPress.keypressed != isPressed)
                {
                    onLeftStickPressStateChanged?.Invoke(isPressed);
                }
                inputs.leftStickPress.keydown = leftStickPress.action.WasPressedThisFrame();
                inputs.leftStickPress.keypressed = isPressed;
                inputs.leftStickPress.keyreleased = leftStickPress.action.WasReleasedThisFrame();
            }

            if (rightStickPress)
            {
                bool isPressed = rightStickPress.action.IsPressed();
                if (inputs.rightStickPress.keypressed != isPressed)
                {
                    onRightStickPressStateChanged?.Invoke(isPressed);
                }
                inputs.rightStickPress.keydown = rightStickPress.action.WasPressedThisFrame();
                inputs.rightStickPress.keypressed = isPressed;
                inputs.rightStickPress.keyreleased = rightStickPress.action.WasReleasedThisFrame();
            }

            if (touchpadPress)
            {
                bool isPressed = touchpadPress.action.IsPressed();
                if (inputs.touchpadPress.keypressed != isPressed)
                {
                    onTouchpadPressStateChanged?.Invoke(isPressed);
                }
                inputs.touchpadPress.keydown = touchpadPress.action.WasPressedThisFrame();
                inputs.touchpadPress.keypressed = isPressed;
                inputs.touchpadPress.keyreleased = touchpadPress.action.WasReleasedThisFrame();
            }
        }

        private void UpdateAxisInput()
        {
            if (leftStick)
            {
                Vector2 oldValue = inputs.leftStick;
                Vector2 newValue = leftStick.action.ReadValue<Vector2>();
                if (!oldValue.Equals(newValue))
                {
                    onLeftStickValueChanged?.Invoke(newValue);
                }
                inputs.leftStick = newValue;
            }

            if (rightStick)
            {
                Vector2 oldValue = inputs.rightStick;
                Vector2 newValue = rightStick.action.ReadValue<Vector2>();
                if (!oldValue.Equals(newValue))
                {
                    onRightStickValueChanged?.Invoke(newValue);
                }
                inputs.rightStick = newValue;
            }

            if (dPad)
            {
                Vector2 oldValue = inputs.dPad;
                Vector2 newValue = dPad.action.ReadValue<Vector2>();
                if (!oldValue.Equals(newValue))
                {
                    onDPadValueChanged?.Invoke(newValue);
                }
                inputs.dPad = newValue;
            }

            if (leftTrigger)
            {
                float oldValue = inputs.leftTrigger;
                float newValue = leftTrigger.action.ReadValue<float>();
                if (!oldValue.Equals(newValue))
                {
                    onLeftTriggerValueChanged?.Invoke(newValue);
                }
                inputs.leftTrigger = newValue;
            }

            if (rightTrigger)
            {
                float oldValue = inputs.rightTrigger;
                float newValue = rightTrigger.action.ReadValue<float>();
                if (!oldValue.Equals(newValue))
                {
                    onRightTriggerValueChanged?.Invoke(newValue);
                }
                inputs.rightTrigger = newValue;
            }
        }

        #endregion
        
    } // class end
}
#endif