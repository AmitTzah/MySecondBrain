using System.Windows.Input;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class GlobalHotkeyService : IGlobalHotkeyService
{
#pragma warning disable CS0414, CS0067
    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
#pragma warning restore CS0414, CS0067

    public bool RegisterHotkey(string hotkeyId, ModifierKeys modifiers, VirtualKey key) => false;

    public bool UnregisterHotkey(string hotkeyId) => false;

    public bool IsRegistered(string hotkeyId) => false;

    public IReadOnlyList<HotkeyAssignment> GetRegisteredHotkeys() => Array.Empty<HotkeyAssignment>();

    public bool DetectConflict(ModifierKeys modifiers, VirtualKey key) => false;

    public void Dispose()
    {
        HotkeyTriggered = null;
    }
}
