using System.Windows.Documents;
using Markdig.Syntax;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IContentBlockRenderer
{
    string RendererName { get; }
    int Priority { get; }

    bool CanRender(MarkdownObject markdownNode);

    Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct);
}
