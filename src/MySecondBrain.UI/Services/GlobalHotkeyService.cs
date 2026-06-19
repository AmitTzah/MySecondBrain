using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class GlobalHotkeyService : IGlobalHotkeyService
{
    // === P/Invoke Declarations ===

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    // === Constants ===

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int WM_HOTKEY = 0x0312;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // Virtual key codes for low-level hook modifier state checking
    private const int VK_MENU = 0x12;    // Alt
    private const int VK_CONTROL = 0x11; // Ctrl
    private const int VK_SHIFT = 0x10;   // Shift
    private const int VK_LWIN = 0x5B;    // Left Windows
    private const int VK_RWIN = 0x5C;    // Right Windows

    // === Delegate ===

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // === Fields ===

    private readonly ILogger<GlobalHotkeyService> _logger;
    private readonly object _lock = new();

    private int _nextId;
    private HwndSource? _hwndSource;
    private IntPtr _hookId = IntPtr.Zero;

    // Stored to prevent GC from collecting the delegate while the hook is active.
    // If this field is removed, the GC will collect the delegate and the hook
    // callback will crash the process with a callback-on-collected-delegate error.
    private LowLevelKeyboardProc? _hookDelegate;

    // Primary (RegisterHotKey) registrations
    private readonly Dictionary<int, HotkeyAssignment> _registrationsById = new();
    private readonly Dictionary<string, int> _idByHotkeyId = new(StringComparer.OrdinalIgnoreCase);

    // Fallback (WH_KEYBOARD_LL) registrations — RegisterHotKey failed for these
    private readonly Dictionary<string, HotkeyAssignment> _fallbackHotkeys = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    // === Known System Hotkeys ===

    private static readonly (ModifierKeys Modifiers, VirtualKey Key)[] KnownSystemHotkeys =
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

    // === Event ===

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    // === Constructor ===

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger)
    {
        _logger = logger;
        CreateMessageWindow();
        InstallLowLevelHook();
        RegisterDefaultHotkeys();
    }

    // === Public API ===

    public bool RegisterHotkey(string hotkeyId, ModifierKeys modifiers, VirtualKey key)
    {
        ArgumentNullException.ThrowIfNull(hotkeyId);

        lock (_lock)
        {
            if (_disposed)
                return false;

            if (_idByHotkeyId.ContainsKey(hotkeyId))
            {
                _logger.LogWarning("Hotkey '{HotkeyId}' is already registered", hotkeyId);
                return false;
            }

            // Build the assignment
            var assignment = new HotkeyAssignment(hotkeyId, modifiers, key);
            var success = TryRegisterApiHotKey(assignment, out var id);

            if (success)
            {
                _registrationsById[id] = assignment;
                _idByHotkeyId[hotkeyId] = id;
                _logger.LogInformation("Registered hotkey '{HotkeyId}' via RegisterHotKey API", hotkeyId);
            }
            else
            {
                // Fall back to WH_KEYBOARD_LL tracking
                _fallbackHotkeys[hotkeyId] = assignment;
                _logger.LogInformation("Registered hotkey '{HotkeyId}' via WH_KEYBOARD_LL fallback", hotkeyId);
            }

            return true;
        }
    }

    public bool UnregisterHotkey(string hotkeyId)
    {
        ArgumentNullException.ThrowIfNull(hotkeyId);

        lock (_lock)
        {
            if (_disposed)
                return false;

            // Check primary registrations
            if (_idByHotkeyId.TryGetValue(hotkeyId, out var apiId))
            {
                if (_hwndSource is not null && _hwndSource.Handle != IntPtr.Zero)
                    UnregisterHotKey(_hwndSource.Handle, apiId);

                _registrationsById.Remove(apiId);
                _idByHotkeyId.Remove(hotkeyId);
                _logger.LogInformation("Unregistered hotkey '{HotkeyId}' via RegisterHotKey API", hotkeyId);
                return true;
            }

            // Check fallback registrations
            if (_fallbackHotkeys.Remove(hotkeyId))
            {
                _logger.LogInformation("Unregistered hotkey '{HotkeyId}' via WH_KEYBOARD_LL fallback", hotkeyId);
                return true;
            }

            return false;
        }
    }

    public bool IsRegistered(string hotkeyId)
    {
        lock (_lock)
        {
            return _idByHotkeyId.ContainsKey(hotkeyId) || _fallbackHotkeys.ContainsKey(hotkeyId);
        }
    }

    public IReadOnlyList<HotkeyAssignment> GetRegisteredHotkeys()
    {
        lock (_lock)
        {
            var all = new List<HotkeyAssignment>(_registrationsById.Count + _fallbackHotkeys.Count);
            all.AddRange(_registrationsById.Values);
            all.AddRange(_fallbackHotkeys.Values);
            return all.AsReadOnly();
        }
    }

    public bool DetectConflict(ModifierKeys modifiers, VirtualKey key)
    {
        lock (_lock)
        {
            // Check registered hotkeys
            foreach (var kvp in _registrationsById)
            {
                if (kvp.Value.Modifiers == modifiers && kvp.Value.Key == key)
                    return true;
            }

            foreach (var kvp in _fallbackHotkeys)
            {
                if (kvp.Value.Modifiers == modifiers && kvp.Value.Key == key)
                    return true;
            }

            // Check known system hotkeys
            foreach (var (sysMod, sysKey) in KnownSystemHotkeys)
            {
                if (sysMod == modifiers && sysKey == key)
                    return true;
            }

            return false;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        UnregisterAllHotkeys();
        UninstallLowLevelHook();
        DestroyMessageWindow();

        HotkeyTriggered = null;
    }

    // === Internal: RegisterHotKey API ===

    private bool TryRegisterApiHotKey(HotkeyAssignment assignment, out int id)
    {
        id = -1;

        if (_hwndSource is null || _hwndSource.Handle == IntPtr.Zero)
            return false;

        id = ++_nextId; // Thread-safe — caller holds _lock
        var modifiers = ToModifierFlags(assignment.Modifiers);
        var vk = (uint)assignment.Key;

        return RegisterHotKey(_hwndSource.Handle, id, modifiers | MOD_NOREPEAT, vk);
    }

    private static uint ToModifierFlags(ModifierKeys modifiers)
    {
        uint flags = 0;
        if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            flags |= MOD_ALT;
        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            flags |= MOD_CONTROL;
        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            flags |= MOD_SHIFT;
        if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
            flags |= MOD_WIN;
        return flags;
    }

    // === Internal: HwndSource Message Window ===

    private void CreateMessageWindow()
    {
        try
        {
            var parameters = new HwndSourceParameters("MSB_HotkeyWindow")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0, // No title bar, no borders — invisible
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            _logger.LogDebug("Hotkey message window created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create hotkey message window — hotkey registration will fall back to WH_KEYBOARD_LL");
        }
    }

    private void DestroyMessageWindow()
    {
        try
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
            _hwndSource = null;
            _logger.LogDebug("Hotkey message window destroyed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error destroying hotkey message window");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var hotkeyId = wParam.ToInt32();

            HotkeyAssignment? assignment;
            lock (_lock)
            {
                _registrationsById.TryGetValue(hotkeyId, out assignment);
            }

            if (assignment is not null)
            {
                _logger.LogInformation("Hotkey triggered via WM_HOTKEY: '{HotkeyId}' ({Modifiers}+{Key})",
                    assignment.HotkeyId, assignment.Modifiers, assignment.Key);
                HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(assignment.HotkeyId));
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    // === Internal: WH_KEYBOARD_LL Hook ===

    private void InstallLowLevelHook()
    {
        try
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule is null)
            {
                _logger.LogWarning("Cannot get current process main module — WH_KEYBOARD_LL hook not installed");
                return;
            }

            var moduleHandle = GetModuleHandle(curModule.ModuleName);
            if (moduleHandle == IntPtr.Zero)
            {
                _logger.LogWarning("GetModuleHandle returned zero — WH_KEYBOARD_LL hook not installed");
                return;
            }

            // Store the delegate to prevent GC from collecting it
            _hookDelegate = LowLevelHookCallback;

            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookDelegate, moduleHandle, 0);

            if (_hookId == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning("SetWindowsHookEx(WH_KEYBOARD_LL) failed with error {Error} — fallback disabled", error);
                _hookDelegate = null;
            }
            else
            {
                _logger.LogDebug("WH_KEYBOARD_LL hook installed (Id: {HookId})", _hookId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install WH_KEYBOARD_LL hook");
            _hookDelegate = null;
        }
    }

    private void UninstallLowLevelHook()
    {
        if (_hookId == IntPtr.Zero)
            return;

        try
        {
            UnhookWindowsHookEx(_hookId);
            _logger.LogDebug("WH_KEYBOARD_LL hook uninstalled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error uninstalling WH_KEYBOARD_LL hook");
        }

        _hookId = IntPtr.Zero;
        _hookDelegate = null;
    }

    private IntPtr LowLevelHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            // Fast-path: skip if no fallback hotkeys are tracked
            if (_fallbackHotkeys.Count == 0)
                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

            var vkCode = Marshal.ReadInt32(lParam);

            // Get current modifier states
            var altDown = (GetKeyState(VK_MENU) & 0x8000) != 0;
            var ctrlDown = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            var shiftDown = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
            var winDown = ((GetKeyState(VK_LWIN) & 0x8000) != 0) || ((GetKeyState(VK_RWIN) & 0x8000) != 0);

            // Build the current modifier combination
            ModifierKeys currentModifiers = ModifierKeys.None;
            if (altDown) currentModifiers |= ModifierKeys.Alt;
            if (ctrlDown) currentModifiers |= ModifierKeys.Control;
            if (shiftDown) currentModifiers |= ModifierKeys.Shift;
            if (winDown) currentModifiers |= ModifierKeys.Windows;

            var currentKey = (VirtualKey)vkCode;

            // Check fallback hotkeys
            HotkeyAssignment? matchedAssignment = null;
            lock (_lock)
            {
                foreach (var kvp in _fallbackHotkeys)
                {
                    if (kvp.Value.Modifiers == currentModifiers && kvp.Value.Key == currentKey)
                    {
                        matchedAssignment = kvp.Value;
                        break;
                    }
                }
            }

            if (matchedAssignment is not null)
            {
                _logger.LogInformation("Hotkey triggered via WH_KEYBOARD_LL: '{HotkeyId}' ({Modifiers}+{Key})",
                    matchedAssignment.HotkeyId, matchedAssignment.Modifiers, matchedAssignment.Key);
                HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(matchedAssignment.HotkeyId));

                // Pass to next hook in chain so other hooks (accessibility tools, etc.) see the event,
                // then return non-zero to prevent the key from being dispatched to the target window.
                CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // === Internal: Unregister All ===

    private void UnregisterAllHotkeys()
    {
        lock (_lock)
        {
            if (_hwndSource is not null && _hwndSource.Handle != IntPtr.Zero)
            {
                foreach (var kvp in _registrationsById)
                {
                    try
                    {
                        UnregisterHotKey(_hwndSource.Handle, kvp.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error unregistering hotkey ID {HotkeyId}", kvp.Key);
                    }
                }
            }

            _registrationsById.Clear();
            _idByHotkeyId.Clear();
            _fallbackHotkeys.Clear();
        }
    }

    // === Internal: Default Hotkey Registration ===

    private void RegisterDefaultHotkeys()
    {
        var defaults = new (string Id, ModifierKeys Modifiers, VirtualKey Key)[]
        {
            ("CommandBar", ModifierKeys.Alt, VirtualKey.Space),
            ("Rewrite", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.Q),
            ("Summarize", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.W),
            ("Explain", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.E),
            ("Translate", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.R),
            ("ContinueWriting", ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.C),
        };

        foreach (var (id, mod, key) in defaults)
        {
            var success = RegisterHotkey(id, mod, key);
            if (success)
                _logger.LogInformation("Default hotkey registered: '{HotkeyId}' ({Modifiers}+{Key})", id, mod, key);
            else
                _logger.LogWarning("Failed to register default hotkey: '{HotkeyId}' ({Modifiers}+{Key})", id, mod, key);
        }
    }
}
