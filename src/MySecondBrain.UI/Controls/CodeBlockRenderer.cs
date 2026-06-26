using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Core.Utilities;

namespace MySecondBrain.UI.Controls;

/// <summary>
/// Renders fenced code blocks with AvalonEdit syntax highlighting.
/// </summary>
public class CodeBlockRenderer : IContentBlockRenderer
{
    public string RendererName => "CodeBlock";
    public int Priority => 200;

    public bool CanRender(MarkdownObject markdownNode) =>
        markdownNode is FencedCodeBlock;

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (markdownNode is not FencedCodeBlock code)
            return Task.CompletedTask;

        var language = code.Info?.Trim() ?? string.Empty;
        var codeText = code.Lines.ToString();

        var container = new Section
        {
            Margin = new Thickness(0, 8, 0, 8),
            FlowDirection = BidiHelper.CodeBlockFlowDirection
        };

        // Header: language label
        var header = new Paragraph
        {
            FontSize = 10,
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
        };
        header.Inlines.Add(new Run(string.IsNullOrEmpty(language) ? "code" : language));
        container.Blocks.Add(header);

        // Code content
        var codeParagraph = new Paragraph
        {
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 13,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0),
            FlowDirection = BidiHelper.CodeBlockFlowDirection
        };

        if (!string.IsNullOrEmpty(codeText))
        {
            var highlighting = HighlightingManager.Instance.GetDefinition(language);

            if (highlighting is not null)
            {
                var document = new ICSharpCode.AvalonEdit.Document.TextDocument(codeText);
                var highlighter = new DocumentHighlighter(document, highlighting);

                foreach (var line in document.Lines)
                {
                    var richText = highlighter.HighlightLine(line.LineNumber);
                    foreach (var section in richText.Sections)
                    {
                        var text = document.GetText(section.Offset, section.Length);
                        var color = section.Color;

                        var run = new Run(text);
                        if (color?.Foreground is not null)
                        {
                            var avalonColor = color.Foreground.GetColor(null);
                            if (avalonColor.HasValue)
                            {
                                run.Foreground = new SolidColorBrush(
                                    System.Windows.Media.Color.FromArgb(
                                        avalonColor.Value.A, avalonColor.Value.R,
                                        avalonColor.Value.G, avalonColor.Value.B));
                            }
                        }

                        codeParagraph.Inlines.Add(run);
                    }
                    codeParagraph.Inlines.Add(new LineBreak());
                }
            }
            else
            {
                codeParagraph.Inlines.Add(new Run(codeText));
            }
        }

        container.Blocks.Add(codeParagraph);
        targetDocument.Blocks.Add(container);
        return Task.CompletedTask;
    }
}
