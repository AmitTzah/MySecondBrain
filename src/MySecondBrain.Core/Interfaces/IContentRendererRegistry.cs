using Markdig.Syntax;

namespace MySecondBrain.Core.Interfaces;

public interface IContentRendererRegistry
{
    void Register(IContentBlockRenderer renderer);
    IReadOnlyList<IContentBlockRenderer> GetRenderers();
    IContentBlockRenderer? Resolve(MarkdownObject markdownNode);
}
