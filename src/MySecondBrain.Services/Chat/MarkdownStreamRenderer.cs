using System.Text;
using System.Windows.Documents;

using Microsoft.Extensions.Logging;

using Markdig;
using Markdig.Syntax;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Core.Utilities;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Receives streaming Markdown tokens from an IAsyncEnumerable and progressively
/// renders them into a WPF FlowDocument by re-parsing the accumulated buffer on
/// each token. Handles partial/incomplete Markdown gracefully (e.g., an unclosed
/// code fence mid-stream) without crashing — parse failures are silently swallowed
/// and retried on the next token.
/// </summary>
public class MarkdownStreamRenderer
{
    private readonly IContentRendererRegistry _registry;
    private readonly ILogger<MarkdownStreamRenderer> _logger;
    private readonly MarkdownPipeline _pipeline;
    private readonly StringBuilder _buffer = new();
    private FlowDocument? _targetDocument;
    private RenderContext? _context;

    public MarkdownStreamRenderer(
        IContentRendererRegistry registry,
        ILogger<MarkdownStreamRenderer> logger)
    {
        _registry = registry;
        _logger = logger;
        _pipeline = MarkdownHelper.Pipeline;
    }

    /// <summary>
    /// Attach (or re-attach) the renderer to a FlowDocument. Clears the
    /// internal text buffer. Call this at the start of a new streaming session.
    /// </summary>
    public void AttachDocument(FlowDocument document, RenderContext? context = null)
    {
        _targetDocument = document;
        _context = context;
        _buffer.Clear();
    }

    /// <summary>
    /// Detach from the current FlowDocument without clearing the buffer.
    /// Useful when the user navigates away mid-stream — the buffer is preserved
    /// on the renderer but no longer written to the UI.
    /// </summary>
    public void DetachDocument()
    {
        _targetDocument = null;
    }

    /// <summary>
    /// The accumulated Markdown text so far. Useful for persisting partial
    /// content when generation is cancelled.
    /// </summary>
    public string AccumulatedText => _buffer.ToString();

    /// <summary>
    /// Whether the renderer is currently attached to a document.
    /// </summary>
    public bool IsAttached => _targetDocument is not null;

    /// <summary>
    /// Append a token (content delta) to the buffer and trigger an
    /// incremental re-render of the FlowDocument.
    /// </summary>
    public void AppendToken(string token)
    {
        _buffer.Append(token);
        IncrementalRender();
    }

    /// <summary>
    /// Re-parse the entire accumulated buffer and rebuild the FlowDocument
    /// from scratch. This is intentionally simple — for long streaming sessions
    /// the re-parse overhead is negligible compared to the LLM response time.
    ///
    /// Partial/incomplete Markdown (e.g., an unclosed code fence mid-stream)
    /// will NOT crash the renderer. Parse failures are swallowed and retried
    /// on the next token.
    /// </summary>
    private void IncrementalRender()
    {
        if (_targetDocument is null) return;

        var markdown = _buffer.ToString();
        if (string.IsNullOrEmpty(markdown)) return;

        try
        {
            var document = Markdown.Parse(markdown, _pipeline);

            // Clear existing blocks and rebuild
            _targetDocument.Blocks.Clear();

            foreach (var block in document)
            {
                var renderer = _registry.Resolve(block);
                if (renderer is not null && _context is not null)
                {
                    renderer.RenderAsync(block, _targetDocument, _context, CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Partial/incomplete Markdown can cause parse failures (e.g., unclosed code fence).
            // Swallow and retry on next token — the buffer will eventually become valid.
            _logger.LogDebug(ex, "Incremental parse failed, will retry on next token. Buffer length: {Length}", markdown.Length);
        }
    }
}
