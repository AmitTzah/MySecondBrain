# Markdig — External Documentation Reference

## Library
- **NuGet:** `Markdig` (latest stable)
- **Source:** https://github.com/xoofx/markdig
- **License:** BSD-2-Clause
- **Purpose:** Fast, CommonMark-compliant, extensible Markdown processor for .NET

## Key APIs for WPF FlowDocument Rendering

### Parsing Markdown to AST
```csharp
using Markdig;
using Markdig.Syntax;

// Parse markdown string to AST
MarkdownDocument document = Markdown.Parse(markdownText);

// With pipeline extensions
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()  // Tables, footnotes, task lists, etc.
    .UseEmojiAndSmiley()
    .UseAutoLinks()
    .Build();
MarkdownDocument document = Markdown.Parse(markdownText, pipeline);
```

### AST Node Types (Relevant to Rendering)

**Container Blocks (have child blocks):**
- `MarkdownDocument` — root node
- `ListBlock` — bulleted/numbered list (children: ListItemBlock)
- `ListItemBlock` — single list item (children: ParagraphBlock, etc.)
- `QuoteBlock` — blockquote

**Leaf Blocks (have inline content, no child blocks):**
- `ParagraphBlock` — regular paragraph, has `.Inline` property (ContainerInline)
- `HeadingBlock` — heading, has `.Level` (1-6) and `.Inline`
- `FencedCodeBlock` — code fence, has `.Info` (language), `.Lines` (StringLineGroup)
- `CodeBlock` — indented code block
- `ThematicBreakBlock` — horizontal rule `<hr>`
- `HtmlBlock` — raw HTML

**Inlines (inside LeafBlock.Inline):**
- `LiteralInline` — plain text
- `EmphasisInline` — bold/italic (`.DelimiterChar`, `.IsDouble`)
- `CodeInline` — inline code
- `LinkInline` — hyperlink (`.Url`, `.Title`)
- `LineBreakInline` — soft/hard line break

### Traversing the AST
```csharp
// Iterate all blocks
foreach (var block in document)
{
    // block is a Block (ParagraphBlock, HeadingBlock, etc.)
}

// Find specific node types
foreach (var heading in document.Descendants<HeadingBlock>())
{
    Console.WriteLine($"H{heading.Level}: {heading.Inline?.FirstChild}");
}

// Get all fenced code blocks
foreach (var code in document.Descendants<FencedCodeBlock>())
{
    string language = code.Info;  // e.g., "csharp", "python"
    string content = code.Lines.ToString();
}
```

### Converting to HTML (for Copy Rich)
```csharp
string html = Markdown.ToHtml(markdownText, pipeline);
```

### Extensions Available
- `UseAdvancedExtensions()` — enables: Abbreviations, AutoIdentifiers, AutoLinks, Citations, CustomContainers, DefinitionLists, Emoji, EmphasisExtras, Figures, Footnotes, GridTables, ListExtras, Mathematics, MediaLinks, PipeTables, SmartyPants, TaskLists, and more
- Individual: `.UsePipeTables()`, `.UseTaskLists()`, `.UseFootnotes()`, `.UseEmojiAndSmiley()`, `.UseAutoLinks()`, `.UseBootstrap()`

## WPF FlowDocument Rendering Strategy

1. Parse Markdown → `MarkdownDocument` AST
2. Iterate `document` blocks
3. For each block, map to WPF `Block` element:
   - `ParagraphBlock` → `Paragraph` with `Run`/`Bold`/`Italic`/`Hyperlink` inlines
   - `HeadingBlock` → `Paragraph` with scaled `FontSize` + `FontWeight`
   - `FencedCodeBlock` → `Section` with monospace `Paragraph` + AvalonEdit highlighting
   - `ListBlock` → WPF `List` with `ListItem` children
   - `QuoteBlock` → `Section` with left border
   - `ThematicBreakBlock` → `Paragraph` with `BorderBrush` line
4. Add to `FlowDocument.Blocks`

### Progressive Rendering (Streaming)
During streaming, re-parse the accumulated text buffer on each token:
```csharp
_buffer.Append(token);
var document = Markdown.Parse(_buffer.ToString(), pipeline);
targetFlowDocument.Blocks.Clear();
foreach (var block in document)
{
    // Render block to FlowDocument
}
```

Performance note: `Markdown.Parse()` is fast enough for progressive re-parsing on each token at typical streaming speeds (<100 tokens/sec). For very large documents, throttle re-render to every 50ms.

## Research Date: 2026-06-26
