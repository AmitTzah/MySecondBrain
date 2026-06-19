# AutoUpdater.NET — External Documentation

## Library ID
/ravibpatel/autoupdater.net

## Package
`Autoupdater.NET.Official` (NuGet, already referenced in MySecondBrain.UI.csproj as Version="1.*")

## Overview
AutoUpdater.NET is a class library for .NET developers to easily add auto-update functionality to their classic desktop applications (WinForms and WPF). It checks a remote XML/JSON feed, compares versions, downloads updates, and triggers installation.

## Key API Reference

### Starting Auto-Update Check
```csharp
// Start auto-update check (typically called from UI thread)
AutoUpdater.Start("https://rbsoft.org/updates/AutoUpdaterTest.xml");

// With FTP credentials
AutoUpdater.Start("ftp://rbsoft.org/updates/AutoUpdaterTest.xml", new NetworkCredential("user", "pass"));
```

### Manual Update Check Pattern
```csharp
AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;

private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
{
    if (args.Error == null)
    {
        if (args.IsUpdateAvailable)
        {
            DialogResult dialogResult;
            if (args.Mandatory.Value)
            {
                dialogResult = MessageBox.Show(
                    $@"There is new version {args.CurrentVersion} available. You are using version {args.InstalledVersion}. This is required update. Press Ok to begin updating the application.",
                    @"Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                dialogResult = MessageBox.Show(
                    $@"There is new version {args.CurrentVersion} available. You are using version {args.InstalledVersion}. Do you want to update the application now?",
                    @"Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            }

            if (dialogResult.Equals(DialogResult.Yes) || dialogResult.Equals(DialogResult.OK))
            {
                try
                {
                    if (AutoUpdater.DownloadUpdate(args))
                    {
                        Application.Exit();
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, exception.GetType().ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        else
        {
            MessageBox.Show(@"There is no update available please try again later.", @"No update available",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    else
    {
        if (args.Error is WebException)
        {
            MessageBox.Show(@"There is a problem reaching update server. Please check your internet connection and try again later.",
                @"Update Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            MessageBox.Show(args.Error.Message, args.Error.GetType().ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
```

### XML Feed Format
```xml
<?xml version="1.0" encoding="UTF-8"?>
<item>
  <version>2.0.0.0</version>
  <url>https://rbsoft.org/downloads/AutoUpdaterTest.zip</url>
  <changelog>https://github.com/ravibpatel/AutoUpdater.NET/releases</changelog>
  <mandatory>false</mandatory>
</item>
```

### JSON Feed Format
```json
{
   "version": "2.0.0.0",
   "url": "https://rbsoft.org/downloads/AutoUpdaterTest.zip",
   "changelog": "https://github.com/ravibpatel/AutoUpdater.NET/releases",
   "mandatory": {
      "value": true,
      "minVersion": "2.0.0.0",
      "mode": 1
   },
   "checksum": {
      "value": "E5F59E50FC91A9E52634FFCB11F32BD37FE0E2F1",
      "hashingAlgorithm": "SHA1"
   }
}
```

## MySecondBrain Usage

### Wrapping Behind IUpdateChecker
AutoUpdater.NET is wrapped behind the `IUpdateChecker` interface (defined in `Core/Interfaces/IUpdateChecker.cs`). This allows swapping to `MsixAppInstallerUpdater` without changing any consumer code.

The `AutoUpdaterDotNet` class (in `Services/Update/AutoUpdaterDotNet.cs`) implements:
- `CheckForUpdatesAsync()`: Fetches XML feed, parses version, compares with `CurrentVersion`
- `DownloadUpdateAsync()`: Downloads MSIX to temp file with progress reporting
- `InstallAsync()`: Launches MSIX installer and triggers app shutdown
- `CurrentVersion`: Reads from `Assembly.GetEntryAssembly().GetName().Version`
- `UpdateFeedUrl`: Hardcoded (configurable via Settings in Feature 8)

### Important Notes
- AutoUpdater.NET requires calling `Start()` on the UI thread (STA)
- The XML feed must be hosted at a publicly accessible HTTPS URL
- For MSIX packages, the `.appinstaller` file provides an alternative auto-update mechanism via Windows App Installer
- The `IUpdateChecker` abstraction has TWO implementations registered in DI: `AutoUpdaterDotNet` (for side-loaded deployments) and `MsixAppInstallerUpdater` (for MSIX-packaged deployments via App Installer)

## Source
GitHub: https://github.com/ravibpatel/AutoUpdater.NET
NuGet: Autoupdater.NET.Official
