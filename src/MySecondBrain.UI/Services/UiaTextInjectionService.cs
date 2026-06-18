using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class UiaTextInjectionService : ITextInjectionService
{
    public Task<TextInjectionResult> InjectAsync(IntPtr targetHwnd, string text, IReadOnlyList<string> availableFormats) =>
        Task.FromResult(new TextInjectionResult(false, "NotImplemented", null));
}
