using Markdig;
using Markdig.Syntax;

namespace MySecondBrain.Core.Utilities;

/// <summary>
/// Shared Markdig pipeline configuration used by the streaming renderer
/// and any other component that needs to parse Markdown.
/// </summary>
public static class MarkdownHelper
{
    private static readonly Lazy<MarkdownPipeline> PipelineLazy = new(() =>
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // tables, footnotes, task lists, footers, figures, etc.
            .UseEmojiAndSmiley()
            .UseAutoLinks()
            .UseBootstrap()
            .Build());

    /// <summary>
    /// The shared Markdig pipeline instance. Built once, cached for the app lifetime.
    /// </summary>
    public static MarkdownPipeline Pipeline => PipelineLazy.Value;

    /// <summary>
    /// Parses Markdown text into a Markdig AST document.
    /// </summary>
    public static MarkdownDocument Parse(string markdown) =>
        Markdown.Parse(markdown, Pipeline);

    /// <summary>
    /// Converts Markdown to HTML using the shared pipeline.
    /// </summary>
    public static string ToHtml(string markdown) =>
        Markdown.ToHtml(markdown, Pipeline);
}
