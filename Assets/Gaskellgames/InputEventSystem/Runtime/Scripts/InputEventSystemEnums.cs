using Gaskellgames;

namespace Gaskellgames.InputEventSystem
{
    #region InputActionControlType

    public enum InputActionValueType
    {
        /// <summary>Used for any other or unknown value type. (Likely not yet supported by <see cref="Gaskellgames.InputEventSystem"/>)</summary>
        Unknown,
			
        /// <summary>The value cannot be anything other than 0 or 1.</summary>
        Bool,
			
        /// <summary>A 1D floating-point axis.</summary>
        Float,
			
        /// <summary>A 2D floating-point vector.</summary>
        Vector2,
			
        /// <summary>A 3D floating-point vector.</summary>
        Vector3,
			
        /// <summary>A special case 4D floating-point vector.</summary>
        Quaternion,
    }

    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    
} // namespace end
