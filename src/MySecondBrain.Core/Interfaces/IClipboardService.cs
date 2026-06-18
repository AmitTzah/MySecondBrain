namespace MySecondBrain.Core.Interfaces;

public interface IClipboardService
{
    string? GetText();
    string? GetHtml();
    string? GetRtf();
    IReadOnlyList<string> GetAvailableFormats();
    void SetText(string text);
    void SetHtml(string html);
    void SetRtf(string rtf);
    void SetMultiFormat(Dictionary<string, object> formats);
    bool ContainsImage();
    byte[]? GetImage();
}
