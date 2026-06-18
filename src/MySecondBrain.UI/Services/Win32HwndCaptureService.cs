using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class Win32HwndCaptureService : IHwndCaptureService
{
    public HwndCaptureResult CaptureActiveWindow() =>
        new(IntPtr.Zero, null, null, null);

    public bool IsWindowStillOpen(IntPtr hwnd) => false;

    public string? GetWindowTitle(IntPtr hwnd) => null;

    public string? GetProcessName(IntPtr hwnd) => null;
}
