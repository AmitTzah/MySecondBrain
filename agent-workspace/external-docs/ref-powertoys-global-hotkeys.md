# Reference Implementation: PowerToys Run — Global Hotkey & Overlay System

## Source
**Product:** Microsoft PowerToys Run (v0.80+, open-source, MIT license)  
**Repository:** https://github.com/microsoft/PowerToys  
**Component studied:** Keyboard hook, overlay window management, plugin architecture  

## What It Does
PowerToys Run is a Spotlight-style launcher for Windows triggered by Alt+Space. It captures a global hotkey, displays a centered overlay window, processes user input via plugins, shows results, and dismisses without stealing focus from the active application.

## Architecture (Relevant to MySecondBrain)

### Global Keyboard Hook
- Uses `RegisterHotKey` (Win32) with `Alt+Space` as the modifier+key combination.
- Falls back to low-level keyboard hook (`WH_KEYBOARD_LL` via `SetWindowsHookEx`) for key combinations that `RegisterHotKey` cannot handle.
- The hook runs on a dedicated thread to avoid blocking the message pump.
- Hotkey registration is resilient — if another app has registered the same hotkey, PowerToys warns the user and offers reassignment.
- **MySecondBrain insight:** Use `RegisterHotKey` as primary (less AV-triggering), with `WH_KEYBOARD_LL` as fallback for complex combos. The dedicated thread pattern prevents UI hangs.

### Overlay Window
- `WS_EX_TOOLWINDOW` + `WS_EX_TOPMOST` + `WS_EX_NOACTIVATE` extended window styles prevent the overlay from appearing in the taskbar or stealing focus.
- Window is positioned at horizontal center, ~15% from the top edge (configurable).
- Semi-transparent acrylic/mica background (Windows 11) with drop shadow.
- Window auto-resizes based on result content height up to a max percentage of screen height.
- **MySecondBrain insight:** The `WS_EX_NOACTIVATE` style is critical for the Tier 2 Command Bar — it must appear without stealing focus from the user's current application. The acrylic background provides visual depth without obscuring content behind.

### Dismissal & Focus Management
- Escape dismisses. Click-outside dismisses.
- On dismiss, the previously focused window is restored (saved HWND before showing overlay).
- No focus-stealing race condition — the overlay never activates.
- **MySecondBrain insight:** This focus-preservation pattern is critical for Tier 1 (pill overlay near cursor) and Tier 2 (Command Bar). The three-phase Tier 1 flow (Capture → Result → Apply) is more complex than PowerToys' single-query model.

### Plugin Architecture
- PowerToys Run supports plugins (calculator, search, shell, etc.) via a plugin interface.
- Each plugin receives the query string and returns results asynchronously.
- Results are displayed in a list with icons and action buttons.
- **MySecondBrain insight:** The Persona selector in the Command Bar could follow a similar "plugin" pattern — selecting a Persona changes which AI provider handles the query. But MySecondBrain doesn't need a full plugin architecture for its fixed tool set.

## Key Takeaways for MySecondBrain

| Concept | PowerToys Approach | MySecondBrain Adaptation |
|---------|-------------------|-------------------------|
| Hotkey registration | RegisterHotKey + WH_KEYBOARD_LL fallback | Same, with configurable hotkeys per Text Action |
| Overlay window style | WS_EX_TOOLWINDOW + NOACTIVATE + TOPMOST | Same for Tier 2 Command Bar; Tier 1 needs cursor-relative positioning |
| Focus preservation | Save and restore foreground HWND | Essential for Tier 1 (capture source HWND before overlay) |
| Theme integration | Acrylic/Mica background | Dark/light theming via WPF resource dictionaries |
| Input processing | Single-line query → results | Multi-turn Q&A with Markdown rendering (more complex) |
| Resize behavior | Auto-height up to 70% screen | Same for Command Bar inline Q&A stack |

## Licensing
MIT license. Compatible with MySecondBrain's anticipated licensing model.

## Risk Notes
- PowerToys Run's approach to `WH_KEYBOARD_LL` has occasionally triggered AV false positives (documented in their issue tracker). Code signing mitigates this. MySecondBrain faces the same risk (Vision Flag #6).
- PowerToys Run's overlay window uses Win32/C++ — MySecondBrain will implement similar window styles in WPF via P/Invoke and `WindowInteropHelper`.
