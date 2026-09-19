#if GASKELLGAMES
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AddComponentMenu("Gaskellgames/Input Event System/Gamepad Cursor")]
    public class GamepadCursor : GgMonoBehaviour
    {
        #region Variables

        [Title("Cursor")]
        [SerializeField]
        [Tooltip("Toggle whether the hardware cursor and gamepad cursor should be automatically shown/hidden when the control scheme changes.")]
        private bool autoHideCursor = true;
        
        [SerializeField, ShowIf(nameof(autoHideCursor)), Required]
        [Tooltip("Reference to the CursorLockState component this GamePadCursor will control.")]
        private CursorLockState cursorLockState;
        
        [SerializeField, ShowIf(nameof(autoHideCursor)), ReadOnly]
        [Tooltip("Reference to the InputEventManager component this GamePadCursor is subscribed to [Auto-set at runtime].")]
        private InputEventManager inputEventManager;

        [SerializeField, Required]
        [Tooltip("The canvas used for the software cursor. This should be a separate canvas solely for the cursor object")]
        private Canvas cursorCanvas;
        
        [SerializeField, Required]
        [Tooltip("The transform for the software cursor. Will only be set if a software cursor is used (see 'Cursor Mode'). Moving the cursor updates the anchored position of the transform.")]
        private RectTransform cursorOffset;

        [SerializeField, Required]
        [Tooltip("The image gameObject used for the cursor")]
        private Image cursorImage;
        
        [SerializeField, Range(0, 1000, true)]
        [Tooltip("Speed in pixels per second with which to move the cursor. Scaled by the input from 'Stick Action'.")]
        private float cursorSpeed = 500;
        
        [SerializeField, Range(0, 100, true)]
        [Tooltip("Scale factor to apply to 'Scroll Wheel Action' when setting the mouse 'scrollWheel' control.")]
        private float scrollSpeed = 50;

        [Title("Inputs", "Do not add InputActions that contain mouse inputs. This may result in invalid inputs!")]
        [SerializeField, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector2), Required]
        [Tooltip("Vector2 action that moves the cursor left/right (X) and up/down (Y) on screen.")]
        private InputActionReference moveAxis;
        
        [SerializeField, RequiredInputAction(InputActionType.Value, InputActionValueType.Vector2), Required]
        [Tooltip("Vector2 action that feeds into the mouse 'scrollWheel' action (scaled by 'Scroll Speed').")]
        private InputActionReference scrollAxis;
        
        [SerializeField, RequiredInputAction, Required]
        [Tooltip("Button action that triggers a left-click on the mouse.")]
        private InputActionReference leftButton;
        
        [SerializeField, RequiredInputAction, Required]
        [Tooltip("Button action that triggers a middle-click on the mouse.")]
        private InputActionReference middleButton;
        
        [SerializeField, RequiredInputAction, Required]
        [Tooltip("Button action that triggers a right-click on the mouse.")]
        private InputActionReference rightButton;
        
        [SerializeField, RequiredInputAction, Required]
        [Tooltip("Button action that triggers a forward button (button #4) click on the mouse.")]
        private InputActionReference forwardButton;
        
        [SerializeField, RequiredInputAction, Required]
        [Tooltip("Button action that triggers a back button (button #5) click on the mouse.")]
        private InputActionReference backButton;
        
        private Mouse virtualMouse;
        private double previousTime;
        private Vector2 previousStickValue;

        private readonly bool updateDelta = false;
        private readonly float threshold = 0.9f;

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region On Validate

        private void OnValidate()
        {
            if (cursorImage)
            {
                cursorImage.raycastTarget = false;
            }

            if (cursorCanvas)
            {
                cursorCanvas.sortingOrder = 32000;
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Game Loop
        
        protected void OnEnable()
        {
            // add virtual mouse device
            if (virtualMouse == null) { virtualMouse = (Mouse)InputSystem.AddDevice("VirtualMouse"); }
            else if (!virtualMouse.added) { InputSystem.AddDevice(virtualMouse); }

            // initialise cursor position
            if (cursorOffset != null) { InputState.Change(virtualMouse.position, cursorOffset.position); }
            
            // subscribe to InputEventManager
            if (!inputEventManager) { inputEventManager = FindAnyObjectByType<InputEventManager>(); }
            if (inputEventManager) { inputEventManager.onControlsChanged.AddListener(InputEventManager_OnControlsChanged); }
            
            // handle actions
            SubscribeToInputActions(true);

            // subscribe to input system update loop
            InputSystem.onAfterUpdate += InputSystem_OnAfterUpdate;
            
            // set cursor mode
            cursorCanvas.enabled = true;
        }
        
        protected void OnDisable()
        {
            // remove virtual mouse device
            if (virtualMouse != null && virtualMouse.added) { InputSystem.RemoveDevice(virtualMouse); }
            
            // unsubscribe to InputEventManager
            if (!inputEventManager) { inputEventManager = FindAnyObjectByType<InputEventManager>(); }
            if (inputEventManager) { inputEventManager.onControlsChanged.RemoveListener(InputEventManager_OnControlsChanged); }
            
            // handle actions
            SubscribeToInputActions(false);
            
            // unsubscribe from input system update loop
            InputSystem.onAfterUpdate -= InputSystem_OnAfterUpdate;
            
            // reset cache
            previousTime = default;
            previousStickValue = default;
            
            // set cursor mode
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorCanvas.enabled = false;
        }

        private void InputSystem_OnAfterUpdate()
        {
            SetInputState_MouseAxis();

            if (leftButton && InputSystemExtensions.PollInput_AxisAsButton(leftButton.action, threshold, out bool axisIsPressed))
            {
                SetInputState_MouseButton(MouseButton.Left, axisIsPressed);
            }
            if (middleButton && InputSystemExtensions.PollInput_AxisAsButton(middleButton.action, threshold, out axisIsPressed))
            {
                SetInputState_MouseButton(MouseButton.Middle, axisIsPressed);
            }
            if (rightButton && InputSystemExtensions.PollInput_AxisAsButton(rightButton.action, threshold, out axisIsPressed))
            {
                SetInputState_MouseButton(MouseButton.Right, axisIsPressed);
            }
            if (forwardButton && InputSystemExtensions.PollInput_AxisAsButton(forwardButton.action, threshold, out axisIsPressed))
            {
                SetInputState_MouseButton(MouseButton.Forward, axisIsPressed);
            }
            if (backButton && InputSystemExtensions.PollInput_AxisAsButton(backButton.action, threshold, out axisIsPressed))
            {
                SetInputState_MouseButton(MouseButton.Back, axisIsPressed);
            }
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Callbacks

        private void OnLeftButton_Performed(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Left, true); }
        private void OnLeftButton_Canceled(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Left, false); }
        private void OnMiddleButton_Performed(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Middle, true); }
        private void OnMiddleButton_Canceled(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Middle, false); }
        private void OnRightButton_Performed(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Right, true); }
        private void OnRightButton_Canceled(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Right, false); }
        private void OnForwardButton_Performed(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Forward, true); }
        private void OnForwardButton_Canceled(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Forward, false); }
        private void OnBackButton_Performed(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Back, true); }
        private void OnBackButton_Canceled(InputAction.CallbackContext callbackContext) { SetInputState_MouseButton(MouseButton.Back, false); }

        private void InputEventManager_OnControlsChanged(InputControlScheme inputControlScheme)
        {
            if (!autoHideCursor) { return; }
            if (!cursorLockState) { return; }
            if (!cursorImage) { return; }
            
            bool mouseInputSupported = inputControlScheme.SupportsDevice(virtualMouse);
            cursorLockState.SetCursorLockState(mouseInputSupported);
            cursorImage.gameObject.SetActive(!mouseInputSupported);
        }

        private void SubscribeToInputActions(bool enable)
        {
            if (enable)
            {
                if (leftButton && leftButton.action.ValueType() == InputActionValueType.Bool)
                {
                    leftButton.action.performed += OnLeftButton_Performed; 
                    leftButton.action.canceled += OnLeftButton_Canceled;
                }
                if (middleButton && middleButton.action.ValueType() == InputActionValueType.Bool)
                {
                    middleButton.action.performed += OnMiddleButton_Performed;
                    middleButton.action.canceled += OnMiddleButton_Canceled;
                }
                if (rightButton && rightButton.action.ValueType() == InputActionValueType.Bool)
                {
                    rightButton.action.performed += OnRightButton_Performed;
                    rightButton.action.canceled += OnRightButton_Canceled;
                }
                if (forwardButton && forwardButton.action.ValueType() == InputActionValueType.Bool)
                {
                    forwardButton.action.performed += OnForwardButton_Performed;
                    forwardButton.action.canceled += OnForwardButton_Canceled;
                }
                if (backButton && backButton.action.ValueType() == InputActionValueType.Bool)
                {
                    backButton.action.performed += OnBackButton_Performed;
                    backButton.action.canceled += OnBackButton_Canceled;
                }
            }
            else
            {
                if (leftButton)
                {
                    leftButton.action.performed -= OnLeftButton_Performed;
                    leftButton.action.canceled -= OnLeftButton_Canceled;
                }
                if (middleButton)
                {
                    middleButton.action.performed -= OnMiddleButton_Performed;
                    middleButton.action.canceled -= OnMiddleButton_Canceled;
                }
                if (rightButton)
                {
                    rightButton.action.performed -= OnRightButton_Performed;
                    rightButton.action.canceled -= OnRightButton_Canceled;
                }
                if (forwardButton)
                {
                    forwardButton.action.performed -= OnForwardButton_Performed;
                    forwardButton.action.canceled -= OnForwardButton_Canceled;
                }
                if (backButton)
                {
                    backButton.action.performed -= OnBackButton_Performed;
                    backButton.action.canceled -= OnBackButton_Canceled;
                }
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Methods

        /// <summary>
        /// button inputs are handled via callbacks
        /// </summary>
        /// <param name="mouseButton"></param>
        /// <param name="inputState"></param>
        private void SetInputState_MouseButton(MouseButton mouseButton, bool inputState)
        {
            // get mouse state with buttons
            virtualMouse.CopyState(out MouseState mouseState);
            mouseState.WithButton(mouseButton, inputState);
            
            // set input value
            InputState.Change(virtualMouse, mouseState);
            Log(GgLogType.Info, "MouseButton: {0} | InputState: {1}", mouseButton, inputState);
        }
        
        /// <summary>
        /// axis inputs are handled in the InputSystem's OnAfterUpdate loop
        /// </summary>
        private void SetInputState_MouseAxis()
        {
            // null check
            if (virtualMouse == null) { return; }
            
            // update scroll wheel.
            if (scrollAxis.action != null)
            {
                Vector2 scrollValue = scrollAxis.action.ReadValue<Vector2>();
                if (!Mathf.Approximately(0, scrollValue.x) || !Mathf.Approximately(0, scrollValue.y))
                {
                    scrollValue.x *= scrollSpeed;
                    scrollValue.y *= scrollSpeed;

                    // set input value
                    InputState.Change(virtualMouse.scroll, scrollValue);
                    Log(GgLogType.Info, "MouseButton: Scroll | ScrollValue: {0}", scrollValue);
                }
            }
            
            // update move cursor
            if (moveAxis.action == null) { return; }
            Vector2 stickValue = moveAxis.action.ReadValue<Vector2>();
            if (!Mathf.Approximately(0, stickValue.x) || !Mathf.Approximately(0, stickValue.y))
            {
                double currentTime = InputState.currentTime;
                if (Mathf.Approximately(0, previousStickValue.x) && Mathf.Approximately(0, previousStickValue.y))
                {
                    // movement started.
                    previousTime = currentTime;
                }

                // calculate delta.
                float deltaTime = (float)(currentTime - previousTime);
                Vector2 delta = new Vector2(cursorSpeed * stickValue.x * deltaTime, cursorSpeed * stickValue.y * deltaTime);

                // update position.
                Vector2 currentPosition = virtualMouse.position.ReadValue();
                Vector2 newPosition = currentPosition + delta;

                // clamp to screen.
                newPosition.x = Mathf.Clamp(newPosition.x, 0, Screen.width);
                newPosition.y = Mathf.Clamp(newPosition.y, 0, Screen.height);
                
                // set input value
                InputState.Change(virtualMouse.position, newPosition);
                if (updateDelta) { InputState.Change(virtualMouse.delta, delta); }
                Log(GgLogType.Info, "MouseButton: Move | MoveValue: {0}", delta);

                // update software cursor transform
                if (cursorOffset != null) { cursorOffset.position = newPosition; }

                // cache values
                previousStickValue = stickValue;
                previousTime = currentTime;
            }
            else
            {
                // movement stopped.
                previousTime = default;
                previousStickValue = default;
            }
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Getter / Setter

        public Canvas CursorCanvas
        {
            get => cursorCanvas;
            set => cursorCanvas = value;
        }

        public RectTransform CursorOffset
        {
            get => cursorOffset;
            set => cursorOffset = value;
        }

        public Image CursorImage
        {
            get => cursorImage;
            set => cursorImage = value;
        }

        public float CursorSpeed
        {
            get => cursorSpeed;
            set => cursorSpeed = value;
        }

        public float ScrollSpeed
        {
            get => scrollSpeed;
            set => scrollSpeed = value;
        }

        #endregion
        
    } // class end
}

#endif