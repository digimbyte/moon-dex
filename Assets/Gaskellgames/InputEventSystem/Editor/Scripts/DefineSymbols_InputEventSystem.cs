#if UNITY_EDITOR
using UnityEditor;

namespace Gaskellgames.InputEventSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [InitializeOnLoad]
    public class DefineSymbols_InputEventSystem : AssetModificationProcessor
    {
        #region Variables
        
        private static readonly string[] ExtraScriptingDefineSymbols = new string[] { "GASKELLGAMES_INPUTEVENTSYSTEM" };
        private static readonly string ThisFileName = "DefineSymbols_InputEventSystem.cs";
        private static readonly string packageName = "InputEventSystem";
        
        private static readonly string error_failedRemoveSymbols = $"{link_GgCore} not detected: {packageName} package failed to automatically remove ScriptingDefineSymbols from the project settings!";

        private static readonly string link_GgCore = "<a href=\"https://assetstore.unity.com/packages/tools/utilities/ggcore-gaskellgames-304325\">GgCore</a>";
        private static readonly string error = $"{link_GgCore} not detected: The {packageName} package from Gaskellgames requires {link_GgCore}. Please add the package from the package manager, or claim it for FREE from the Unity Asset Store using the same account that has the {packageName} asset licence.";
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region AssetModificationProcessor

        static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
        {
            // If, and only if, the filename matches this filename, remove ScriptingDefineSymbols
            string[] pathSplit = path.Split("/");
            if (pathSplit[^1] == ThisFileName)
            {
#if GASKELLGAMES
                Gaskellgames.EditorOnly.DefineSymbols_GgCore.RemoveExtraScriptingDefineSymbols(ExtraScriptingDefineSymbols);
#else
                UnityEngine.Debug.LogError(error_failedRemoveSymbols);
#endif
            }
            
            // return of 'DidNotDelete' tells the Unity internal implementation that this 'OnWillDeleteAsset' callback
            // did not delete the asset: so the asset will instead be deleted by the Unity internal implementation.
            return AssetDeleteResult.DidNotDelete;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Constructors

        static DefineSymbols_InputEventSystem()
        {
#if !ENABLE_INPUT_SYSTEM
            UnityEngine.Debug.LogError("Input System not detected! Please download via the package manager.");
#endif
            
#if GASKELLGAMES
            Gaskellgames.EditorOnly.DefineSymbols_GgCore.AddExtraScriptingDefineSymbols(ExtraScriptingDefineSymbols);
#else
            UnityEngine.Debug.LogError(error);
#endif
        }
        
        #endregion
        
    } // class end
}

#endif