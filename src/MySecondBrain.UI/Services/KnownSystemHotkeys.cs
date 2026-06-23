using System.Windows.Input;

using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

internal static class KnownSystemHotkeys
{
    // === Known System Hotkeys ===

    internal static readonly (ModifierKeys Modifiers, VirtualKey Key)[] All =
    [
        // Win key shortcuts
        (ModifierKeys.Windows, VirtualKey.D),      // Win+D — Show Desktop
        (ModifierKeys.Windows, VirtualKey.L),      // Win+L — Lock
        (ModifierKeys.Windows, VirtualKey.R),      // Win+R — Run
        (ModifierKeys.Windows, VirtualKey.E),      // Win+E — Explorer
        (ModifierKeys.Windows, VirtualKey.S),      // Win+S — Search
        (ModifierKeys.Windows, VirtualKey.F4),     // Win+F4 — Close (not a real system one, but safe)
        // Alt combos
        (ModifierKeys.Alt, VirtualKey.Tab),        // Alt+Tab — Switch
        (ModifierKeys.Alt, VirtualKey.F4),         // Alt+F4 — Close
        (ModifierKeys.Alt, VirtualKey.Escape),     // Alt+Esc — Cycle
        (ModifierKeys.Alt, VirtualKey.Space),      // Alt+Space — System menu
        (ModifierKeys.Alt, VirtualKey.PrintScreen),// Alt+PrtScn — Active window screenshot
        // Ctrl combos
        (ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Delete), // Ctrl+Alt+Del
        (ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.Escape), // Ctrl+Shift+Esc
        // Win+Shift combos
        (ModifierKeys.Windows | ModifierKeys.Shift, VirtualKey.S),    // Win+Shift+S — Snipping
        // Win+Ctrl combos
        (ModifierKeys.Windows | ModifierKeys.Control, VirtualKey.D),  // Win+Ctrl+D — Virtual Desktop
        (ModifierKeys.Windows | ModifierKeys.Control, VirtualKey.Left), // Win+Ctrl+Left — Prev Desktop
        (ModifierKeys.Windows | ModifierKeys.Control, VirtualKey.Right), // Win+Ctrl+Right — Next Desktop
    ];
}
