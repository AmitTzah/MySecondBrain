using System.Text;
using System.Windows.Documents;

using Microsoft.Extensions.Logging;

using MdXaml;

using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Receives streaming Markdown tokens from an IAsyncEnumerable and progressively
/// renders them into a WPF FlowDocument by re-parsing the accumulated buffer using
/// MdXaml's <see cref="Markdown.Transform"/> on each token.
/// Handles partial/incomplete Markdown gracefully (e.g., an unclosed code fence
/// mid-stream) without crashing — transform failures are silently swallowed and
/// retried on the next token.
/// </summary>
public class MarkdownStreamRenderer
{
    private readonly ILogger<MarkdownStreamRenderer> _logger;
    private readonly Markdown _mdEngine = new();
    private readonly StringBuilder _buffer = new();
    private FlowDocument? _targetDocument;
    private RenderContext? _context;

    public MarkdownStreamRenderer(
        ILogger<MarkdownStreamRenderer> logger)
    {
        _logger = logger;
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
    /// from scratch using MdXaml. This is intentionally simple — for long
    /// streaming sessions the re-parse overhead is negligible compared to
    /// the LLM response time.
    ///
    /// Partial/incomplete Markdown (e.g., an unclosed code fence mid-stream)
    /// will NOT crash the renderer. Transform failures are swallowed and
    /// retried on the next token.
    /// </summary>
    private void IncrementalRender()
    {
        if (_targetDocument is null) return;

        var markdown = _buffer.ToString();
        if (string.IsNullOrEmpty(markdown)) return;

        try
        {
            // MdXaml parses the full markdown and produces a complete FlowDocument.
            // We copy blocks from the fresh document into the target to avoid
            // needing to reassign the Document property (which would unbind it).
            var newDoc = _mdEngine.Transform(markdown);

            _targetDocument.Blocks.Clear();

            // Copy blocks from the new document to the target
            var blocksToCopy = newDoc.Blocks.ToList();
            foreach (var block in blocksToCopy)
            {
                newDoc.Blocks.Remove(block);
                _targetDocument.Blocks.Add(block);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Partial/incomplete Markdown can cause transform failures (e.g.,
            // unclosed code fence). Swallow and retry on next token.
            _logger.LogDebug(ex,
                "Incremental MdXaml transform failed, will retry on next token. Buffer length: {Length}",
                markdown.Length);
        }
    }
}
