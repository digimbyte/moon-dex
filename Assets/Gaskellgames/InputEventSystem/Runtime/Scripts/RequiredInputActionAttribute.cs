using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gaskellgames.InputEventSystem
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RequiredInputActionAttribute : PropertyAttribute
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;
        
        public bool requiredType = false;
        public bool requiredControlType = false;
        
        public InputActionType inputActionType;
        public InputActionValueType inputActionValueType;
        
        public RequiredInputActionAttribute()
        {
            R = 255;
            G = 000;
            B = 000;
            A = 255;
            
            requiredType = false;
            requiredControlType = false;
            
            this.inputActionType = default;
            this.inputActionValueType = default;
        }
        
        public RequiredInputActionAttribute(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            
            requiredType = false;
            requiredControlType = false;
            
            this.inputActionType = default;
            this.inputActionValueType = default;
        }
        
        public RequiredInputActionAttribute(InputActionType inputActionType)
        {
            R = 255;
            G = 000;
            B = 000;
            A = 255;
            
            requiredType = true;
            requiredControlType = false;
            
            this.inputActionType = inputActionType;
            this.inputActionValueType = default;
        }
        
        public RequiredInputActionAttribute(InputActionType inputActionType, byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            
            requiredType = true;
            requiredControlType = false;
            
            this.inputActionType = inputActionType;
            this.inputActionValueType = default;
        }
        
        public RequiredInputActionAttribute(InputActionValueType inputActionValueType)
        {
            R = 255;
            G = 000;
            B = 000;
            A = 255;
            
            requiredType = false;
            requiredControlType = true;
            
            this.inputActionType = default;
            this.inputActionValueType = inputActionValueType;
        }
        
        public RequiredInputActionAttribute(InputActionValueType inputActionValueType, byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            
            requiredType = false;
            requiredControlType = true;
            
            this.inputActionType = default;
            this.inputActionValueType = inputActionValueType;
        }
        
        public RequiredInputActionAttribute(InputActionType inputActionType, InputActionValueType inputActionValueType)
        {
            R = 255;
            G = 000;
            B = 000;
            A = 255;
            
            requiredType = true;
            requiredControlType = true;
            
            this.inputActionType = inputActionType;
            this.inputActionValueType = inputActionValueType;
        }
        
        public RequiredInputActionAttribute(InputActionType inputActionType, InputActionValueType inputActionValueType, byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            
            requiredType = true;
            requiredControlType = true;
            
            this.inputActionType = inputActionType;
            this.inputActionValueType = inputActionValueType;
        }
        
    } // class end
}
