using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IHwndCaptureService
{
    HwndCaptureResult CaptureActiveWindow();
    bool IsWindowStillOpen(IntPtr hwnd);
    string? GetWindowTitle(IntPtr hwnd);
    string? GetProcessName(IntPtr hwnd);
}
