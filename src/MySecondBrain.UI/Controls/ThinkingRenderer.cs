using System.Windows.Documents;
using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

public class ThinkingRenderer : IContentBlockRenderer
{
    public string RendererName => "Thinking";
    public int Priority => 600;

    public bool CanRender(MarkdownObject markdownNode) => false;

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct) => Task.CompletedTask;
}
