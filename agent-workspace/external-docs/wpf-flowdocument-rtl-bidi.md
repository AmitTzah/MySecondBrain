# WPF FlowDocument RTL/BiDi — External Documentation Reference

## Platform
- **Technology:** WPF FlowDocument + Unicode Bidirectional Algorithm
- **.NET Version:** .NET 8.0
- **Purpose:** Right-to-left (Hebrew) text rendering in chat messages

## Key APIs

### Per-Element FlowDirection
```csharp
using System.Windows;
using System.Windows.Documents;

// Set FlowDirection on FlowDocument
var doc = new FlowDocument();
doc.FlowDirection = FlowDirection.RightToLeft;

// Set on individual Paragraph
var paragraph = new Paragraph();
paragraph.FlowDirection = FlowDirection.RightToLeft;

// Set on individual Run/Span for mixed LTR/RTL
var rtlRun = new Run("שלום") { FlowDirection = FlowDirection.RightToLeft };
var ltrRun = new Run("hello") { FlowDirection = FlowDirection.LeftToRight };
```

### Hebrew Unicode Range
- **Range:** U+0590–U+05FF (Hebrew block)
- **Includes:** Letters, niqqud (vowel points), cantillation marks, Yiddish digraphs
- **Also:** U+FB1D–U+FB4F (Hebrew presentation forms, including wide letters)

### RTL Detection Algorithm (Vision Spec Q2)
```csharp
public static FlowDirection GetMessageFlowDirection(string content)
{
    if (string.IsNullOrEmpty(content)) return FlowDirection.LeftToRight;
    
    int hebrewChars = 0, totalLetters = 0;
    foreach (var c in content)
    {
        if (char.IsLetter(c))
        {
            totalLetters++;
            // Hebrew range: U+0590–U+05FF
            if (c >= 0x0590 && c <= 0x05FF) hebrewChars++;
        }
    }
    
    if (totalLetters == 0) return FlowDirection.LeftToRight;
    
    // >30% Hebrew characters → RTL (vision spec threshold)
    return (double)hebrewChars / totalLetters > 0.3
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;
}
```

### Code Block Enforcement
Code blocks must ALWAYS be LTR regardless of content:
```csharp
// In CodeBlockRenderer:
var codeParagraph = new Paragraph();
codeParagraph.FlowDirection = FlowDirection.LeftToRight;
codeParagraph.FontFamily = new FontFamily("Cascadia Code, Consolas, monospace");
```

### WPF FlowDocument Bidirectional Algorithm
WPF's `FlowDocument` natively implements the Unicode Bidirectional Algorithm (UBA) per the Unicode Standard. When `FlowDirection` is set at the document or paragraph level, WPF automatically handles mixed LTR/RTL text within a single paragraph. For explicit segment-level control, individual `Run` elements can have their own `FlowDirection`.

### Known Limitations
1. **Rich text input with bidirectional text:** WPF `RichTextBox` supports BiDi but cursor navigation in mixed text can behave unexpectedly
2. **Numbers in RTL text:** By default, numbers in RTL paragraphs render LTR per UBA rules. This is usually correct (e.g., "המחיר הוא 100 ₪" — the number 100 stays LTR)
3. **Code blocks:** Always enforce LTR. Hebrew characters in code blocks (e.g., Hebrew variable names) should still render LTR to maintain code alignment

## Textbox Input Direction
Auto-detect input direction based on first strong directional character typed:
```csharp
private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
{
    var textBox = (TextBox)sender;
    var text = textBox.Text;
    if (string.IsNullOrEmpty(text)) return;
    
    var firstChar = text[0];
    textBox.FlowDirection = (firstChar >= 0x0590 && firstChar <= 0x05FF)
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;
}
```

## Testing Strategy
- Pure English message → verify LTR rendering
- Pure Hebrew message → verify RTL rendering
- Mixed message (60% Hebrew, 40% English) → verify RTL container
- Mixed message (20% Hebrew, 80% English) → verify LTR container
- Code block with Hebrew content → verify LTR (code alignment preserved)
- Message with emoji + Hebrew → verify emoji doesn't affect detection (>30% Hebrew letters, not total chars)

## Research Date: 2026-06-26
