using System.Windows.Documents;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IContentBlockRenderer
{
    string RendererName { get; }
    int Priority { get; }

    bool CanRender(object? markdownNode);

    Task RenderAsync(
        object? markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct);
}
