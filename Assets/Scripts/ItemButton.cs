using UnityEngine;

/// <summary>
/// Represents a leaf "item" node in the YAML tree (no children).
/// Use this to implement item-specific visuals/behavior.
/// </summary>
public class ItemButton : MenuButtonBase
{
	protected override void OnClickLocal()
	{
		// Leaf selection is owned by RadialMenuFromYaml via Owner.HandleItemClicked(...)
		// Add item-specific local behavior here.
	}
}
