using System.Windows.Input;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IGlobalHotkeyService : IDisposable
{
    bool RegisterHotkey(string hotkeyId, ModifierKeys modifiers, VirtualKey key);
    bool UnregisterHotkey(string hotkeyId);
    bool IsRegistered(string hotkeyId);
    IReadOnlyList<HotkeyAssignment> GetRegisteredHotkeys();
    bool DetectConflict(ModifierKeys modifiers, VirtualKey key);
    event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
}
