using UnityEngine;

public sealed class VariantButtonPanelController : MonoBehaviour
{
	[SerializeField] Color selectedColor = new Color(0f, 0.85f, 1f, 1f);
	[SerializeField] Color selectedHoverColor = new Color(0.1f, 1f, 1f, 1f);
	[SerializeField] Color selectedPressedColor = new Color(0f, 0.55f, 0.7f, 1f);
	[SerializeField] Color unselectedColor = new Color(0f, 0.27f, 0.51f, 1f);
	[SerializeField] Color unselectedHoverColor = new Color(0.29f, 0.45f, 0.82f, 1f);
	[SerializeField] Color unselectedPressedColor = new Color(0.2f, 0.25f, 0.37f, 1f);

	NovaSamples.UIControls.Button[] _buttons;

	public void Apply(NovaSamples.UIControls.Button[] buttons, int selectedIndex)
	{
		_buttons = buttons;
		for (int i = 0; i < _buttons.Length; i++)
		{
			if (_buttons[i] == null) continue;
			var view = _buttons[i].GetComponent<Nova.ItemView>();
			if (view == null || !view.TryGetVisuals(out NovaSamples.UIControls.ButtonVisuals visuals)) continue;
			bool selected = i == selectedIndex;
			visuals.DefaultColor = selected ? selectedColor : unselectedColor;
			visuals.HoveredColor = selected ? selectedHoverColor : unselectedHoverColor;
			visuals.PressedColor = selected ? selectedPressedColor : unselectedPressedColor;
			visuals.UpdateVisualState(NovaSamples.UIControls.VisualState.Default);
		}
	}
}
