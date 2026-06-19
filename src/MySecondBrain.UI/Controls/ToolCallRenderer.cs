using System.Windows.Documents;
using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Controls;

public class ToolCallRenderer : IContentBlockRenderer
{
    public string RendererName => "ToolCall";
    public int Priority => 700;

    public bool CanRender(MarkdownObject markdownNode) => false;

    public Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct) => Task.CompletedTask;
}
