namespace MySecondBrain.Core.Interfaces;

public interface IContentRendererRegistry
{
    void Register(IContentBlockRenderer renderer);
    IReadOnlyList<IContentBlockRenderer> GetRenderers();
    IContentBlockRenderer? Resolve(object? markdownNode);
}
