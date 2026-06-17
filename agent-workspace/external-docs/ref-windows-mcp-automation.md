# Reference Implementation: Windows-MCP — Windows UI Automation & System Integration

## Source
**Product:** Windows-MCP (MIT license, open-source)  
**Repository:** https://github.com/CursorTouch/Windows-MCP  
**Version studied:** Latest (2025-2026)  
**Component studied:** UI Automation, text injection, keyboard/mouse simulation, clipboard, process management, PowerShell execution, screenshot capture  

## What It Does
Windows-MCP is an MCP (Model Context Protocol) server that enables AI agents to interact with the Windows operating system. It wraps Windows UI Automation, Win32 APIs, and system utilities into a set of 20+ MCP tools accessible to any MCP-compatible LLM client. It has over 2M users in Claude Desktop extensions.

## Architecture (Relevant to MySecondBrain)

### UI Automation Engine
- Uses Python's `uiautomation` library (wrapper around Windows UIA COM interfaces).
- Provides tools: Click, Type, Scroll, Move, Shortcut, Wait, WaitFor, Screenshot, Snapshot.
- `Snapshot` tool: captures full desktop UI tree including interactive element IDs, scrollable regions, and browser DOM (via `use_dom=True` for Chrome/Edge/Firefox).
- Element discovery: by name, automation ID, control type, class name, or coordinates.
- **MySecondBrain insight:** MySecondBrain needs the same UIA capabilities but from .NET. The architecture patterns (element discovery, multi-layered fallback, snapshot-based state) are directly transferable. The key difference: MSB will implement these in C# via `System.Windows.Automation` and `FlaUI`, not Python.

### Text Injection Methods
- **Type tool:** Types text character-by-character into the focused control using `SendKeys` or UIA `ValuePattern.SetValue()`.
- **Clipboard tool:** Reads/writes Windows clipboard with format preservation.
- **MultiEdit tool:** Enters text into multiple fields via bulk label-to-coordinate resolution.
- **MySecondBrain insight:** Windows-MCP validates the multi-layered approach recommended in MSB's tech-sourcing (#6): try UIA ValuePattern first, fall back to SendKeys/SendInput, fall back to clipboard. MSB should implement this exact pattern but in C#. Windows-MCP's `Type` tool using character-by-character input may be too slow for MSB's Tier 1 "instant" text replacement — `ValuePattern.SetValue()` or `WM_SETTEXT` should be preferred.

### Keyboard & Hotkey Simulation
- **Shortcut tool:** Presses keyboard shortcuts (Ctrl+C, Alt+Tab, etc.) via `SendInput`.
- **MultiSelect tool:** Selects multiple items with optional Ctrl key for multi-select.
- **MySecondBrain insight:** MSB's Tier 1 Apply fallback (simulate Ctrl+V) can use the same `SendInput` approach. Windows-MCP validates this as a reliable pattern.

### Screenshot & Screen Analysis
- Fast screenshot-first capture (dxcam → mss → pillow backends).
- Can capture specific displays or all displays.
- Snapshot includes both screenshot image AND UI tree for element-level interaction.
- Optional visual flash to confirm capture area.
- **MySecondBrain insight:** The screenshot capability is relevant for MSB's deferred T4 (Screenshot/Screen Awareness) feature. The multi-backend approach (dxcam → mss → pillow) provides robustness.

### Process & App Management
- **App tool:** Launches applications from Start Menu, resizes/moves windows, switches between apps.
- **Process tool:** Lists running processes, terminates by PID or name.
- **MySecondBrain insight:** MSB's terminal execution (#17) and HWND management (#6) can reference these patterns. The App tool's window manipulation (resize, move, switch) is directly relevant to Tier 1's source window management.

### PowerShell & Registry
- **PowerShell tool:** Executes PowerShell commands and returns output.
- **Registry tool:** Reads/writes/deletes Windows registry keys.
- **MySecondBrain insight:** For MSB's terminal execution (#17), the PowerShell execution pattern is relevant but MSB also needs cmd.exe and arbitrary shell support. Windows-MCP's approach of capturing stdout/stderr is the standard pattern.

### MCP Protocol Architecture
- Supports stdio, SSE, and Streamable HTTP transports.
- Auth via Bearer token, IP allowlists, OAuth 2.0 + PKCE.
- Tool whitelisting/blacklisting for security.
- **MySecondBrain insight:** MSB could potentially expose its own capabilities as an MCP server, allowing external AI agents to interact with MySecondBrain's features. This is a deferred consideration (not in current vision). More immediately, MSB's local WebSocket server (#8) serves a similar purpose with a simpler protocol.

## Key Takeaways for MySecondBrain

| Concept | Windows-MCP Approach | MySecondBrain Adaptation |
|---------|---------------------|-------------------------|
| UI Automation | Python `uiautomation` library | .NET `System.Windows.Automation` + FlaUI |
| Text injection | Type tool (char-by-char) + Clipboard | ValuePattern.SetValue() + WM_SETTEXT + clipboard fallback |
| Element discovery | By name/ID/type/class/coordinates | Same approach in C# |
| Screenshot | dxcam → mss → pillow backends | Deferred (T4). Use similar multi-backend pattern |
| Process execution | PowerShell only (via PowerShell tool) | cmd.exe + PowerShell + any shell (broader scope) |
| Keyboard simulation | SendInput via Shortcut tool | Same for Ctrl+V fallback in Tier 1 Apply |
| Protocol | MCP (stdio/SSE/HTTP) | Local WebSocket (simpler, single-purpose) |
| Security | Auth, IP allowlist, tool whitelist | Token auth (WebSocket), mandatory confirmation (terminal), N8 restrictions (wiki) |

## Licensing
MIT license. Fully compatible. Windows-MCP's patterns can be freely adapted.

## Risk Notes
- Windows-MCP is Python-based. MSB is C#/.NET. The architectural patterns transfer, but code does not. MSB must implement UIA, SendInput, and clipboard handling in C#.
- Windows-MCP's `Type` tool uses character-by-character input which is too slow for MSB's Tier 1 (needs instant text replacement). MSB should prefer bulk text injection methods.
- Windows-MCP validates that UIA-based text injection works broadly across Windows applications, reducing the risk for MSB's #6 component (HWND Capture & Text Injection).
- For rapid prototyping, MSB could integrate Windows-MCP as an MCP client (via stdio or localhost SSE) to accelerate Windows automation development before implementing native C# equivalents. This is a viable prototyping strategy but should not be the production implementation due to the Python dependency overhead.
