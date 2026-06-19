# RegisterHotKey & WH_KEYBOARD_LL — External Documentation

## Platform
Windows Global Keyboard Hooks — via P/Invoke `user32.dll`

## Overview
MySecondBrain uses a two-tier global hotkey strategy:
1. **Primary:** `RegisterHotKey` — kernel-level hotkey registration, reliable, less AV suspicion
2. **Fallback:** `WH_KEYBOARD_LL` — low-level keyboard hook for combos `RegisterHotKey` cannot handle

Both feed into the same `HotkeyTriggered` event so consumers are mechanism-agnostic.

## Key API Reference

### RegisterHotKey (Primary)
```csharp
[DllImport("user32.dll", SetLastError = true)]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll", SetLastError = true)]
private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

// Modifier constants
private const uint MOD_ALT = 0x0001;
private const uint MOD_CONTROL = 0x0002;
private const uint MOD_SHIFT = 0x0004;
private const uint MOD_WIN = 0x0008;
private const uint MOD_NOREPEAT = 0x4000;

// WM_HOTKEY message constant
private const int WM_HOTKEY = 0x0312;
```

### RegisterHotKey Usage
```csharp
// Register Alt+Q (virtual key code for 'Q' = 0x51)
bool success = RegisterHotKey(
    hwnd,           // Handle to the window that will receive WM_HOTKEY messages
    1,              // Hotkey ID (unique per registration)
    MOD_ALT,        // Modifier keys
    0x51            // Virtual key code for 'Q'
);

// Unregister
UnregisterHotKey(hwnd, 1);
```

### WH_KEYBOARD_LL (Fallback)
```csharp
[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
private static extern IntPtr SetWindowsHookEx(
    int idHook,
    LowLevelKeyboardProc lpfn,
    IntPtr hMod,
    uint dwThreadId);

[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool UnhookWindowsHookEx(IntPtr hhk);

[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
private static extern IntPtr CallNextHookEx(
    IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

private const int WH_KEYBOARD_LL = 13;
private const int WM_KEYDOWN = 0x0100;
private const int WM_SYSKEYDOWN = 0x0104;

// Low-level keyboard callback delegate
private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
```

### Low-Level Keyboard Hook Callback
```csharp
private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
{
    if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
    {
        int vkCode = Marshal.ReadInt32(lParam);

        // Check if Alt key is pressed
        bool altDown = (GetKeyState(VK_MENU) & 0x8000) != 0;

        if (altDown && vkCode == VK_SPACE)
        {
            // Alt+Space detected — fire HotkeyTriggered for Command Bar
            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs("CommandBar"));
            return (IntPtr)1; // Suppress key
        }
    }

    return CallNextHookEx(_hookId, nCode, wParam, lParam);
}
```

### HwndSource Hook (WPF Message Pump Integration)
```csharp
// Create a hidden window to receive WM_HOTKEY messages
private HwndSource _hwndSource;

private void CreateMessageWindow()
{
    var parameters = new HwndSourceParameters("HotkeyWindow")
    {
        Width = 0,
        Height = 0,
        WindowStyle = 0,
    };
    _hwndSource = new HwndSource(parameters);
    _hwndSource.AddHook(WndProc);
}

private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    if (msg == WM_HOTKEY)
    {
        int hotkeyId = wParam.ToInt32();
        // Look up hotkey by ID and fire event
        var assignment = GetAssignmentById(hotkeyId);
        if (assignment != null)
        {
            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(assignment.HotkeyId));
            handled = true;
        }
    }
    return IntPtr.Zero;
}
```

## MySecondBrain Usage

### GlobalHotkeyService (IGlobalHotkeyService)
The `GlobalHotkeyService` class (in `UI/Services/GlobalHotkeyService.cs`) implements:

- **RegisterHotkey(hotkeyId, modifiers, key)**: Tries `RegisterHotKey` first. If that fails, falls back to `WH_KEYBOARD_LL` hook. Returns `true` on success.
- **UnregisterHotkey(hotkeyId)**: Removes from tracking. Calls `UnregisterHotKey` if registered via primary; removes from LL hook check logic if registered via fallback.
- **IsRegistered(hotkeyId)**: Check if hotkey is currently active.
- **GetRegisteredHotkeys()**: Returns all currently registered hotkey assignments.
- **DetectConflict(modifiers, key)**: Check if this combination is already registered or conflicts with known Windows system hotkeys (Win+D, Win+L, Win+R, Alt+Tab, Alt+F4, Ctrl+Alt+Del, Ctrl+Shift+Esc).
- **HotkeyTriggered event**: Fires when any registered hotkey is pressed. Consumers receive `HotkeyTriggeredEventArgs` with the `HotkeyId`.

### Default Hotkey Assignments (from TextAction seed data)
| Hotkey | Action |
|--------|--------|
| Alt+Q | Rewrite |
| Alt+W | Summarize |
| Alt+E | Explain |
| Alt+R | Translate |
| Alt+C | Continue Writing |
| Alt+Space | Command Bar |

### Important Notes
- Global keyboard hooks may trigger antivirus false positives (Flag #6). Code signing mitigates this.
- `RegisterHotKey` requires a message pump and a valid window handle — the `HwndSource` hidden window provides this.
- `WH_KEYBOARD_LL` hooks MUST call `CallNextHookEx` to avoid breaking other applications' hooks.
- The low-level hook is registered with `dwThreadId = 0` (system-wide).
- Hotkey IDs are sequentially assigned integers starting from 0.

## Source
Microsoft Docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
Microsoft Docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexa
WPF HwndSource: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource
