using Markdig.Syntax;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Controls;

public class ContentRendererRegistry : IContentRendererRegistry
{
    private readonly List<IContentBlockRenderer> _renderers = new();

    public ContentRendererRegistry(IEnumerable<IContentBlockRenderer> renderers)
    {
        _renderers.AddRange(renderers);
    }

    public void Register(IContentBlockRenderer renderer)
    {
        _renderers.Add(renderer);
    }

    public IReadOnlyList<IContentBlockRenderer> GetRenderers() => _renderers;

    public IContentBlockRenderer? Resolve(MarkdownObject markdownNode) =>
        _renderers.FirstOrDefault(r => r.CanRender(markdownNode));
}
