using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Serilog;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// WPF UserControl wrapping WebView2 for artifact rendering.
/// Provides <see cref="NavigateToArtifact"/> to load artifact files,
/// a theme bridge via <see cref="SetThemeAsync"/>, and graceful fallback
/// to a plain <see cref="TextBlock"/> when the WebView2 runtime is unavailable.
/// </summary>
public class ArtifactsWebView2Host : System.Windows.Controls.UserControl
{
    private readonly WebView2 _webView;
    private readonly TextBlock _fallback;
    private readonly Grid _root;
    private bool _isDarkTheme;
    private bool _webViewReady;

    public ArtifactsWebView2Host()
    {
        _root = new Grid();

        _webView = new WebView2();
        _webView.Visibility = Visibility.Collapsed;

        _fallback = new TextBlock
        {
            Text = "Artifact preview requires the WebView2 runtime.\n" +
                   "Download from: https://go.microsoft.com/fwlink/p/?LinkId=2124703",
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(12),
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };

        _root.Children.Add(_webView);
        _root.Children.Add(_fallback);
        Content = _root;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _webView.EnsureCoreWebView2Async();
            ConfigureWebView2();
            _webViewReady = true;
            _webView.Visibility = Visibility.Visible;
            _fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WebView2 runtime not available — falling back to plain text renderer");
            _webViewReady = false;
            _webView.Visibility = Visibility.Collapsed;
            _fallback.Visibility = Visibility.Visible;
        }
    }

    private void ConfigureWebView2()
    {
        _webView.CoreWebView2.Settings.IsScriptEnabled = true;
        _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
        _webView.CoreWebView2.Settings.IsWebMessageEnabled = false;
        _webView.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;

        // Re-apply theme after each navigation for HTML/SVG/PDF direct navigations
        _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess && _webViewReady)
        {
            _ = SetThemeAsync(_isDarkTheme);
        }
    }

    /// <summary>
    /// Navigates the WebView2 to the specified artifact file.
    /// Supports: HTML, SVG, PDF (browser-native), plus code/Markdown/diffs rendered
    /// via injected JavaScript libraries (Prism.js, marked.js, diff2html.js).
    /// Falls back silently if WebView2 is not available.
    /// </summary>
    public void NavigateToArtifact(string filePath)
    {
        if (!_webViewReady)
            return;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            NavigateToBlank();
            return;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        switch (extension)
        {
            case ".html":
            case ".htm":
            case ".svg":
            case ".pdf":
                // Browser-native — escape # in paths so they aren't treated as URI fragments
                NavigateToFile(filePath);
                break;

            case ".md":
            case ".markdown":
                _ = LoadAndRenderMarkdownAsync(filePath);
                break;

            case ".diff":
            case ".patch":
                _ = LoadAndRenderDiffAsync(filePath);
                break;

            default:
                // Code or plain text — wrap in syntax-highlighted HTML
                _ = RenderCodeContentAsync(filePath, extension.TrimStart('.'));
                break;
        }
    }

    /// <summary>
    /// Loads an empty placeholder page.
    /// </summary>
    public void NavigateToBlank()
    {
        if (!_webViewReady)
            return;

        _webView.CoreWebView2.NavigateToString(
            "<html><body style='background:#1e1e1e; margin:0; display:flex; align-items:center; justify-content:center; height:100vh; font-family:Segoe UI,sans-serif; color:#888; font-size:14px;'>" +
            "<p>No artifact selected</p></body></html>");
    }

    /// <summary>
    /// Toggles the artifact WebView2 between dark and light theme.
    /// Injects a <c>data-theme</c> attribute on the HTML element
    /// and a <c>.dark</c>/<c>.light</c> CSS class for downstream styling.
    /// </summary>
    public async Task SetThemeAsync(bool isDark)
    {
        _isDarkTheme = isDark;

        if (!_webViewReady)
            return;

        var theme = isDark ? "dark" : "light";
        var script = $@"
document.documentElement.setAttribute('data-theme', '{theme}');
document.documentElement.classList.remove('dark', 'light');
document.documentElement.classList.add('{theme}');";
        await _webView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void NavigateToFile(string filePath)
    {
        // Use absolute file URI with proper escaping for special characters like #
        var uri = new Uri($"file:///{filePath.Replace("#", "%23").Replace("?", "%3F").Replace(" ", "%20")}");
        _webView.CoreWebView2.Navigate(uri.AbsoluteUri);
    }

    private async Task LoadAndRenderMarkdownAsync(string filePath)
    {
        var markdown = await File.ReadAllTextAsync(filePath);

        var html = $@"<!DOCTYPE html>
<html data-theme=""{( _isDarkTheme ? "dark" : "light")}"" class=""{( _isDarkTheme ? "dark" : "light")}"">
<head>
<meta charset=""utf-8""/>
<style>
  body {{ font-family:'Segoe UI',sans-serif; font-size:15px; line-height:1.6; padding:16px; max-width:900px; margin:0 auto; }}
  html[data-theme=""dark""]  body {{ background:#1e1e1e; color:#d4d4d4; }}
  html[data-theme=""light""] body {{ background:#fff; color:#333; }}
  pre {{ background:#f5f5f5; padding:12px; border-radius:4px; overflow-x:auto; }}
  html[data-theme=""dark""] pre {{ background:#2d2d2d; }}
  code {{ font-family:'Cascadia Code','Fira Code','Consolas',monospace; font-size:13px; }}
  img {{ max-width:100%; }}
  table {{ border-collapse:collapse; width:100%; }}
  th, td {{ border:1px solid #ccc; padding:6px 10px; text-align:left; }}
  html[data-theme=""dark""] th, html[data-theme=""dark""] td {{ border-color:#444; }}
  blockquote {{ border-left:3px solid #888; margin:1em 0; padding:0 1em; color:#666; }}
  html[data-theme=""dark""] blockquote {{ color:#999; }}
</style>
<script src=""https://cdn.jsdelivr.net/npm/marked/marked.min.js""></script>
</head>
<body>
<div id=""content"">{System.Net.WebUtility.HtmlEncode(markdown)}</div>
<script>
  document.getElementById('content').innerHTML = marked.parse(document.getElementById('content').textContent);
</script>
</body>
</html>";

        _webView.CoreWebView2.NavigateToString(html);
    }

    private async Task LoadAndRenderDiffAsync(string filePath)
    {
        var diffText = await File.ReadAllTextAsync(filePath);

        // Escape for JS string literal — NOT HtmlEncode (which is wrong for <script> context)
        var jsSafe = diffText
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");

        var html = $@"<!DOCTYPE html>
<html data-theme=""{( _isDarkTheme ? "dark" : "light")}"" class=""{( _isDarkTheme ? "dark" : "light")}"">
<head>
<meta charset=""utf-8""/>
<link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/diff2html/bundles/css/diff2html.min.css""/>
<style>
  body {{ margin:0; padding:8px; }}
  html[data-theme=""dark""]  body {{ background:#1e1e1e; }}
  html[data-theme=""light""] body {{ background:#fff; }}
  .d2h-wrapper {{ font-size:12px; }}
</style>
</head>
<body>
<div id=""diff""></div>
<script src=""https://cdn.jsdelivr.net/npm/diff2html/bundles/js/diff2html.min.js""></script>
<script>
  var html = Diff2Html.html('{jsSafe}', {{ inputFormat: 'diff', drawFileList: false, matching: 'lines' }});
  document.getElementById('diff').innerHTML = html;
</script>
</body>
</html>";

        _webView.CoreWebView2.NavigateToString(html);
    }

    private async Task RenderCodeContentAsync(string filePath, string language)
    {
        var code = await File.ReadAllTextAsync(filePath);
        var safeCode = System.Net.WebUtility.HtmlEncode(code);

        var html = $@"<!DOCTYPE html>
<html data-theme=""{( _isDarkTheme ? "dark" : "light")}"" class=""{( _isDarkTheme ? "dark" : "light")}"">
<head>
<meta charset=""utf-8""/>
<link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/prismjs@1/themes/prism-tomorrow.min.css""/>
<style>
  body {{ margin:0; padding:0; }}
  html[data-theme=""light""] body {{ background:#fff; }}
  pre {{ margin:0; padding:16px; overflow-x:auto; }}
  code {{ font-family:'Cascadia Code','Fira Code','Consolas',monospace; font-size:13px; }}
</style>
</head>
<body>
<pre><code class=""language-{language}"">{safeCode}</code></pre>
<script src=""https://cdn.jsdelivr.net/npm/prismjs@1/prism.min.js""></script>
<script src=""https://cdn.jsdelivr.net/npm/prismjs@1/plugins/autoloader/prism-autoloader.min.js""></script>
<script>
  Prism.plugins.autoloader.languages_path = 'https://cdn.jsdelivr.net/npm/prismjs@1/components/';
  Prism.highlightAll();
</script>
</body>
</html>";

        _webView.CoreWebView2.NavigateToString(html);
    }
}
