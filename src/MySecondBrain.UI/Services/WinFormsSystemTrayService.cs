using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using Application = System.Windows.Application;

namespace MySecondBrain.UI.Services;

public class WinFormsSystemTrayService : ISystemTrayService
{
    private readonly ILogger<WinFormsSystemTrayService> _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _recentChatsMenu;
    private bool _isDisposed;

    public event EventHandler? OpenStudioRequested;
    public event EventHandler? NewChatRequested;
    public event EventHandler? CommandBarRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public bool IsVisible => _notifyIcon.Visible;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static Icon LoadAppIcon(ILogger logger)
    {
        // Try pack URI (works in full WPF runtime)
        try
        {
            var stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/app.ico"))?.Stream;
            if (stream != null)
                return new Icon(stream);
        }
        catch (Exception ex) when (ex is UriFormatException or IOException or InvalidOperationException)
        {
            logger.LogDebug(ex, "Pack URI icon load failed; trying file path");
        }

        // Fallback: load from file path relative to assembly location
        try
        {
            var asmPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var icoPath = Path.Combine(asmPath ?? ".", "Resources", "app.ico");
            if (File.Exists(icoPath))
                return new Icon(icoPath);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "File path icon load failed");
        }

        // Last resort: create a default icon programmatically
        logger.LogWarning("No app.ico found; creating default icon programmatically");
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
            g.FillRectangle(bgBrush, 0, 0, 16, 16);
            using var fgBrush = new SolidBrush(Color.White);
            g.FillRectangle(fgBrush, 3, 4, 3, 9);
            g.FillRectangle(fgBrush, 10, 4, 3, 9);
            g.FillRectangle(fgBrush, 5, 6, 6, 2);
        }
        var hicon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hicon);
            using var ms = new MemoryStream();
            tempIcon.Save(ms);
            ms.Position = 0;
            return new Icon(ms);
        }
        finally
        {
            DestroyIcon(hicon);
        }
    }

    public WinFormsSystemTrayService(ILogger<WinFormsSystemTrayService> logger)
    {
        _logger = logger;

        var icon = LoadAppIcon(logger);
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Visible = false,
            Text = "MySecondBrain"
        };

        _contextMenu = new ContextMenuStrip();

        var newChat = new ToolStripMenuItem("New Chat");
        newChat.Click += (s, e) => NewChatRequested?.Invoke(this, EventArgs.Empty);

        var openStudio = new ToolStripMenuItem("Open Studio");
        openStudio.Click += (s, e) => OpenStudioRequested?.Invoke(this, EventArgs.Empty);

        var commandBar = new ToolStripMenuItem("Command Bar");
        commandBar.Click += (s, e) => CommandBarRequested?.Invoke(this, EventArgs.Empty);

        _recentChatsMenu = new ToolStripMenuItem("Recent Chats");
        _recentChatsMenu.DropDownItems.Add(new ToolStripMenuItem("No recent chats") { Enabled = false });

        var settings = new ToolStripMenuItem("Settings");
        settings.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            newChat,
            openStudio,
            commandBar,
            new ToolStripSeparator(),
            _recentChatsMenu,
            settings,
            new ToolStripSeparator(),
            exit
        });

        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += (s, e) => OpenStudioRequested?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation("WinFormsSystemTrayService initialized");
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
        _logger.LogDebug("System tray icon shown");
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
        _logger.LogDebug("System tray icon hidden");
    }

    public void SetGenerationIndicator(bool isGenerating)
    {
        // Placeholder — full implementation in Step 7
        _logger.LogDebug("SetGenerationIndicator({IsGenerating})", isGenerating);
    }

    public void UpdateRecentChats(IReadOnlyList<string> recentChatTitles)
    {
        _logger.LogDebug("UpdateRecentChats({Count} titles)", recentChatTitles.Count);

        _recentChatsMenu.DropDownItems.Clear();

        if (recentChatTitles.Count == 0)
        {
            _recentChatsMenu.DropDownItems.Add(new ToolStripMenuItem("No recent chats") { Enabled = false });
            return;
        }

        var maxItems = Math.Min(recentChatTitles.Count, 5);
        if (recentChatTitles.Count > 5)
            _logger.LogDebug("Truncated {TotalCount} recent chat titles to 5", recentChatTitles.Count);
        for (int i = 0; i < maxItems; i++)
        {
            var title = recentChatTitles[i];
            var item = new ToolStripMenuItem(title);
            _recentChatsMenu.DropDownItems.Add(item);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        OpenStudioRequested = null;
        NewChatRequested = null;
        CommandBarRequested = null;
        SettingsRequested = null;
        ExitRequested = null;

        // Disconnect context menu from the icon before disposing either
        if (_notifyIcon != null)
        {
            _notifyIcon.ContextMenuStrip = null;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _contextMenu?.Dispose();

        _logger.LogDebug("WinFormsSystemTrayService disposed");
    }
}
