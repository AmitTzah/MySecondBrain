using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Services;

public class WpfClipboardService : IClipboardService
{
    public string? GetText() => null;

    public string? GetHtml() => null;

    public string? GetRtf() => null;

    public IReadOnlyList<string> GetAvailableFormats() => Array.Empty<string>();

    public void SetText(string text) { }

    public void SetHtml(string html) { }

    public void SetRtf(string rtf) { }

    public void SetMultiFormat(Dictionary<string, object> formats) { }

    public bool ContainsImage() => false;

    public byte[]? GetImage() => null;
}
