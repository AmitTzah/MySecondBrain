# Feature Reference: Studio Chat — Core Workspace

## Global & Shared Documentation

### Core Interfaces (Already Defined)
All interfaces referenced below are defined in `MySecondBrain.Core/Interfaces/`:
- `IChatThreadService` — central chat operations (CreateThread, SendMessage, Regenerate, etc.)
- `IChatThreadRepository` — ChatThread CRUD + FTS5 search
- `IMessageRepository` — Message CRUD + branch navigation (recursive CTE)
- `ILLMProviderService` — LLM streaming wrapper (ChatStreamAsync)
- `IContentBlockRenderer` — Markdig AST → WPF FlowDocument
- `IContentRendererRegistry` — renderer resolution by priority
- `IThemeProvider` — dark/light + chat theme management
- `IChatEncryptionService` — AES-256-GCM for locked chats
- `IClipboardService` — clipboard read/write with multi-format support
- `IConfirmationService` — mockable dialog abstraction
- `ISettingsRepository` — key-value persistence for all settings

### Shared Domain Models
- `ChatThread` (`Core/Models/DomainModels.cs`) — conversation container entity
- `Message` (`Core/Models/DomainModels.cs`) — single message with branching attributes
- `Persona` — AI behavior preset
- `ModelConfiguration` — engine configuration
- `ChatTabItem` (NEW — `Core/Models/ChatTabItem.cs`) — ObservableObject wrapper for tab state
- `StreamRenderState` (NEW — `Core/Models/StreamRenderState.cs`) — streaming state DTO

### Shared Utilities
- `BidiHelper` (NEW — `Core/Utilities/BidiHelper.cs`) — RTL detection
- `MarkdownHelper` (NEW — `Core/Utilities/MarkdownHelper.cs`) — Markdig pipeline config

### DI Registration Convention
All services register in [`DependencyInjectionConfig.cs`](src/MySecondBrain.UI/DependencyInjectionConfig.cs):
```csharp
// Singleton services
services.AddSingleton<INewService, NewServiceImpl>();

// Transient ViewModels
services.AddTransient<NewViewModel>();

// Multi-implementation (auto-discovered via IEnumerable<T>)
services.AddSingleton<IContentBlockRenderer, NewRenderer>();
```

### MVVM Pattern
```csharp
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty] private string _myProperty;
    [RelayCommand] private async Task MyCommandAsync() { ... }
}
```

### XAML Binding Pattern
```xml
<Button Content="Send" Command="{Binding SendMessageCommand}"
        AutomationProperties.AutomationId="SendMessageBtn"/>
```

### E2E Test Pattern
```csharp
[Collection("E2E")]
public class StudioChatE2ETests : E2eTestBase
{
    public StudioChatE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }
    
    [Fact]
    public async Task TestName_ShouldExpectedBehavior()
    {
        await UseSharedAppAsync();
        // ARRANGE → ACT → ASSERT → CLEANUP → ASSERT
    }
}
```

### Key NuGet Packages
| Package | Version | Purpose |
|---------|---------|---------|
| `Markdig` | `*` (latest stable) | Markdown parsing → AST |
| `AvalonEdit` | `*` (latest stable) | Syntax highlighting engine (HighlightingManager) |
| `CommunityToolkit.Mvvm` | `8.*` | MVVM source generators |
| `Microsoft.EntityFrameworkCore.Sqlite` | `8.0.*` | SQLite ORM |
| `Serilog` | `4.*` | Structured logging |

---

## Step-Specific Documentation

### Step 1: Fill Repository Stubs — ChatThread & Message Data Access

- **Library:** Entity Framework Core 8.x (`Microsoft.EntityFrameworkCore.Sqlite`)
- **Import:** `Microsoft.EntityFrameworkCore`, `Microsoft.Data.Sqlite`
- **Key Pattern — Recursive CTE for Active Branch:**
```csharp
// In MessageRepository.GetActiveBranchAsync
var messages = await _db.Messages
    .FromSqlRaw(@"
        WITH RECURSIVE active_chain AS (
            SELECT * FROM Messages 
            WHERE ThreadId = {0} AND ParentMessageId IS NULL AND IsActiveBranch = 1
            UNION ALL
            SELECT m.* FROM Messages m
            JOIN active_chain a ON m.ParentMessageId = a.Id
            WHERE m.IsActiveBranch = 1
        )
        SELECT * FROM active_chain ORDER BY CreatedAt",
        threadId)
    .AsNoTracking()
    .ToListAsync();
```

- **Key Pattern — FTS5 Search:**
```csharp
var results = await _db.Messages
    .FromSqlRaw(@"
        SELECT m.* FROM Messages m
        INNER JOIN MessageFts fts ON m.rowid = fts.rowid
        WHERE MessageFts MATCH {0}
        ORDER BY rank LIMIT {1}",
        query, maxResults)
    .AsNoTracking()
    .ToListAsync();
```

- **Entity Fields to Add (ChatThread):**
```csharp
public bool IsFavorite { get; set; }
public bool IsPinned { get; set; }
public bool IsArchived { get; set; }
public string? ColorLabel { get; set; }
public string? Tags { get; set; } // JSON array as string
public string? FolderId { get; set; }
public bool IsLocked { get; set; }
public string? LockSalt { get; set; } // Base64
public string? LockNonce { get; set; } // Base64
```

- **Entity Fields to Add (Message):**
```csharp
public bool IsFavorited { get; set; }
public string? ThinkingContent { get; set; }
```

- **Migration Command:**
```powershell
dotnet ef migrations add AddChatOrganizationFields --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI
```

- **Snippet — Repository CreateAsync Pattern:**
```csharp
public async Task<ChatThread> CreateAsync(ChatThread thread)
{
    _db.ChatThreads.Add(thread);
    await _db.SaveChangesAsync();
    return thread;
}
```

- **Snippet — Repository SoftDeleteAsync:**
```csharp
public async Task SoftDeleteAsync(string id)
{
    var thread = await _db.ChatThreads.FindAsync(id);
    if (thread is null) return;
    thread.IsDeleted = true;
    thread.DeletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync();
}
```

- **Snippet — GetAllPermanentAsync with Sorting:**
```csharp
public async Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort)
{
    var query = _db.ChatThreads
        .Where(t => !t.IsTransient && !t.IsDeleted);
    
    query = sort switch
    {
        ChatSortOrder.MostRecent => query.OrderByDescending(t => t.LastActivityAt),
        ChatSortOrder.Name => query.OrderBy(t => t.Title),
        ChatSortOrder.DateCreated => query.OrderByDescending(t => t.CreatedAt),
        ChatSortOrder.LastActivity => query.OrderByDescending(t => t.LastActivityAt),
        _ => query.OrderByDescending(t => t.LastActivityAt)
    };
    
    return await query.AsNoTracking().ToListAsync();
}
```

---

### Step 2: Fill ChatThreadService — Core Chat Operations with LLM Integration

- **Library:** `ILLMProviderService` (already registered in DI), `Markdig` (for MarkdownStreamRenderer)
- **Import:** `MySecondBrain.Core.Interfaces`, `MySecondBrain.Core.Models`, `System.Runtime.CompilerServices`
- **Key Pattern — SendMessageAsync Full Lifecycle:**
```csharp
public async Task<Message> SendMessageAsync(string threadId, string content, CancellationToken ct)
{
    // 1. Create user message
    var userMsg = new Message
    {
        Id = Guid.NewGuid().ToString("N"),
        ThreadId = threadId,
        Role = "User",
        Content = content,
        CreatedAt = DateTimeOffset.UtcNow,
        VersionNumber = 1,
        BranchId = Guid.NewGuid().ToString("N"),
        IsActiveBranch = true
    };
    await _messageRepo.CreateAsync(userMsg);
    
    // 2. Build context
    var thread = await _threadRepo.GetByIdAsync(threadId)
        ?? throw new InvalidOperationException("Thread not found");
    var persona = /* resolve from thread.PersonaId */;
    var modelConfig = /* resolve from persona.DefaultModelConfigId */;
    var history = await _messageRepo.GetActiveBranchAsync(threadId);
    
    // 3. Call LLM (streaming)
    var assistantMsg = new Message { /* ... */ };
    var responseBuilder = new StringBuilder();
    var stopwatch = Stopwatch.StartNew();
    
    await foreach (var chunk in _llmService.ChatStreamAsync(thread, content, persona, modelConfig, tools, ct))
    {
        if (chunk.ContentDelta is not null)
            responseBuilder.Append(chunk.ContentDelta);
        // Stream chunks to UI renderer via callback/event
        OnStreamChunk?.Invoke(chunk);
    }
    
    stopwatch.Stop();
    
    // 4. Persist assistant message
    assistantMsg.Content = responseBuilder.ToString();
    assistantMsg.GenerationTimeMs = (long)stopwatch.Elapsed.TotalMilliseconds;
    // ... token counts, cost, etc.
    await _messageRepo.CreateAsync(assistantMsg);
    
    // 5. Update thread
    thread.LastActivityAt = DateTimeOffset.UtcNow;
    await _threadRepo.UpdateAsync(thread);
    
    return assistantMsg;
}
```

- **Key Pattern — MarkdownStreamRenderer:**
```csharp
public class MarkdownStreamRenderer
{
    private readonly IContentRendererRegistry _registry;
    private readonly MarkdownPipeline _pipeline;
    private readonly StringBuilder _buffer = new();
    private FlowDocument? _targetDocument;
    
    public MarkdownStreamRenderer(IContentRendererRegistry registry)
    {
        _registry = registry;
        _pipeline = MarkdownHelper.CreatePipeline(); // from MarkdownHelper utility
    }
    
    public void AttachDocument(FlowDocument document)
    {
        _targetDocument = document;
        _buffer.Clear();
    }
    
    public void AppendToken(string token)
    {
        _buffer.Append(token);
        IncrementalRender();
    }
    
    private void IncrementalRender()
    {
        if (_targetDocument is null) return;
        var markdown = _buffer.ToString();
        
        try
        {
            var document = Markdown.Parse(markdown, _pipeline);
            
            _targetDocument.Blocks.Clear();
            foreach (var block in document)
            {
                var renderer = _registry.Resolve(block);
                renderer?.RenderAsync(block, _targetDocument, _context, CancellationToken.None);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Partial/incomplete Markdown can cause parse failures (e.g., unclosed code fence).
            // Swallow and retry on next token — the buffer will eventually become valid.
            // Log at Debug level for diagnostics: _logger.LogDebug(ex, "Incremental parse failed, will retry");
        }
    }
}
```

- **Key Pattern — ChatTitleGenerator:**
```csharp
public class ChatTitleGenerator
{
    private readonly ILLMProviderService _llmService;
    
    public async Task<string> GenerateTitleAsync(
        string userMessage, string assistantResponse, 
        Persona persona, ModelConfiguration config, CancellationToken ct)
    {
        try
        {
            var prompt = $"Generate a concise 3-7 word title for this conversation:\n\nUser: {userMessage}\n\nAssistant: {assistantResponse}\n\nTitle:";
            var response = await _llmService.ChatAsync(/* lightweight call */);
            var title = response.Content.Trim().Trim('"');
            return string.IsNullOrEmpty(title) || title.Length > 100
                ? userMessage[..Math.Min(50, userMessage.Length)]
                : title;
        }
        catch
        {
            return userMessage[..Math.Min(50, userMessage.Length)];
        }
    }
}
```

- **DI Registration:**
```csharp
services.AddSingleton<MarkdownStreamRenderer>();
services.AddSingleton<ChatTitleGenerator>();
```

---

### Step 3: ChatThreadViewModel — Multi-Tab Architecture & Chat State Management

- **Library:** `CommunityToolkit.Mvvm` (source generators), `System.Collections.ObjectModel`
- **Import:** `MySecondBrain.UI.ViewModels`, `CommunityToolkit.Mvvm.ComponentModel`, `CommunityToolkit.Mvvm.Input`
- **Key Pattern — ChatTabItem:**
```csharp
public partial class ChatTabItem : ObservableObject
{
    public ChatThread Thread { get; }
    
    [ObservableProperty] private ObservableCollection<Message> _messages = new();
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string _textboxContent = string.Empty;
    [ObservableProperty] private int _cursorPosition;
    [ObservableProperty] private double _scrollOffset;
    [ObservableProperty] private bool _hasCompletionAlert;
    
    public ChatTabItem(ChatThread thread) { Thread = thread; }
}
```

- **Key Pattern — NewChat Command:**
```csharp
[RelayCommand]
private async Task NewChatAsync()
{
    var persona = ActivePersona ?? _personas.FirstOrDefault();
    if (persona is null) return;
    
    var thread = await _chatService.CreateThreadAsync(null, false, persona);
    var tab = new ChatTabItem(thread);
    ChatTabs.Add(tab);
    ActiveTab = tab;
}
```

- **Key Pattern — SendMessage Command:**
```csharp
[RelayCommand]
private async Task SendMessageAsync()
{
    if (ActiveTab is null || string.IsNullOrWhiteSpace(ActiveTab.TextboxContent)) return;
    
    var content = ActiveTab.TextboxContent;
    ActiveTab.TextboxContent = string.Empty;
    ActiveTab.IsStreaming = true;
    
    try
    {
        using var cts = new CancellationTokenSource();
        _activeCts = cts;
        
        var message = await _chatService.SendMessageAsync(
            ActiveTab.Thread.Id, content, cts.Token);
        ActiveTab.Messages.Add(message);
    }
    catch (OperationCanceledException) { /* partial preserved */ }
    catch (Exception ex) { /* error handling */ }
    finally
    {
        ActiveTab.IsStreaming = false;
        _activeCts = null;
    }
}
```

- **Key Pattern — Cross-Tab Alert via Messenger:**
```csharp
// In constructor:
WeakReferenceMessenger.Default.Register<GenerationCompletedMessage>(this, (r, m) =>
{
    var tab = ChatTabs.FirstOrDefault(t => t.Thread.Id == m.Value.ThreadId);
    if (tab is not null && tab != ActiveTab)
        tab.HasCompletionAlert = true;
});
```

- **Keyboard Shortcut Wiring (MainWindow.xaml.cs):**
```csharp
protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
    {
        switch (e.Key)
        {
            case Key.N: _viewModel.NewChatCommand.Execute(null); e.Handled = true; break;
            case Key.W: _viewModel.CloseTabCommand.Execute(_viewModel.ActiveTab); e.Handled = true; break;
            case Key.T when e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                _viewModel.ReopenTabCommand.Execute(null); e.Handled = true; break;
        }
    }
    base.OnKeyDown(e);
}
```

---

### Step 4: Conversation View — VirtualizingStackPanel + Markdown Rendering Engine

- **Library:** `Markdig` (NuGet, already referenced), `AvalonEdit` (NuGet, `ICSharpCode.AvalonEdit.Highlighting`)
- **Import:** `Markdig`, `Markdig.Syntax`, `Markdig.Extensions.Tables`, `ICSharpCode.AvalonEdit.Highlighting`
- **Key Pattern — Markdig Pipeline Configuration:**
```csharp
public static class MarkdownHelper
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()  // tables, footnotes, task lists, etc.
        .UseEmojiAndSmiley()
        .UseAutoLinks()
        .UseBootstrap()  // adds CSS classes (useful for WPF styling)
        .Build();
    
    public static MarkdownDocument Parse(string markdown) =>
        Markdown.Parse(markdown, Pipeline);
    
    public static string ToHtml(string markdown) =>
        Markdown.ToHtml(markdown, Pipeline);
}
```

- **Key Pattern — MarkdownTextRenderer (Headings):**
```csharp
public bool CanRender(MarkdownObject node) => node is HeadingBlock;

public async Task RenderAsync(MarkdownObject node, FlowDocument target, RenderContext ctx, CancellationToken ct)
{
    var heading = (HeadingBlock)node;
    var fontSize = heading.Level switch
    {
        1 => 24, 2 => 20, 3 => 16, 4 => 14, 5 => 12, 6 => 11, _ => 14
    };
    var paragraph = new Paragraph
    {
        FontSize = fontSize,
        FontWeight = heading.Level <= 2 ? FontWeights.Bold : FontWeights.SemiBold,
        Margin = new Thickness(0, 8, 0, 4)
    };
    foreach (var inline in heading.Inline)
    {
        if (inline is LiteralInline lit)
            paragraph.Inlines.Add(new Run(lit.Content.ToString()));
    }
    target.Blocks.Add(paragraph);
    await Task.CompletedTask;
}
```

- **Key Pattern — CodeBlockRenderer (AvalonEdit Highlighting):**
```csharp
public bool CanRender(MarkdownObject node) => node is FencedCodeBlock;

public async Task RenderAsync(MarkdownObject node, FlowDocument target, RenderContext ctx, CancellationToken ct)
{
    var code = (FencedCodeBlock)node;
    var language = code.Info?.Trim() ?? "";
    var highlighting = HighlightingManager.Instance.GetDefinition(language);
    
    var container = new Section { Margin = new Thickness(0, 8, 0, 8) };
    
    // Header: language label + copy button
    var header = new Paragraph { FontSize = 10, Foreground = Brushes.Gray };
    header.Inlines.Add(new Run(language ?? "plain text"));
    container.Blocks.Add(header);
    
    // Code content
    var codeParagraph = new Paragraph
    {
        FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
        FontSize = 13,
        Background = /* theme-aware background */,
        Padding = new Thickness(12, 8, 12, 8)
    };
    
    if (highlighting is not null)
    {
        // Apply syntax colors
        var document = new TextDocument(code.Lines.ToString());
        var highlighter = new DocumentHighlighter(document, highlighting);
        // Iterate highlighted lines, create colorized Runs
        foreach (var line in document.Lines)
        {
            var richText = highlighter.HighlightLine(line.LineNumber);
            foreach (var section in richText)
            {
                codeParagraph.Inlines.Add(new Run(document.GetText(section.Offset, section.Length))
                {
                    Foreground = new SolidColorBrush(/* AvalonEdit color → WPF brush */)
                });
            }
            codeParagraph.Inlines.Add(new LineBreak());
        }
    }
    else
    {
        codeParagraph.Inlines.Add(new Run(code.Lines.ToString()));
    }
    
    container.Blocks.Add(codeParagraph);
    target.Blocks.Add(container);
    await Task.CompletedTask;
}
```

- **Key Pattern — VirtualizingStackPanel with DataTemplateSelector:**
```xml
<ListBox ItemsSource="{Binding ActiveTab.Messages}"
         VirtualizingStackPanel.IsVirtualizing="True"
         VirtualizingStackPanel.VirtualizationMode="Recycling"
         ItemTemplateSelector="{StaticResource MessageTemplateSelector}">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel/>
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="Focusable" Value="False"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
        </Style>
    </ListBox.ItemContainerStyle>
</ListBox>
```

- **Key Pattern — Relative Timestamp:**
```csharp
public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dt) return "";
        var diff = DateTimeOffset.UtcNow - dt;
        return diff.TotalSeconds switch
        {
            < 60 => "just now",
            < 3600 => $"{(int)diff.TotalMinutes} min ago",
            < 86400 => $"{(int)diff.TotalHours}h ago",
            < 172800 => "Yesterday",
            < 604800 => $"{(int)diff.TotalDays}d ago",
            _ => dt.ToString("MMM dd")
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

---

### Step 5: Streaming Response Display + Auto-Scroll + Message Actions + Error Handling

- **Library:** `System.Runtime.CompilerServices` (IAsyncEnumerable), `System.Windows.Clipboard`
- **Import:** `System.Windows.Controls`, `System.Windows.Documents`, `System.Windows.Threading`
- **Key Pattern — Send/Stop Button Binding:**
```xml
<Button Command="{Binding IsStreaming ? StopGenerationCommand : SendMessageCommand}">
    <Button.Style>
        <Style TargetType="Button">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsStreaming}" Value="True">
                    <Setter Property="Content" Value="⬛ Stop"/>
                    <Setter Property="Background" Value="{DynamicResource ErrorBrush}"/>
                </DataTrigger>
                <DataTrigger Binding="{Binding IsStreaming}" Value="False">
                    <Setter Property="Content" Value="Send"/>
                    <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

- **Key Pattern — Stop with Partial Preservation:**
```csharp
[RelayCommand]
private void StopGeneration()
{
    _activeCts?.Cancel();
    // The catch block in SendMessageAsync handles OperationCanceledException
    // and preserves whatever content was accumulated so far
}
```

- **Key Pattern — Auto-Scroll Detection:**
```csharp
private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
{
    if (sender is not ScrollViewer sv) return;
    var isAtBottom = sv.VerticalOffset >= sv.ScrollableHeight - 10;
    _isAutoScrolling = isAtBottom || !ActiveTab.IsStreaming;
    
    if (!_isAutoScrolling && ActiveTab.IsStreaming)
        AutoScrollPausedIndicator.Visibility = Visibility.Visible;
}
```

- **Key Pattern — Scroll-to-Bottom Floating Button:**
```xml
<Button x:Name="ScrollToBottomBtn" Content="↓"
        Visibility="{Binding IsScrolledUp, Converter={StaticResource BoolToVisibilityConverter}}"
        Style="{StaticResource FloatingButtonStyle}"
        HorizontalAlignment="Right" VerticalAlignment="Bottom"
        Margin="0,0,20,20"
        Click="ScrollToBottomBtn_Click"/>
```

- **Key Pattern — Copy Rich:**
```csharp
[RelayCommand]
private void CopyRich(Message message)
{
    var html = Markdown.ToHtml(message.Content);
    var dataObject = new DataObject();
    dataObject.SetData(DataFormats.Html, html);
    dataObject.SetData(DataFormats.Text, message.Content);
    // RTF conversion if needed
    Clipboard.SetDataObject(dataObject);
}
```

- **Key Pattern — Error Banner:**
```xml
<Border Background="{DynamicResource ErrorBrush}" CornerRadius="4" Padding="8"
        Visibility="{Binding HasError, Converter={StaticResource BoolToVisibilityConverter}}">
    <StackPanel>
        <TextBlock Text="{Binding ErrorMessage}" Foreground="White"/>
        <Button Content="Retry" Command="{Binding RetryCommand}" Margin="0,4,0,0"/>
    </StackPanel>
</Border>
```

- **TokenContextBar — Colored Fill:**
```xml
<Border x:Name="ContextBar" Height="6" CornerRadius="3" Width="200">
    <Border.Background>
        <SolidColorBrush Color="{Binding ContextPercentage, Converter={StaticResource TokenCountToColorConverter}}"/>
    </Border.Background>
    <Border.Width>
        <MultiBinding Converter="{StaticResource PercentageToWidthConverter}">
            <Binding Path="ContextPercentage"/>
            <Binding ElementName="ContextBar" Path="Width"/>
        </MultiBinding>
    </Border.Width>
</Border>
```

---

### Step 6: Chat Header Full Layout + Chat Modes + RTL + Controls

- **Library:** WPF `FlowDocument.BidiAlgorithm`, `System.Globalization.UnicodeCategory`
- **Import:** `System.Windows.Documents`, `System.Windows.Controls.Primitives`, `System.Globalization`
- **Key Pattern — BidiHelper:**
```csharp
public static class BidiHelper
{
    public static FlowDirection GetMessageFlowDirection(string? content)
    {
        if (string.IsNullOrEmpty(content)) return FlowDirection.LeftToRight;
        
        int hebrewChars = 0, totalLetters = 0;
        foreach (var c in content)
        {
            if (char.IsLetter(c))
            {
                totalLetters++;
                if (c >= 0x0590 && c <= 0x05FF) hebrewChars++;
            }
        }
        
        if (totalLetters == 0) return FlowDirection.LeftToRight;
        return (double)hebrewChars / totalLetters > 0.3
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
    
    public static FlowDirection CodeBlockFlowDirection => FlowDirection.LeftToRight;
}
```

- **Key Pattern — ChatHeaderBar Layout Structure:**
```xml
<Border Background="{DynamicResource HeaderBackground}" Padding="12,8">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <!-- Left: Persona | Theme | API History | Context Bar | Cost | Source Banner -->
        <StackPanel Grid.Column="0" Orientation="Horizontal">
            <Button Content="{Binding ActivePersona.DisplayName}" .../>
            <ComboBox ItemsSource="{Binding ChatThemeOptions}" .../>
            <Button Content="📡" ToolTip="API History" .../>
            <local:TokenContextBar .../>
            <TextBlock Text="{Binding CumulativeCost, StringFormat='${0:F2} total'}" .../>
            <Border x:Name="SourceBanner" Visibility="{Binding HasSourceContext, Converter=...}">
                <TextBlock Text="{Binding SourceContextText}"/>
                <Button Content="[Apply Latest]" IsEnabled="False" 
                        ToolTip="Text action integration coming in a future update"/>
            </Border>
        </StackPanel>
        <!-- Right: Font Size | Dark Mode | Pin | Help | ... -->
        <StackPanel Grid.Column="1" Orientation="Horizontal">
            <Button Content="A⁻" Command="{Binding DecreaseFontCommand}"/>
            <TextBlock Text="{Binding FontSizeDisplay}"/>
            <Button Content="A⁺" Command="{Binding IncreaseFontCommand}"/>
            <Button Content="{Binding ThemeToggleIcon}" Command="{Binding ToggleThemeCommand}"/>
            <Button Content="📌" Command="{Binding TogglePinCommand}"/>
            <Button Content="?" Command="{Binding ShowHelpCommand}"/>
            <Button Content="⋯" Command="{Binding ShowThreeDotMenuCommand}"/>
        </StackPanel>
    </Grid>
</Border>
```

- **Key Pattern — System Message Popover (E5):**
```xml
<Popup IsOpen="{Binding IsSystemMessageEditorOpen}" StaysOpen="False"
       PlacementTarget="{Binding ElementName=PersonaNameBtn}">
    <Border Background="{DynamicResource ContentBackground}" 
            BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
            Width="400" Padding="12">
        <StackPanel>
            <TextBlock Text="System Message" FontWeight="Bold" Margin="0,0,0,8"/>
            <TextBox Text="{Binding EditingSystemMessage}" 
                     AcceptsReturn="True" Height="150" TextWrapping="Wrap"/>
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
                <Button Content="Reset to Persona Default" 
                        Command="{Binding ResetSystemMessageCommand}"/>
                <Button Content="Save" Command="{Binding SaveSystemMessageCommand}" Margin="8,0,0,0"
                        Style="{StaticResource AccentButtonStyle}"/>
            </StackPanel>
        </StackPanel>
    </Border>
</Popup>
```

- **Key Pattern — Three-Dot Menu (ContextMenu):**
```xml
<Button Content="⋯" Click="ThreeDotMenu_Click">
    <Button.ContextMenu>
        <ContextMenu>
            <MenuItem Header="Clear Conversation" Command="{Binding ClearConversationCommand}"/>
            <MenuItem Header="Export Chat" Command="{Binding ExportChatCommand}"/>
            <MenuItem Header="Duplicate Chat" Command="{Binding DuplicateChatCommand}"/>
            <MenuItem Header="Chat Tree" Command="{Binding ShowChatTreeCommand}"/>
            <MenuItem Header="Edit System Message" Command="{Binding EditSystemMessageCommand}"/>
            <MenuItem Header="Summarize Chat" Command="{Binding SummarizeChatCommand}"/>
            <Separator/>
            <MenuItem Header="Make Temporary" Command="{Binding ToggleTemporaryCommand}"/>
        </ContextMenu>
    </Button.ContextMenu>
</Button>
```

- **Key Pattern — FlowDirection Binding in Message Template:**
```xml
<FlowDocumentScrollViewer FlowDirection="{Binding Content, Converter={StaticResource FlowDirectionConverter}}">
    <FlowDocument x:Name="MessageDocument"/>
</FlowDocumentScrollViewer>
```

- **Pin Window Toggle:**
```csharp
[RelayCommand]
private void TogglePinWindow()
{
    IsPinned = !IsPinned;
    Application.Current.MainWindow.Topmost = IsPinned;
    _settingsRepo.SetAsync("PinWindow", IsPinned ? "true" : "false");
}
```

---

### Step 7: QoL Features — File Viewer Tabs, Incognito, Locked Chats, Titling, Favoriting, Cross-Tab Alerts, Message Selection, Right Panel

- **Library:** `System.Security.Cryptography.AesGcm` (.NET 8+), `System.Net.NetworkInformation`
- **Import:** `System.Security.Cryptography`, `System.IO`, `System.Net.NetworkInformation`
- **Key Pattern — LockedChatService:**
```csharp
public class LockedChatService
{
    private readonly IChatEncryptionService _encryption;
    private readonly IMessageRepository _messageRepo;
    
    public async Task LockChatAsync(string threadId, string password, CancellationToken ct)
    {
        var salt = _encryption.GenerateSalt();
        var messages = await _messageRepo.GetActiveBranchAsync(threadId);
        
        foreach (var msg in messages)
        {
            var plaintext = Encoding.UTF8.GetBytes(msg.Content);
            var ciphertext = _encryption.Encrypt(plaintext, password, salt);
            msg.Content = Convert.ToBase64String(ciphertext);
            await _messageRepo.UpdateAsync(msg);
        }
        
        // Mark thread as locked with salt
        var thread = await _threadRepo.GetByIdAsync(threadId);
        thread.IsLocked = true;
        thread.LockSalt = Convert.ToBase64String(salt);
        await _threadRepo.UpdateAsync(thread);
    }
    
    public async Task UnlockChatAsync(string threadId, string password, CancellationToken ct)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        var salt = Convert.FromBase64String(thread.LockSalt);
        var messages = await _messageRepo.GetActiveBranchAsync(threadId);
        
        // CRITICAL: Validate password on the FIRST message before decrypting all.
        // Decrypting all messages with a wrong password corrupts the entire chat.
        if (messages.Count > 0)
        {
            try
            {
                var firstCiphertext = Convert.FromBase64String(messages[0].Content);
                _encryption.Decrypt(firstCiphertext, password, salt); // throws on wrong password
            }
            catch (CryptographicException)
            {
                throw new UnauthorizedAccessException("Incorrect password.");
            }
        }
        
        foreach (var msg in messages)
        {
            var ciphertext = Convert.FromBase64String(msg.Content);
            var plaintext = _encryption.Decrypt(ciphertext, password, salt);
            msg.Content = Encoding.UTF8.GetString(plaintext);
            await _messageRepo.UpdateAsync(msg);
        }
        
        thread.IsLocked = false;
        thread.LockSalt = null;
        await _threadRepo.UpdateAsync(thread);
    }
}
```

- **Key Pattern — FileViewerTabViewModel:**
```csharp
public partial class FileViewerTabViewModel : ObservableObject
{
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileContent = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private FileViewerType _fileType;
    [ObservableProperty] private bool _isReadOnly = true;
    
    public static async Task<FileViewerTabViewModel> FromFileAsync(string filePath)
    {
        var vm = new FileViewerTabViewModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FileType = DetermineType(filePath)
        };
        
        if (vm.FileType == FileViewerType.Image)
        {
            // Load image bytes, not text
        }
        else
        {
            vm.FileContent = await File.ReadAllTextAsync(filePath);
        }
        return vm;
    }
    
    private static FileViewerType DetermineType(string path) => Path.GetExtension(path).ToLower() switch
    {
        ".md" => FileViewerType.Markdown,
        ".png" or ".jpg" or ".gif" or ".webp" => FileViewerType.Image,
        ".cs" or ".py" or ".js" or ".ts" or ".json" or ".xml" or ".yaml" or ".html" 
            or ".css" or ".java" or ".cpp" or ".rs" or ".go" or ".rb" => FileViewerType.Code,
        _ => FileViewerType.Text
    };
}
```

- **Key Pattern — Offline Detection:**
```csharp
public partial class ChatThreadViewModel : ObservableObject
{
    [ObservableProperty] private NetworkStatus _networkStatus = NetworkStatus.Online;
    
    public ChatThreadViewModel(/*...*/)
    {
        NetworkChange.NetworkAvailabilityChanged += (s, e) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                NetworkStatus = e.IsAvailable ? NetworkStatus.Online : NetworkStatus.Offline;
            });
        };
    }
}
```

- **Key Pattern — Cross-Tab Alert (Green Dot):**
```xml
<!-- Tab header template -->
<StackPanel Orientation="Horizontal">
    <TextBlock Text="{Binding Thread.Title}"/>
    <Ellipse Width="8" Height="8" Fill="LimeGreen" Margin="4,0,0,0"
             Visibility="{Binding HasCompletionAlert, Converter={StaticResource BoolToVisibilityConverter}}">
        <Ellipse.Style>
            <Style TargetType="Ellipse">
                <Style.Triggers>
                    <DataTrigger Binding="{Binding HasCompletionAlert}" Value="True">
                        <DataTrigger.EnterActions>
                            <BeginStoryboard>
                                <Storyboard RepeatBehavior="Forever">
                                    <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                                     From="1.0" To="0.3" Duration="0:0:0.8"
                                                     AutoReverse="True"/>
                                </Storyboard>
                            </BeginStoryboard>
                        </DataTrigger.EnterActions>
                    </DataTrigger>
                </Style.Triggers>
            </Style>
        </Ellipse.Style>
    </Ellipse>
</StackPanel>
```

- **Key Pattern — Message Selection Mode:**
```csharp
[ObservableProperty] private bool _isSelectionMode;
[ObservableProperty] private ObservableCollection<Message> _selectedMessages = new();

[RelayCommand]
private void ToggleMessageSelection(Message message)
{
    if (SelectedMessages.Contains(message))
        SelectedMessages.Remove(message);
    else
        SelectedMessages.Add(message);
}

[RelayCommand]
private void DeleteSelectedMessages()
{
    foreach (var msg in SelectedMessages.ToList())
    {
        _chatService.DeleteMessageAsync(msg.Id);
        ActiveTab.Messages.Remove(msg);
    }
    SelectedMessages.Clear();
    IsSelectionMode = false;
}
```

- **Key Pattern — Close Confirmation:**
```csharp
// In MainWindow.xaml.cs
protected override async void OnClosing(CancelEventArgs e)
{
    var vm = DataContext as MainWindowViewModel;
    if (vm?.ChatThreadViewModel?.ChatTabs.Any(t => t.IsStreaming) == true)
    {
        var confirm = await _confirmationService.ConfirmAsync(
            "Generation in Progress",
            "A response is still being generated. Are you sure you want to close?");
        if (!confirm)
        {
            e.Cancel = true;
            return;
        }
    }
    base.OnClosing(e);
}
```

- **DI Registration:**
```csharp
services.AddSingleton<LockedChatService>();
services.AddTransient<FileViewerTabViewModel>();
```

---

### Step 8: E2E Tests + Integration Tests + Visual Polish

- **Library:** `FlaUI.UIA3`, `xUnit` (already in test projects)
- **Import:** `FlaUI.Core`, `FlaUI.UIA3`, `Xunit`
- **Key Pattern — E2E Test for New Chat:**
```csharp
[Fact]
public async Task CreateNewChat_ShouldShowEmptyConversationView()
{
    await UseSharedAppAsync();
    
    // Click + New Chat
    var newChatBtn = FindById("NewChatBtn");
    Assert.NotNull(newChatBtn);
    newChatBtn!.Click();
    await Task.Delay(500);
    
    // Verify Persona picker appears
    var personaPicker = FindById("PersonaPickerDialog", timeout: TimeSpan.FromSeconds(3));
    Assert.NotNull(personaPicker);
    
    // Select first persona
    var selectBtn = FindById("PersonaPickerSelectBtn");
    selectBtn!.Click();
    await Task.Delay(500);
    
    // Verify chat view is visible with empty state
    var chatView = FindById("ChatView");
    Assert.NotNull(chatView);
    
    _output.WriteLine("AC-1 PASSED: New chat created and displayed.");
}
```

- **Key Pattern — E2E Test for Message Sending:**
```csharp
[Fact]
public async Task SendMessage_ShouldDisplayUserMessage()
{
    await UseSharedAppAsync();
    
    // Type in textbox
    var textbox = FindById("MessageInput");
    textbox!.AsTextBox().Text = "Hello, test message!";
    await Task.Delay(200);
    
    // Click Send
    var sendBtn = FindById("SendMessageBtn");
    sendBtn!.Click();
    await Task.Delay(2000); // Wait for response
    
    // Verify message appears
    var userMsg = FindByNameContains("Hello, test message!");
    Assert.NotNull(userMsg);
    
    _output.WriteLine("AC-2 PASSED: Message sent and displayed.");
}
```

- **New AutomationIds to Add:**
  - `NewChatBtn`, `SendMessageBtn`, `StopGenerationBtn`, `MessageInput`
  - `ChatTab_{index}`, `CloseTabBtn`, `ReopenTabBtn`
  - `CopyMDBtn`, `CopyRichBtn`, `RegenerateBtn`, `ContinueBtn`
  - `ScrollToBottomBtn`, `AutoScrollPaused`
  - `ThreeDotMenuBtn`, `ClearConversationItem`, `ExportChatItem`
  - `PersonaNameBtn`, `SystemMessageEditor`
  - `ThinkingToggleBtn`, `MuteToggleBtn`
  - `ThemeToggleBtn`, `PinToggleBtn`, `HelpBtn`
  - `FileViewerTab`, `FileViewerContent`
  - `LockChatBtn`, `LockPasswordInput`, `LockConfirmBtn`
  - `FavoriteBtn`, `SelectMessagesBtn`, `BulkActionBar`
  - `OfflineBanner`, `NetworkStatusDot`

- **Visual Polish Checklist:**
  - Message spacing and padding match HTML mock
  - Code block copy button appears on hover only
  - Scroll-to-bottom button uses smooth animation
  - RTL messages: text aligns right, padding mirrors LTR
  - Empty states have centered text with muted color
  - Loading skeletons: alternating gray bars with subtle animation
  - Error banners: red left border, white background
  - All three chat themes render correctly (Classic/Compact/Bubble)
  - Font size changes apply instantly to all message text
  - Dark/Light mode transition is smooth (no flicker)

- **Test Commands:**
```powershell
# Run all tests
dotnet test MySecondBrain.sln --configuration Debug

# Run E2E only
dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug --verbosity normal

# Run unit tests only
dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug

# Run integration tests only
dotnet test tests/integration/MySecondBrain.Tests.Integration --configuration Debug

# Run specific E2E test class
dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug --filter "FullyQualifiedName~StudioChatE2ETests"
```
