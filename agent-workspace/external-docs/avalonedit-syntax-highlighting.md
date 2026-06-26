# AvalonEdit — External Documentation Reference

## Library
- **NuGet:** `AvalonEdit` (latest stable)
- **Source:** https://github.com/icsharpcode/AvalonEdit
- **License:** MIT
- **Purpose:** WPF-based text editor component with syntax highlighting engine

## Key APIs for Code Block Rendering (Without Full Editor)

### Syntax Highlighting Manager
```csharp
using ICSharpCode.AvalonEdit.Highlighting;

// Get the global highlighting manager
HighlightingManager manager = HighlightingManager.Instance;

// List all available highlighting definitions (100+ languages built-in)
foreach (var definition in manager.HighlightingDefinitions)
{
    Console.WriteLine(definition.Name);
}

// Get highlighting by name
IHighlightingDefinition csharpHighlighting = manager.GetDefinition("C#");

// Get highlighting by file extension
IHighlightingDefinition jsHighlighting = manager.GetDefinitionByExtension(".js");
```

### Applying Syntax Colors WITHOUT TextEditor Control
For rendering code blocks in a FlowDocument (not in a TextEditor), use `DocumentHighlighter`:

```csharp
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

// Create a TextDocument from code text
var textDocument = new TextDocument(codeText);

// Get highlighting definition
IHighlightingDefinition highlighting = HighlightingManager.Instance.GetDefinition(language);

if (highlighting != null)
{
    // Create highlighter
    var highlighter = new DocumentHighlighter(textDocument, highlighting);
    
    // Highlight line by line
    for (int line = 1; line <= textDocument.LineCount; line++)
    {
        var documentLine = textDocument.GetLineByNumber(line);
        var highlightedLine = highlighter.HighlightLine(line);
        
        // highlightedLine is RichText with sections
        // Each section has: Offset, Length, Color
        foreach (var section in highlightedLine.Sections)
        {
            string segmentText = textDocument.GetText(section.Offset, section.Length);
            // section.Color is HighlightingColor with Foreground/Background
            // Convert to WPF Brush:
            // new SolidColorBrush(Color.FromArgb(section.Color.Foreground.A, ...))
        }
    }
}
```

### Loading Custom Syntax Definitions (XSHD Files)
```csharp
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Xml;

// Load custom highlighting from XSHD file
IHighlightingDefinition customHighlighting;
using (Stream stream = File.OpenRead("CustomLanguage.xshd"))
using (XmlTextReader reader = new XmlTextReader(stream))
{
    customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
}
manager.RegisterHighlighting("CustomLanguage", new[] { ".custom" }, customHighlighting);
```

### Available Built-in Languages (Partial List)
C#, VB.NET, C++, Java, JavaScript, TypeScript, Python, HTML, XML, CSS, PHP, SQL, PowerShell, F#, Rust, Go, Ruby, Markdown, JSON, YAML, Dockerfile, INI, Diff, Batch, PowerShell, and many more — 100+ languages total.

### Converting HighlightingColor to WPF Brush
```csharp
using System.Windows.Media;

public static Brush ToWpfBrush(HighlightingColor? color)
{
    if (color?.Foreground == null) return Brushes.Transparent;
    var c = color.Foreground.Value;
    return new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
}
```

### HighlightingColor Properties
- `Name` — named color (e.g., "Comment", "String", "Keyword")
- `Foreground` — optional `SimpleHighlightingBrush` with Color
- `Background` — optional background color
- `FontWeight` — optional font weight
- `FontStyle` — optional font style

## FlowDocument Code Block Rendering Strategy

1. Detect fenced code block from Markdig AST
2. Extract language from `FencedCodeBlock.Info` (e.g., "csharp")
3. Resolve `IHighlightingDefinition` from `HighlightingManager.Instance.GetDefinition(language)`
4. Create `TextDocument` from code text
5. Use `DocumentHighlighter` to get per-line `RichText`
6. For each line, iterate `HighlightedLine.Sections`:
   - Get text segment via `textDocument.GetText(section.Offset, section.Length)`
   - Convert `section.Color.Foreground` to WPF `SolidColorBrush`
   - Create `Run` with text + brush, add to `Paragraph`
7. Wrap in `Section` with code block styling (monospace font, background, padding, copy button)

## Research Date: 2026-06-26
