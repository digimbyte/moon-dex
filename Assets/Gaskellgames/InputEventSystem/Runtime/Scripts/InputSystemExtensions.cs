#if GASKELLGAMES
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public static class InputSystemExtensions
    {
        private enum InputActionControlType
        {
            Any,
            Analog,
            Axis,
            Bone,
            Button,
            Delta,
            Digital,
            Double,
            Dpad,
            Eyes,
            Integer,
            Pose,
            Quaternion,
            Stick,
            Touch,
            Vector2,
            Vector3
        }
		
        private static InputActionControlType ControlType(this InputAction inputAction)
        {
            switch (inputAction.expectedControlType)
            {
                case "Analog":
                    return InputActionControlType.Analog;
                case "Axis":
                    return InputActionControlType.Axis;
                case "Bone":
                    return InputActionControlType.Bone;
                case "Button":
                    return InputActionControlType.Button;
                case "Delta":
                    return InputActionControlType.Delta;
                case "Digital":
                    return InputActionControlType.Digital;
                case "Double":
                    return InputActionControlType.Double;
                case "Dpad":
                    return InputActionControlType.Dpad;
                case "Eyes":
                    return InputActionControlType.Eyes;
                case "Integer":
                    return InputActionControlType.Integer;
                case "Pose":
                    return InputActionControlType.Pose;
                case "Quaternion":
                    return InputActionControlType.Quaternion;
                case "Stick":
                    return InputActionControlType.Stick;
                case "Touch":
                    return InputActionControlType.Touch;
                case "Vector2":
                    return InputActionControlType.Vector2;
                case "Vector3":
                    return InputActionControlType.Vector3;
                default:
                    return InputActionControlType.Any; // if (expectedControlType == "Any")
            }
        }
		
        /// <summary>
        /// The output value type for this InputAction
        /// </summary>
        /// <param name="inputAction"></param>
        /// <returns></returns>
        public static InputActionValueType ValueType(this InputAction inputAction)
        {
            switch (inputAction.ControlType())
            {
                case InputActionControlType.Button:
                    return InputActionValueType.Bool;
				
                case InputActionControlType.Axis:
                    return InputActionValueType.Float;
				
                case InputActionControlType.Vector2:
                    return InputActionValueType.Vector2;
                
                case InputActionControlType.Vector3:
                    return InputActionValueType.Vector3;
                
                case InputActionControlType.Quaternion:
                    return InputActionValueType.Quaternion;
				
                // other values (not yet used / supported by Gaskellgames.InputEventSystem)
                case InputActionControlType.Any:
                case InputActionControlType.Analog:
                case InputActionControlType.Bone:
                case InputActionControlType.Delta:
                case InputActionControlType.Digital:
                case InputActionControlType.Double:
                case InputActionControlType.Dpad:
                case InputActionControlType.Eyes:
                case InputActionControlType.Integer:
                case InputActionControlType.Pose:
                case InputActionControlType.Stick:
                case InputActionControlType.Touch:
                default:
                    return InputActionValueType.Unknown;
            }
        }

        /// <summary>
        /// Check if an axis input is being pressed
        /// </summary>
        /// <param name="inputAction"></param>
        /// <param name="threshold"></param>
        /// <param name="axisAsButton"></param>
        /// <returns>True is InputAction is being polled (InputAction ValueType is float or Vector2)</returns>
        public static bool PollInput_AxisAsButton(InputAction inputAction, float threshold, out bool axisAsButton)
        {
            switch (inputAction.ValueType())
            {
                case InputActionValueType.Bool:
                    // handled by callbacks
                    axisAsButton = false;
                    return false;
                
                case InputActionValueType.Float:
                    float floatValue = inputAction.ReadValue<float>();
                    axisAsButton = threshold < floatValue;
                    return true;
                
                case InputActionValueType.Vector2:
                    Vector2 vector2Value = inputAction.ReadValue<Vector2>();
                    axisAsButton = (threshold < Mathf.Abs(vector2Value.x)) || (threshold < Mathf.Abs(vector2Value.y));
                    return true;
                
                case InputActionValueType.Vector3:
                    Vector3 vector3Value = inputAction.ReadValue<Vector3>();
                    axisAsButton = (threshold < Mathf.Abs(vector3Value.x)) || (threshold < Mathf.Abs(vector3Value.y)) || (threshold < Mathf.Abs(vector3Value.z));
                    return true;
                
                case InputActionValueType.Quaternion:
                    Quaternion quaternionValue = inputAction.ReadValue<Quaternion>();
                    axisAsButton = (threshold < Mathf.Abs(quaternionValue.x)) || (threshold < Mathf.Abs(quaternionValue.y)) || (threshold < Mathf.Abs(quaternionValue.z)) || (threshold < Mathf.Abs(quaternionValue.w));
                    return true;
                
                default: // InputActionValueType.Unknown
                    axisAsButton = false;
                    return false;
            }
        }
		
        /// <summary>
        /// Get the valid InputControlScheme for a specified InputDevice from a list of InputActionAssets
        /// </summary>
        /// <param name="inputActionAssets"></param>
        /// <param name="inputDevice"></param>
        /// <param name="controlScheme"></param>
        /// <returns>True if InputControlScheme, false otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryFindControlScheme(List<InputActionAsset> inputActionAssets, InputDevice inputDevice, out InputControlScheme controlScheme)
        {
            // try to get the control scheme directly from the device
            foreach (InputActionAsset inputActionAsset in inputActionAssets)
            {
                InputControlScheme? foundControlScheme = InputControlScheme.FindControlSchemeForDevice(inputDevice, inputActionAsset.controlSchemes);
                controlScheme = foundControlScheme ?? inputActionAsset.controlSchemes[0];
                if (foundControlScheme.HasValue) { return true; }
            }
            
            // try to get the control scheme from inputActionAsset's controlSchemes' supported devices
            foreach (var inputActionAsset in inputActionAssets)
            {
                foreach (InputControlScheme inputControlScheme in inputActionAsset.controlSchemes)
                {
                    if (inputControlScheme.SupportsDevice(inputDevice))
                    {
                        controlScheme = inputControlScheme;
                        return true;
                    }
                }
            }

            // unable to find control scheme
            if (inputActionAssets[0] == null) { throw new ArgumentNullException(nameof(inputActionAssets), "inputAction [0] is null."); }
            controlScheme = inputActionAssets[0].controlSchemes[0];
            return false;
        }
		
    } // class end
}
#endif