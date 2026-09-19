using System;
using UnityEngine;

/// <summary>
/// Legacy compatibility shim.
/// Prefer using MenuButton / ItemButton (both derive from MenuButtonBase).
/// </summary>
[Obsolete("Use MenuButton or ItemButton instead.")]
public class MenuSetup : MenuButton
{
}
