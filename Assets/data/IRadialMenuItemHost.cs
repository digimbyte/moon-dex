using UnityEngine;

public interface IRadialMenuItemHost
{
	// Called after the item is spawned and configured.
	// You own selection, hover, click, labels, animations, etc.
	void Bind(RadialMenuFromYaml.MenuNode node, int index, float startDeg, float wedgeDeg);
}
