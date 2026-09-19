#if GASKELLGAMES
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AddComponentMenu("Gaskellgames/Input Event System/XR Input Controller")]
    public class XRInputController : GgMonoBehaviour
    {
        #region Variables

        public enum ControllerType
        {
            Both,
            Left,
            Right
        }

        private enum UpdateType
        {
            Update,
            BeforeRender,
            UpdateAndBeforeRender
        }
        
        [SerializeField]
        [Tooltip("Type of controller this input will control")]
        private ControllerType controllerType = ControllerType.Both;

        public bool isLeftController => controllerType == ControllerType.Both || controllerType == ControllerType.Left;
        public bool isRightController => controllerType == ControllerType.Both || controllerType == ControllerType.Right;
        
        [SerializeField]
        [Tooltip("Update loop(s) to update the controller tracking")]
        private UpdateType trackingType = UpdateType.UpdateAndBeforeRender;
        
        [SerializeField, Required]
        [Tooltip("Reference to the left controller gameObject.")]
        private Transform leftController;
        
        [SerializeField, Required]
        [Tooltip("Reference to the right controller gameObject.")]
        private Transform rightController;
        
        // ---

        [Title("Left Controller Inputs")]
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: MenuButton")]
        private InputActionReference menuButtonL;

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: PrimaryButton")]
        private InputActionReference primaryButtonL;

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: SecondaryButton")]
        private InputActionReference secondaryButtonL;

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: JoystickButton")]
        private InputActionReference joystickButtonL;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector2)]
        [Tooltip("Default: JoystickAxis")]
        private InputActionReference joystickAxisL;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Float)]
        [Tooltip("Default: TriggerAxis")]
        private InputActionReference triggerAxisL;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Float)]
        [Tooltip("Default: GripAxis")]
        private InputActionReference gripAxisL;

        [Title("Left Controller Touch")]
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Primary Touch")]
        private InputActionReference primaryButtonTouchL;
        
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Secondary Touch")]
        private InputActionReference secondaryButtonTouchL;
        
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Joystick Touch")]
        private InputActionReference joystickButtonTouchL;
        
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Trigger Touch")]
        private InputActionReference triggerAxisTouchL;
        
        [Title("Left Controller Tracking")]
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: IsTracked")]
        private InputActionReference isTrackedL;

        [SerializeField, Required]
        [Tooltip("Default: Position"), RequiredInputAction(InputActionType.Value, InputActionValueType.Vector3)]
        private InputActionReference positionL;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Quaternion)]
        [Tooltip("Default: Rotation")]
        private InputActionReference rotationL;
        
        // ---

        [Title("Right Controller Inputs")]
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: MenuButton")]
        private InputActionReference menuButtonR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: PrimaryButton")]
        private InputActionReference primaryButtonR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: SecondaryButton")]
        private InputActionReference secondaryButtonR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: JoystickButton")]
        private InputActionReference joystickButtonR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector2)]
        [Tooltip("Default: JoystickAxis")]
        private InputActionReference joystickAxisR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Float)]
        [Tooltip("Default: TriggerAxis")]
        private InputActionReference triggerAxisR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Float)]
        [Tooltip("Default: GripAxis")]
        private InputActionReference gripAxisR;

        [Title("Right Controller Touch")]
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Primary Touch")]
        private InputActionReference primaryButtonTouchR;
        
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Secondary Touch")]
        private InputActionReference secondaryButtonTouchR;
        
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Joystick Touch")]
        private InputActionReference joystickButtonTouchR;
        
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: Trigger Touch")]
        private InputActionReference triggerAxisTouchR;

        [Title("Right Controller Tracking")]
        [SerializeField, Required, RequiredInputAction(InputActionType.Button, InputActionValueType.Bool)]
        [Tooltip("Default: IsTracked")]
        private InputActionReference isTrackedR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector3)]
        [Tooltip("Default: Position")]
        private InputActionReference positionR;

        [SerializeField, Required, RequiredInputAction(InputActionType.Value, InputActionValueType.Quaternion)]
        [Tooltip("Default: Rotation")]
        private InputActionReference rotationR;
        
        // ---
        
        [SerializeField, ReadOnly]
        [Tooltip("Cached inputs for the current frame.")]
        private XRInputs leftControllerInputs;
        
        [SerializeField, ReadOnly]
        [Tooltip("Cached inputs for the current frame.")]
        private XRTracking leftControllerTracking;
        
        [SerializeField, ReadOnly]
        [Tooltip("Cached inputs for the current frame.")]
        private XRInputs rightControllerInputs;
        
        [SerializeField, ReadOnly]
        [Tooltip("Cached inputs for the current frame.")]
        private XRTracking rightControllerTracking;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Getter / Setter

        /// <summary>
        /// Get the cached inputs for the current frame for the left controller.
        /// </summary>
        public XRInputs LeftControllerInputs => leftControllerInputs;
        
        /// <summary>
        /// Get the cached position and rotation for the current frame for the left controller.
        /// </summary>
        public XRTracking LeftControllerTracking => leftControllerTracking;
        
        /// <summary>
        /// Get the cached inputs for the current frame for the right controller.
        /// </summary>
        public XRInputs RightControllerInputs => rightControllerInputs;
        
        /// <summary>
        /// Get the cached position and rotation for the current frame for the right controller.
        /// </summary>
        public XRTracking RightControllerTracking => rightControllerTracking;

        #endregion
        
        //---------------------------------------------------------------------------------------------------
        
        #region Game Loop
        
        private void OnEnable()
        {
            // subscribe to render update loop
            Application.onBeforeRender += OnBeforeRender;
            
            // subscribe to input system update loop
            InputSystem.onAfterUpdate += InputSystem_OnAfterUpdate;
        }

        private void OnDisable()
        {
            // subscribe to render update loop
            Application.onBeforeRender -= OnBeforeRender;
            
            // subscribe to input system update loop
            InputSystem.onAfterUpdate -= InputSystem_OnAfterUpdate;
        }

        private void InputSystem_OnAfterUpdate()
        {
            if (isLeftController) { HandleUserInputs_LeftController(); }
            if (isRightController) { HandleUserInputs_RightController(); }
        }
        
        private void Update()
        {
            if (trackingType == UpdateType.Update || trackingType == UpdateType.UpdateAndBeforeRender)
            {
                if (isLeftController) { HandleTracking_LeftController(); }
                if (isRightController) { HandleTracking_RightController(); }
            }
        }

        private void OnBeforeRender()
        {
            if (trackingType == UpdateType.BeforeRender || trackingType == UpdateType.UpdateAndBeforeRender)
            {
                if (isLeftController) { HandleTracking_LeftController(); }
                if (isRightController) { HandleTracking_RightController(); }
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region User Inputs
            
        private void HandleUserInputs_LeftController()
        {
            // null check
            if (controllerType != ControllerType.Both && controllerType != ControllerType.Left) { return; }
            
            leftControllerInputs.menuButton.keydown = menuButtonL.action.WasPressedThisFrame();
            leftControllerInputs.menuButton.keypressed = menuButtonL.action.IsPressed();
            leftControllerInputs.menuButton.keyreleased = menuButtonL.action.WasReleasedThisFrame();

            leftControllerInputs.primaryButton.keydown = primaryButtonL.action.WasPressedThisFrame();
            leftControllerInputs.primaryButton.keypressed = primaryButtonL.action.IsPressed();
            leftControllerInputs.primaryButton.keyreleased = primaryButtonL.action.WasReleasedThisFrame();
            leftControllerInputs.joystickButton.keytouched = primaryButtonTouchL.action.IsPressed();

            leftControllerInputs.secondaryButton.keydown = secondaryButtonL.action.WasPressedThisFrame();
            leftControllerInputs.secondaryButton.keypressed = secondaryButtonL.action.IsPressed();
            leftControllerInputs.secondaryButton.keyreleased = secondaryButtonL.action.WasReleasedThisFrame();
            leftControllerInputs.joystickButton.keytouched = secondaryButtonTouchL.action.IsPressed();

            leftControllerInputs.joystickButton.keydown = joystickButtonL.action.WasPressedThisFrame();
            leftControllerInputs.joystickButton.keypressed = joystickButtonL.action.IsPressed();
            leftControllerInputs.joystickButton.keyreleased = joystickButtonL.action.WasReleasedThisFrame();
            leftControllerInputs.joystickButton.keytouched = joystickButtonTouchL.action.IsPressed();

            leftControllerInputs.joystick = joystickAxisL.action.ReadValue<Vector2>();

            leftControllerInputs.triggerAxis = triggerAxisL.action.ReadValue<float>();
            leftControllerInputs.triggerTouch = triggerAxisTouchL.action.IsPressed();

            leftControllerInputs.gripAxis = gripAxisL.action.ReadValue<float>();
        }
            
        private void HandleUserInputs_RightController()
        {
            // null check
            if (controllerType != ControllerType.Both && controllerType != ControllerType.Right) { return; }
            
            rightControllerInputs.menuButton.keydown = menuButtonR.action.WasPressedThisFrame();
            rightControllerInputs.menuButton.keypressed = menuButtonR.action.IsPressed();
            rightControllerInputs.menuButton.keyreleased = menuButtonR.action.WasReleasedThisFrame();

            rightControllerInputs.primaryButton.keydown = primaryButtonR.action.WasPressedThisFrame();
            rightControllerInputs.primaryButton.keypressed = primaryButtonR.action.IsPressed();
            rightControllerInputs.primaryButton.keyreleased = primaryButtonR.action.WasReleasedThisFrame();
            rightControllerInputs.joystickButton.keytouched = primaryButtonTouchR.action.IsPressed();

            rightControllerInputs.secondaryButton.keydown = secondaryButtonR.action.WasPressedThisFrame();
            rightControllerInputs.secondaryButton.keypressed = secondaryButtonR.action.IsPressed();
            rightControllerInputs.secondaryButton.keyreleased = secondaryButtonR.action.WasReleasedThisFrame();
            rightControllerInputs.joystickButton.keytouched = secondaryButtonTouchR.action.IsPressed();

            rightControllerInputs.joystickButton.keydown = joystickButtonR.action.WasPressedThisFrame();
            rightControllerInputs.joystickButton.keypressed = joystickButtonR.action.IsPressed();
            rightControllerInputs.joystickButton.keyreleased = joystickButtonR.action.WasReleasedThisFrame();
            rightControllerInputs.joystickButton.keytouched = joystickButtonTouchR.action.IsPressed();

            rightControllerInputs.joystick = joystickAxisR.action.ReadValue<Vector2>();

            rightControllerInputs.triggerAxis = triggerAxisR.action.ReadValue<float>();
            rightControllerInputs.triggerTouch = triggerAxisTouchR.action.IsPressed();

            rightControllerInputs.gripAxis = gripAxisR.action.ReadValue<float>();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Tracking
        
        private void HandleTracking_LeftController()
        {
            if (leftController && isLeftController)
            {
                // cache values
                leftControllerTracking.isTracked = isTrackedR.action.ReadValue<float>();
                leftControllerTracking.position = positionR.action.ReadValue<Vector3>();
                leftControllerTracking.rotation = rotationR.action.ReadValue<Quaternion>();

                // apply values
                if (1 <= leftControllerTracking.isTracked)
                {
                    leftController.localPosition = leftControllerTracking.position;
                    leftController.localRotation = leftControllerTracking.rotation;
                }
            }
        }

        private void HandleTracking_RightController()
        {
            if (rightController && isRightController)
            {
                // cache values
                rightControllerTracking.isTracked = isTrackedL.action.ReadValue<float>();
                rightControllerTracking.position = positionL.action.ReadValue<Vector3>();
                rightControllerTracking.rotation = rotationL.action.ReadValue<Quaternion>();

                // apply values
                if (1 <= rightControllerTracking.isTracked)
                {
                    rightController.localPosition = rightControllerTracking.position;
                    rightController.localRotation = rightControllerTracking.rotation;
                }
            }
        }

        #endregion
        
    } // class end
}
#endif