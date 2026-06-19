# NotifyIcon (WinForms System Tray) — External Documentation

## Platform
Windows System Tray / Notification Area — via `System.Windows.Forms.NotifyIcon`

## Prerequisites
```xml
<!-- In .csproj -->
<UseWindowsForms>true</UseWindowsForms>
```

## Overview
WPF has no native tray icon. `System.Windows.Forms.NotifyIcon` (WinForms interop) provides the system tray icon with context menu support. MySecondBrain uses `UseWindowsForms=true` (already in .csproj) solely for this integration.

## Key API Reference

### Creating a NotifyIcon
```csharp
using System.Windows.Forms;

var notifyIcon = new NotifyIcon
{
    Icon = new Icon("Resources/app.ico"),
    Visible = true,
    Text = "MySecondBrain"
};

// Left-click event
notifyIcon.Click += (s, e) => RestoreMainWindow();

// Double-click event
notifyIcon.DoubleClick += (s, e) => RestoreMainWindow();
```

### Context Menu (ContextMenuStrip)
```csharp
var contextMenu = new ContextMenuStrip();

var openStudioItem = new ToolStripMenuItem("Open Studio");
openStudioItem.Click += (s, e) => OpenStudioRequested?.Invoke(this, EventArgs.Empty);

var newChatItem = new ToolStripMenuItem("New Chat");
var commandBarItem = new ToolStripMenuItem("Command Bar");

// Submenu: Recent Chats
var recentChatsMenu = new ToolStripMenuItem("Recent Chats");
recentChatsMenu.DropDownItems.Add(new ToolStripMenuItem("No recent chats") { Enabled = false });

var settingsItem = new ToolStripMenuItem("Settings");
contextMenu.Items.Add(new ToolStripSeparator());
var exitItem = new ToolStripMenuItem("Exit");

contextMenu.Items.AddRange(new ToolStripItem[]
{
    newChatItem, openStudioItem, commandBarItem,
    new ToolStripSeparator(), recentChatsMenu,
    settingsItem, new ToolStripSeparator(), exitItem
});

notifyIcon.ContextMenuStrip = contextMenu;
```

### Programmatic Icon Generation (Generation Indicator)
```csharp
using System.Drawing;
using System.Drawing.Drawing2D;

private Icon CreateGeneratingIcon(Icon baseIcon)
{
    var bitmap = new Bitmap(32, 32);
    using (var g = Graphics.FromImage(bitmap))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawIcon(baseIcon, 0, 0);
        // Draw green dot overlay in bottom-right
        using (var brush = new SolidBrush(Color.FromArgb(0, 200, 0)))
        {
            g.FillEllipse(brush, 24, 24, 8, 8);
        }
    }
    return Icon.FromHandle(bitmap.GetHicon());
}
```

### Minimize to Tray Pattern
```csharp
// In MainWindow.Closing event:
protected override void OnClosing(CancelEventArgs e)
{
    if (minimizeToTrayEnabled && _systemTrayService.IsVisible)
    {
        e.Cancel = true;
        this.Hide();
        return;
    }
    base.OnClosing(e);
}

// Full exit only via Exit menu item:
exitItem.Click += (s, e) => Application.Current.Shutdown();
```

## MySecondBrain Usage

### WinFormsSystemTrayService (ISystemTrayService)
The `WinFormsSystemTrayService` class (in `UI/Services/WinFormsSystemTrayService.cs`) wraps `NotifyIcon` behind the `ISystemTrayService` interface:

- **Show/Hide**: Toggle `NotifyIcon.Visible`
- **Context Menu**: New Chat, Open Studio, Command Bar, Recent Chats (submenu, up to 5), Settings, Exit
- **Events**: `OpenStudioRequested`, `NewChatRequested`, `CommandBarRequested`, `SettingsRequested`, `ExitRequested`
- **Generation Indicator**: `SetGenerationIndicator(bool)` swaps icon between normal and green-dot variant
- **Recent Chats**: `UpdateRecentChats(IReadOnlyList<string>)` rebuilds submenu (placeholder until Feature 9)

### Important Notes
- `NotifyIcon` must be explicitly disposed — the zombie icon issue on crash is a known Windows behavior
- The icon `.ico` file must be a multi-resolution icon (16x16, 32x32, 48x48, 256x256) for proper rendering at all DPI levels
- `NotifyIcon.Visible = false` hides the icon; `Dispose()` removes it permanently
- WinForms message pump is provided by `UseWindowsForms=true` and the WPF-WinForms interop layer
- The service is a Singleton, created at startup, disposed at shutdown

## Source
Microsoft Docs: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon
