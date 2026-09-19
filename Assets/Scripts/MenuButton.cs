using UnityEngine;

/// <summary>
/// Represents a "menu" node (folder) in the YAML tree.
/// If you want menu-only visuals/behavior, override the virtual hooks in MenuButtonBase.
/// </summary>
public class MenuButton : MenuButtonBase
{
	protected override void OnClickLocal()
	{
		// Navigation is owned by RadialMenuFromYaml via Owner.HandleItemClicked(...)
		// Keep this override for menu-specific local behavior if needed.
	}
}
