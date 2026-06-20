using System.IO;
using System.Windows.Media.Imaging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Services;

/// <summary>
/// WPF-based clipboard service that uses System.Windows.Clipboard for clipboard operations.
/// Read operations (Get*) are wrapped in try/catch to handle COM/STA threading exceptions
/// gracefully and return fallback values on failure. Write operations (Set*) propagate
/// exceptions to the caller, who is expected to handle them (e.g., the ViewModel's
/// CopyKeyCommand wraps the call in try/catch).
/// </summary>
public class WpfClipboardService : IClipboardService
{
    public string? GetText()
    {
        try { return System.Windows.Clipboard.GetText(); }
        catch { return null; }
    }

    public string? GetHtml()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Html))
                return System.Windows.Clipboard.GetData(System.Windows.DataFormats.Html) as string;
            return null;
        }
        catch { return null; }
    }

    public string? GetRtf()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Rtf))
                return System.Windows.Clipboard.GetData(System.Windows.DataFormats.Rtf) as string;
            return null;
        }
        catch { return null; }
    }

    public IReadOnlyList<string> GetAvailableFormats()
    {
        try
        {
            return System.Windows.Clipboard.GetDataObject()?.GetFormats() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SetText(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        System.Windows.Clipboard.SetText(text);
    }

    public void SetHtml(string html)
    {
        if (html is null)
            throw new ArgumentNullException(nameof(html));

        System.Windows.Clipboard.SetData(System.Windows.DataFormats.Html, html);
    }

    public void SetRtf(string rtf)
    {
        if (rtf is null)
            throw new ArgumentNullException(nameof(rtf));

        System.Windows.Clipboard.SetData(System.Windows.DataFormats.Rtf, rtf);
    }

    public void SetMultiFormat(Dictionary<string, object> formats)
    {
        if (formats is null)
            throw new ArgumentNullException(nameof(formats));

        var dataObject = new System.Windows.DataObject();
        foreach (var (format, data) in formats)
        {
            dataObject.SetData(format, data);
        }
        System.Windows.Clipboard.SetDataObject(dataObject);
    }

    public bool ContainsImage()
    {
        try { return System.Windows.Clipboard.ContainsImage(); }
        catch { return false; }
    }

    public byte[]? GetImage()
    {
        try
        {
            var image = System.Windows.Clipboard.GetImage();
            if (image is null) return null;

            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch { return null; }
    }
}
