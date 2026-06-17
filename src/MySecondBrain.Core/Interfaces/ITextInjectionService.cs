using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ITextInjectionService
{
    Task<TextInjectionResult> InjectAsync(IntPtr targetHwnd, string text, IReadOnlyList<string> availableFormats);
}
