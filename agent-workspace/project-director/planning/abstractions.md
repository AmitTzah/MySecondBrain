# Abstractions — Provider Interfaces & Contracts

Every integration point from [`tech-sourcing.md`](../tech-sourcing.md) is captured as a C# interface. Each interface defines the contract that concrete implementations must fulfill. The pattern: **Build [interface] with [key methods]; implement [concrete] as first [provider/implementation].**

---

## 1. LLM Provider Abstraction

### `ILLMProvider`
The core abstraction for all AI language model providers. Normalizes streaming chunks, errors, and model metadata across OpenAI, Anthropic, Google, and OpenAI-compatible endpoints.

```csharp
public interface ILLMProvider
{
    // Provider identity
    string ProviderName { get; }
    ProviderType Type { get; }  // OpenAI, Anthropic, Google, OpenAICompatible

    // Core chat — non-streaming
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct);

    // Core chat — streaming (SSE token-by-token)
    IAsyncEnumerable<StreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken ct);

    // Model discovery
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct);

    // API key validation
    Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct);
}

// Normalized streaming chunk (all providers map to this)
public record StreamChunk(
    string? ContentDelta,           // Text content delta (null for non-text chunks)
    IReadOnlyList<ToolCallDelta>? ToolCalls,  // Incremental tool call data
    string? ThinkingDelta,          // Thinking/reasoning tokens (null if not present)
    string? FinishReason,           // "stop", "length", "tool_calls", "error"
    UsageInfo? Usage,               // Token usage (usually on final chunk)
    bool IsFinal                    // True on last chunk
);

// Request DTO
public record ChatRequest(
    IReadOnlyList<ChatMessage> Messages,  // Conversation history
    ModelConfiguration ModelConfig,       // Model, temperature, max tokens, thinking
    IReadOnlyList<ToolDefinition>? Tools, // Function-calling tool definitions
    string? SystemMessage                 // Override system prompt
);

// Response DTO (non-streaming)
public record ChatResponse(
    string Content,
    IReadOnlyList<ToolCall>? ToolCalls,
    string? ThinkingContent,
    string FinishReason,
    UsageInfo Usage
);

public record ChatMessage(string Role, string Content);  // "user", "assistant", "system"
public record ToolDefinition(string Name, string Description, string ParametersJsonSchema);
public record ToolCallDelta(int Index, string? Id, string? Name, string? ArgumentsDelta);
public record ToolCall(string Id, string Name, string Arguments);
public record UsageInfo(int PromptTokens, int CompletionTokens, int TotalTokens);
public record ModelInfo(string Id, string DisplayName, int MaxContextTokens);
```

**Concrete implementations:**

| Implementation | Adapts | SDK / HTTP Client | Notes |
|---------------|--------|-------------------|-------|
| `OpenAIProvider` | OpenAI, DeepSeek, Mistral, any OpenAI-compatible | `OpenAI` NuGet SDK | One implementation covers ~70% of providers |
| `AnthropicProvider` | Anthropic Claude | `Anthropic.SDK` NuGet | Normalizes Messages API to common `StreamChunk` |
| `GoogleProvider` | Google Gemini | `Google.Cloud.AIPlatform.V1` | Normalizes Gemini streaming format |
| `OpenAICompatibleProvider` | Any OpenAI-API-compatible service (inc. local models) | `HttpClient` + `System.Text.Json` | User configures endpoint URL + optional API key |

**Ref:** [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client)

---

### `ILLMProviderFactory`
Resolves the correct `ILLMProvider` implementation at runtime based on the user's `ModelConfiguration`.

```csharp
public interface ILLMProviderFactory
{
    ILLMProvider GetProvider(ProviderType type, string? endpointUrl = null);
    IReadOnlyList<ProviderType> SupportedProviders { get; }
}
```

**Implementation:** `LLMProviderFactory` — registry of provider adapters keyed by `ProviderType`.

---

## 2. Speech-to-Text Provider

### `ISTTProvider`
Abstraction for voice dictation. Supports cloud (OpenAI Whisper API) and local (Whisper.net) implementations.

```csharp
public interface ISTTProvider
{
    string ProviderName { get; }
    STTProviderType Type { get; }  // OpenAI, LocalWhisper, OpenAICompatible

    // Transcribe audio data to text
    Task<STTResult> TranscribeAsync(byte[] audioData, string audioFormat, CancellationToken ct);

    // Streaming transcription (for real-time dictation)
    IAsyncEnumerable<string> TranscribeStreamAsync(Stream audioStream, string audioFormat, CancellationToken ct);

    // Check if provider is available/configured
    Task<bool> IsAvailableAsync(CancellationToken ct);
}

public record STTResult(string Text, string? Language, double? Confidence);
```

**Concrete implementations:**

| Implementation | Backend | Notes |
|---------------|---------|-------|
| `OpenAIWhisperProvider` | OpenAI Whisper API via OpenAI SDK | Requires API key. Cloud-based, highest accuracy. |
| `LocalWhisperProvider` | Whisper.net (Whisper.cpp binding) | Runs entirely on-device. Model download on first use (~1-4GB). |
| `WindowsSpeechProvider` | `System.Speech` (Windows built-in) | Free, zero-config, lower accuracy. Third option for zero-cost dictation. |

**Ref:** [tech-sourcing #21](../tech-sourcing.md#21-speech-to-text-stt)

---

## 3. Backup Provider

### `IBackupProvider`
Abstraction for off-site (and local) backup of SQLite DB, wiki `.md` files, and artifacts.

```csharp
public interface IBackupProvider
{
    string ProviderName { get; }
    BackupProviderType Type { get; }  // GoogleCloudStorage, LocalFolder

    // Upload a backup archive
    Task<BackupResult> UploadAsync(Stream backupData, string backupName, CancellationToken ct);

    // List available backups
    Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken ct);

    // Download a specific backup
    Task<Stream> DownloadAsync(string backupId, CancellationToken ct);

    // Delete a backup
    Task DeleteAsync(string backupId, CancellationToken ct);

    // Check provider connectivity/credentials
    Task<bool> ValidateCredentialsAsync(CancellationToken ct);
}

public record BackupResult(string BackupId, DateTimeOffset Timestamp, long SizeBytes);
public record BackupInfo(string BackupId, DateTimeOffset Timestamp, long SizeBytes, string Name);
```

**Concrete implementations:**

| Implementation | Backend | Notes |
|---------------|---------|-------|
| `GcsBackupProvider` | Google Cloud Storage via `Google.Cloud.Storage.V1` SDK | Requires GCS bucket + service account key (encrypted via DPAPI). |
| `LocalFolderBackupProvider` | User-specified local folder path | Zero-dependency fallback. Simple file copy. |

**Ref:** [tech-sourcing #26](../tech-sourcing.md#26-backup-to-google-cloud-storage)

---

## 4. Web Search Provider

### `ISearchProvider`
Abstraction for web search used by AI tool-use (H1) and Deep Research (H6).

```csharp
public interface ISearchProvider
{
    string ProviderName { get; }
    SearchProviderType Type { get; }  // GoogleCustomSearch, Bing

    // Execute a web search query
    Task<SearchResults> SearchAsync(string query, int maxResults, CancellationToken ct);

    // Check if provider is configured and available
    Task<bool> IsAvailableAsync(CancellationToken ct);
}

public record SearchResults(
    string Query,
    IReadOnlyList<SearchResultItem> Items,
    long TotalEstimatedResults
);

public record SearchResultItem(
    string Title,
    string Url,
    string Snippet,
    string? DisplayUrl
);
```

**Concrete implementations:**

| Implementation | API | Notes |
|---------------|-----|-------|
| `GoogleCustomSearchProvider` | Google Custom Search API | User's own API key + Search Engine ID. Free tier: 100 queries/day. |
| `BingSearchProvider` | Bing Web Search API | User's own API key. Azure marketplace. |

**Ref:** [tech-sourcing #16](../tech-sourcing.md#16-web-search-integration-tool-use-h1)

---

## 5. Tokenizer

### `ITokenizer`
Abstraction for real-time token counting. Different models use different tokenizers — this interface enables per-model selection.

```csharp
public interface ITokenizer
{
    string TokenizerName { get; }

    // Count tokens in text
    int CountTokens(string text);

    // Encode text to token IDs
    IReadOnlyList<int> Encode(string text);

    // Decode token IDs back to text
    string Decode(IReadOnlyList<int> tokens);

    // Get the maximum context window for this tokenizer's model
    int MaxContextTokens { get; }

    // Check if this tokenizer is appropriate for the given model
    bool SupportsModel(string modelId);
}
```

### `ITokenizerFactory`
Resolves the correct tokenizer for a given model.

```csharp
public interface ITokenizerFactory
{
    ITokenizer GetTokenizer(string modelId, ProviderType provider);
    ITokenizer GetFallbackTokenizer();  // Character-count approximation (chars/4)
}
```

**Concrete implementations:**

| Implementation | Backend | Models Supported |
|---------------|---------|-----------------|
| `SharpTokenTokenizer` | SharpToken (C# port of tiktoken) | All OpenAI models (GPT-4, GPT-4o, GPT-3.5, etc.) |
| `AnthropicTokenizer` | Anthropic tokenizer (community port) | Claude models |
| `FallbackTokenizer` | Character count / 4 | Any model (approximation, off by 30-40%) |

**Ref:** [tech-sourcing #10](../tech-sourcing.md#10-local-tokenization-real-time-token-counting)

---

## 6. Chat Import Parser

### `IChatImporter`
Abstraction for parsing exported chat history from external platforms and converting to the internal `ChatThread` + `Message` data model.

```csharp
public interface IChatImporter
{
    string FormatName { get; }       // "ChatGPT", "Claude"
    string[] SupportedFileExtensions { get; }  // [".json"]

    // Parse a file and return imported chat threads
    Task<ImportResult> ImportAsync(string filePath, CancellationToken ct);

    // Validate file is parseable without full import
    Task<ImportValidationResult> ValidateAsync(string filePath, CancellationToken ct);
}

public record ImportResult(
    IReadOnlyList<ImportedChatThread> Threads,
    IReadOnlyList<ImportWarning> Warnings
);

public record ImportedChatThread(
    string Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ImportedMessage> Messages,
    string? SourceSystemMessage  // ChatGPT custom GPT instructions → system message
);

public record ImportedMessage(
    string Role,       // "user", "assistant", "system"
    string Content,
    DateTimeOffset Timestamp,
    string? ModelName
);

public record ImportWarning(string Message, string? Detail);
public record ImportValidationResult(bool IsValid, string? ErrorMessage, int EstimatedMessageCount);
```

**Concrete implementations:**

| Implementation | Format | Parser |
|---------------|--------|--------|
| `ChatGPTImporter` | ChatGPT export JSON | `System.Text.Json` — well-documented format |
| `ClaudeImporter` | Claude export JSON | `System.Text.Json` — format-specific adaptation |

**Ref:** [tech-sourcing #29](../tech-sourcing.md#29-chat-import-parsing-chatgpt--claude)

---

## 7. Tool Executor

### `IToolExecutor`
Abstraction for individual tool execution within the Tool Use Orchestrator. Each tool type has its own executor implementation.

```csharp
public interface IToolExecutor
{
    string ToolName { get; }                                          // "web_search", "terminal", "file_generate", "file_edit", "wiki_search"
    bool RequiresUserConfirmation { get; }                            // Terminal: always true; others: configurable
    ToolRiskLevel RiskLevel { get; }                                  // Low, Medium, High
    bool CanAutoApprove { get; }                                      // False for terminal (H2 override)

    // Validate tool parameters before execution
    Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct);

    // Execute the tool
    Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct);

    // Get a user-facing description of what will happen (for confirmation dialog)
    string GetConfirmationDescription(ToolCall toolCall);
}

public record ToolValidationResult(bool IsValid, string? ErrorMessage, ToolRiskLevel AssessedRisk);
public record ToolResult(bool Success, string Content, string? ErrorMessage);
public enum ToolRiskLevel { Low, Medium, High }
```

**Concrete implementations:**

| Implementation | Tool | Execution Mechanism | Risk |
|---------------|------|---------------------|------|
| `WebSearchToolExecutor` | `web_search` | Google/Bing API via `ISearchProvider` | Low |
| `TerminalToolExecutor` | `terminal` | `System.Diagnostics.Process` | High (always requires confirmation) |
| `FileGenerateToolExecutor` | `file_generate` | `System.IO.File.WriteAllText` + save dialog | Low-Medium |
| `FileEditToolExecutor` | `file_edit` | `System.IO` + DiffPlex diff preview | Low-Medium |
| `WikiSearchToolExecutor` | `wiki_search` | Local SQLite FTS5 query | Low (read-only) |

**Ref:** [tech-sourcing #16-19](../tech-sourcing.md#16-web-search-integration-tool-use-h1)

---

### `IToolOrchestrator`
Manages the function-calling loop: AI requests tool → validate → confirm → execute → feed result back.

```csharp
public interface IToolOrchestrator
{
    // Process tool calls from an AI response, returning results for the next LLM call
    Task<IReadOnlyList<ToolResult>> ProcessToolCallsAsync(
        IReadOnlyList<ToolCall> toolCalls,
        ToolAutoApprovalSettings settings,
        CancellationToken ct);

    // Get available tool definitions to send to the LLM
    IReadOnlyList<ToolDefinition> GetAvailableToolDefinitions();

    // Check if a specific tool is enabled
    bool IsToolEnabled(string toolName);

    // Auto-approval settings for the current chat
    ToolAutoApprovalSettings GetAutoApprovalSettings();
}
```

---

## 8. Content Block Renderer

### `IContentBlockRenderer`
Plugin/registry pattern for rendering heterogeneous content blocks within chat messages. Each renderer handles a specific Markdig AST node type and produces WPF UI elements.

```csharp
public interface IContentBlockRenderer
{
    string RendererName { get; }
    int Priority { get; }  // Lower = checked first (ascending priority scan)

    // Can this renderer handle the given Markdig block/inline?
    bool CanRender(MarkdownObject markdownNode);

    // Render the Markdig node to WPF elements
    // The FlowDocument is the target; renderers add elements to it
    Task RenderAsync(
        MarkdownObject markdownNode,
        FlowDocument targetDocument,
        RenderContext context,
        CancellationToken ct);
}

public record RenderContext(
    ChatThread Thread,
    Message Message,
    IThemeProvider Theme,           // Current theme for color resolution
    bool IsStreaming,               // True during progressive rendering
    double AvailableWidth           // For layout calculations
);
```

**Priority Ordering:** The `ContentRendererRegistry` scans renderers in ascending priority order (lowest number first) and returns the first `CanRender()` match. Lower numbers = checked earlier. This allows specific renderers (e.g., code blocks) to take precedence over general-purpose renderers (e.g., Markdown text).

| Priority | Renderer | Rationale |
|----------|----------|-----------|
| 100 | `MarkdownTextRenderer` | General-purpose fallback — handles all standard Markdown constructs |
| 200 | `CodeBlockRenderer` | Must precede MarkdownText to intercept fenced code blocks before generic paragraph handling |
| 300 | `ArtifactReferenceRenderer` | Intercepts artifact citation nodes before generic link rendering |
| 350 | `CitationRenderer` | Intercepts footnote-style citation markers (`[1]`, `[2]`) before generic link rendering; renders as clickable superscript anchors |
| 400 | `ImageRenderer` | Intercepts image nodes before generic paragraph/inline handling |
| 500 | `MediaRenderer` | Audio/video embeds |
| 600 | `ThinkingRenderer` | Thinking/reasoning tokens |
| 700 | `ToolCallRenderer` | Tool call/result system messages |

**Concrete implementations:**

| Implementation | Handles | Output |
|---------------|---------|--------|
| `MarkdownTextRenderer` | Paragraphs, headings, bold, italic, lists, links, tables, blockquotes, horizontal rules | WPF `Paragraph`, `Run`, `Bold`, `Italic`, `Hyperlink`, `List`, `Table` elements |
| `CodeBlockRenderer` | Fenced code blocks (` ```language ... ``` `) | Syntax-highlighted `Section` with `Paragraph` elements + copy button. Uses AvalonEdit highlighting engine. |
| `ArtifactReferenceRenderer` | Artifact citations/references | Clickable artifact card (`Border` with artifact name, type, version) |
| `CitationRenderer` | Inline citation markers (`[1]`, `[2]`, etc.) from Deep Research and web search results | Clickable superscript link that scrolls to the Sources footnote section at the bottom of the message. Each source footnote shows: index number, **title** (linked to URL when available), **domain**, and **date-accessed**. |
| `ImageRenderer` | Inline images (`![alt](url)`) | WPF `Image` control with click-to-enlarge |
| `MediaRenderer` | Audio/video embeds | NAudio mini player / WPF `MediaElement` |
| `ThinkingRenderer` | Thinking/reasoning tokens (E3) | Collapsible `Expander` with "Thinking…" header |
| `ToolCallRenderer` | Tool call/result system messages | Styled border with tool name, parameters, result summary |

### Citation Rendering Details

Citations originate from Feature 14 — Tool Use & Agent Capabilities (H6 Deep Research), where the AI produces a report with inline citation markers (`[1]`, `[2]`, etc.) that reference a **Sources** section at the bottom of the message. The citation format is embedded directly in the Message's Markdown `content` field as structured Markdown footnotes:

```markdown
## Sources
[^1]: "Fusion Energy Outlook 2025" — iter.org — accessed 2026-06-15
[^2]: "Private Fusion Investment Hits $6B" — techcrunch.com — accessed 2026-06-15
```

**CitationRenderer behavior:**
1. **Inline markers:** Scans for `[1]`, `[2]`, etc. in the Markdown AST and renders them as clickable superscript links (WPF `Hyperlink` inside a `Span` with `Typography.Variants=Superscript`).
2. **Click action:** On click, scrolls the `FlowDocument` to the corresponding `[^N]:` footnote in the Sources section via WPF `BringIntoView()` or named-anchor navigation.
3. **Source footnote rendering:** Each `[^N]:` footnote is rendered as a styled `Paragraph` with index number, **bold linked title** (wrapped in `Hyperlink` pointing to source URL), domain in secondary color, and date-accessed in muted color.
4. **Graceful degradation:** If a footnote definition is missing (citation references a non-existent source), the inline marker renders as plain text (no link). If a URL is unavailable, the title is rendered as plain text (not hyperlinked).

**Ref:** [tech-sourcing #4](../tech-sourcing.md#4-markdown--code-rendering-engine)

---

### `IContentRendererRegistry`
Registry of all `IContentBlockRenderer` implementations, resolved at startup via DI.

```csharp
public interface IContentRendererRegistry
{
    void Register(IContentBlockRenderer renderer);
    IReadOnlyList<IContentBlockRenderer> GetRenderers();
    IContentBlockRenderer? Resolve(MarkdownObject markdownNode);
}
```

**Implementation:** `ContentRendererRegistry` — scans registered renderers by priority, returns first that `CanRender()`.

---

## 9. Theme Provider

### `IThemeProvider`
Abstraction for dark/light mode theming with runtime toggle. Also provides the three chat visual themes (Classic, Compact, Bubble).

```csharp
public interface IThemeProvider
{
    AppTheme CurrentAppTheme { get; }        // Dark, Light
    ChatTheme CurrentChatTheme { get; }      // Classic, Compact, Bubble

    // Theme resources (colors, brushes, font settings)
    ResourceDictionary GetAppThemeResources();
    DataTemplate GetChatMessageTemplate(ChatTheme theme);

    // Switch themes at runtime (no restart required)
    void SetAppTheme(AppTheme theme);
    void SetChatTheme(ChatTheme theme);

    // Font settings
    string FontFamily { get; }
    double FontSize { get; }         // 10-24px
    FontWeight FontWeight { get; }
    void SetFontSettings(string fontFamily, double fontSize, FontWeight fontWeight);

    // Theme-changed notification
    event EventHandler<AppTheme>? AppThemeChanged;
    event EventHandler<ChatTheme>? ChatThemeChanged;
}

public enum AppTheme { Dark, Light }
public enum ChatTheme { Classic, Compact, Bubble }
```

**Concrete implementations:**

| Implementation | Description |
|---------------|-------------|
| `WpfThemeProvider` | WPF `ResourceDictionary` with `DynamicResource` references. Two top-level dictionaries: `Dark.xaml`, `Light.xaml`. Three chat `DataTemplate` variants. Instant toggle via resource swap. |

**Ref:** [tech-sourcing #33](../tech-sourcing.md#33-theming--darklight-mode)

---

## 10. Update Checker

### `IUpdateChecker`
Abstraction for auto-update checking and installation.

```csharp
public interface IUpdateChecker
{
    // Check for available updates
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct);

    // Download the update
    Task<Stream> DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct);

    // Install the downloaded update (triggers app restart)
    Task InstallAsync(Stream updatePackage, CancellationToken ct);

    // Get the current app version
    Version CurrentVersion { get; }

    // Get the update feed URL
    string UpdateFeedUrl { get; }
}

public record UpdateCheckResult(
    bool UpdateAvailable,
    UpdateInfo? Update,
    string? ErrorMessage
);

public record UpdateInfo(
    Version Version,
    string ReleaseNotes,
    DateTimeOffset ReleaseDate,
    long DownloadSizeBytes,
    string DownloadUrl,
    bool IsMandatory
);
```

**Concrete implementations:**

| Implementation | Backend | Notes |
|---------------|---------|-------|
| `AutoUpdaterDotNet` | AutoUpdater.NET library | Checks XML/JSON feed, downloads MSIX, triggers install. |
| `MsixAppInstallerUpdater` | Windows App Installer (built-in) | For MSIX-packaged apps distributed via App Installer. |

**Ref:** [tech-sourcing #27](../tech-sourcing.md#27-auto-update-mechanism)

---

## 11. Repository Interfaces (Data Layer)

These are the data access contracts used by application services. All implemented via EF Core on SQLite.

### `IChatThreadRepository`
```csharp
public interface IChatThreadRepository
{
    Task<ChatThread?> GetByIdAsync(string id);
    Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort);
    Task<IReadOnlyList<ChatThread>> GetTransientInWindowAsync();  // <7 days old
    Task<IReadOnlyList<ChatThread>> GetTrashAsync();
    Task<IReadOnlyList<ChatThread>> SearchAsync(string query, int maxResults);
    Task<ChatThread> CreateAsync(ChatThread thread);
    Task UpdateAsync(ChatThread thread);
    Task SoftDeleteAsync(string id);
    Task PermanentDeleteAsync(string id);
    Task<int> CleanupTransientAsync(DateTimeOffset olderThan);
    Task<int> PurgeTrashAsync(DateTimeOffset olderThan);
}
```

### `IMessageRepository`
```csharp
public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(string id);
    Task<IReadOnlyList<Message>> GetActiveBranchAsync(string threadId);  // Follow isActiveBranch chain
    Task<IReadOnlyList<Message>> GetBranchAsync(string branchId);
    Task<IReadOnlyList<Message>> GetAllBranchesForThreadAsync(string threadId);  // For chat tree
    Task<IReadOnlyList<Message>> SearchAsync(string query, int maxResults);
    Task<Message> CreateAsync(Message message);
    Task UpdateAsync(Message message);
    Task SetActiveBranch(string messageId, string branchId);  // Branch navigation
    Task<int> GetBranchCountAsync(string threadId);
}
```

### `IPersonaRepository`
```csharp
public interface IPersonaRepository
{
    Task<IReadOnlyList<Persona>> GetAllAsync();
    Task<Persona?> GetByIdAsync(string id);
    Task<Persona?> GetDefaultAsync();
    Task<Persona> CreateAsync(Persona persona);
    Task UpdateAsync(Persona persona);
    Task DeleteAsync(string id);
}
```

### `IModelConfigurationRepository`
```csharp
public interface IModelConfigurationRepository
{
    Task<IReadOnlyList<ModelConfiguration>> GetAllAsync();
    Task<ModelConfiguration?> GetByIdAsync(string id);
    Task<ModelConfiguration> CreateAsync(ModelConfiguration config);
    Task UpdateAsync(ModelConfiguration config);
    Task DeleteAsync(string id);
}
```

### `IApiKeyRepository`
```csharp
public interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKey>> GetAllAsync();
    Task<ApiKey?> GetByIdAsync(string id);
    Task<ApiKey> CreateAsync(ApiKey key);
    Task UpdateAsync(ApiKey key);
    Task DeleteAsync(string id);
    // Encryption handled at service layer (DPAPI), not repository
}
```

### `IWikiIndexRepository`
```csharp
public interface IWikiIndexRepository
{
    Task<IReadOnlyList<WikiFile>> GetAllAsync();
    Task<WikiFile?> GetByPathAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> SearchAsync(string query, int maxResults);
    Task<WikiFile> UpsertAsync(WikiFile file);  // Insert or update
    Task DeleteAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> GetBacklinksAsync(string targetPath);
    Task<IReadOnlyList<WikiFile>> GetRelatedSectionsAsync(string filePath, int maxResults);
    Task<IReadOnlyList<WikiFile>> GetOrphansAsync();  // Files with no incoming links
    // Wiki snapshots
    Task<IReadOnlyList<WikiVersionSnapshot>> GetSnapshotsAsync(string filePath);
    Task CreateSnapshotAsync(WikiVersionSnapshot snapshot);
    Task PruneSnapshotsAsync(string filePath, int maxSnapshots, long maxTotalBytes);
    Task<WikiVersionSnapshot?> GetSnapshotAsync(string filePath, int versionNumber);
}
```

### `IUsageRepository`
```csharp
public interface IUsageRepository
{
    Task RecordUsageAsync(UsageRecord record);
    Task<IReadOnlyList<UsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to);
    Task<UsageSummary> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<UsageByProvider>> GetByProviderAsync(DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<UsageByModel>> GetByModelAsync(DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<UsageByChat>> GetByChatAsync(DateTimeOffset from, DateTimeOffset to);
    Task<FeedbackSummary> GetFeedbackSummaryAsync(DateTimeOffset from, DateTimeOffset to);
}
```

### `ISettingsRepository`
```csharp
public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync(string key, string value);
    Task SetAsync<T>(string key, T value) where T : class;
    Task DeleteAsync(string key);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync();
}
```

---

## 12. Service Interfaces (Cross-Cutting)

### `ILLMProviderService`
High-level service wrapping `ILLMProviderFactory` and `ITokenizerFactory`. The primary API consumed by ChatThreadService and all three UI tiers.

```csharp
public interface ILLMProviderService
{
    IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        IReadOnlyList<ToolDefinition>? tools,
        CancellationToken ct);

    Task<ChatResponse> ChatAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        CancellationToken ct);

    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ModelConfiguration config, CancellationToken ct);
    Task<bool> ValidateApiKeyAsync(ProviderType provider, string apiKey, string? endpointUrl, CancellationToken ct);

    // Token counting
    int CountTokens(string text, string modelId, ProviderType provider);
    int CountMessageTokens(ChatMessage message, string modelId, ProviderType provider);

    // Context overflow check
    ContextOverflowResult CheckContextOverflow(
        IReadOnlyList<ChatMessage> history,
        string newMessage,
        ModelConfiguration config);
}
```

### `IChatThreadService`
Central service managing all chat thread operations. Consumed by all UI tiers.

```csharp
public interface IChatThreadService
{
    // CRUD
    Task<ChatThread> CreateThreadAsync(string? title, bool isTransient, Persona persona);
    Task<ChatThread?> GetThreadAsync(string threadId);
    Task<IReadOnlyList<ChatThread>> GetPermanentThreadsAsync(ChatSortOrder sort);
    Task<IReadOnlyList<ChatThread>> GetTransientThreadsAsync();
    Task SoftDeleteThreadAsync(string threadId);
    Task RestoreThreadAsync(string threadId);
    Task PermanentDeleteThreadAsync(string threadId);

    // Elevation
    Task ElevateToPermanentAsync(string threadId);

    // Messages
    Task<Message> SendMessageAsync(string threadId, string content, CancellationToken ct);
    Task<Message> EditMessageAsync(string messageId, string newContent, bool createBranch);
    Task DeleteMessageAsync(string messageId);
    Task<Message> RegenerateAsync(string messageId, CancellationToken ct);
    Task<Message> ContinueGenerationAsync(string threadId, CancellationToken ct);

    // Branching
    Task<IReadOnlyList<Message>> GetActiveBranchMessagesAsync(string threadId);
    Task SetActiveBranchAsync(string messageId, string branchId);
    Task<int> GetBranchCountAsync(string threadId);
    Task<ChatTree> GetChatTreeAsync(string threadId);  // For D4 visualization

    // Search
    Task<IReadOnlyList<SearchResult>> SearchMessagesAsync(string query, int maxResults);

    // Auto-save drafts
    Task SaveDraftAsync(string threadId, string content, int cursorPosition);
    Task<MessageDraft?> GetDraftAsync(string threadId);
    Task DeleteDraftAsync(string threadId);
}
```

### `IWikiService`
Central service for all wiki operations.

```csharp
public interface IWikiService
{
    // Indexing
    Task IndexAllAsync(CancellationToken ct);
    Task IndexFileAsync(string relativePath, CancellationToken ct);

    // Search
    Task<IReadOnlyList<WikiSearchResult>> SearchAsync(string query, int maxResults);

    // Wiki Browser data
    Task<IReadOnlyList<WikiFileTree>> GetFileTreeAsync();
    Task<WikiFileDetail?> GetFileDetailAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> GetBacklinksAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> GetRelatedSectionsAsync(string relativePath, int maxResults);

    // Write to Wiki pipeline (N5)
    Task<WikiWritePreview> GenerateWikiContentAsync(string chatThreadId, string targetPath, CancellationToken ct);
    Task SaveToWikiAsync(string relativePath, string markdownContent, bool appendMode);

    // Versioning
    Task<IReadOnlyList<WikiVersionSnapshot>> GetVersionHistoryAsync(string relativePath);
    Task RestoreVersionAsync(string relativePath, int versionNumber);

    // Auto-generated index.md
    Task RegenerateIndexMdAsync(CancellationToken ct);

    // Git operations
    Task InitializeGitAsync();
    Task CommitChangesAsync(string message);
    Task PushAsync(CancellationToken ct);
}
```

---

## 13. Platform Service Interfaces (Windows-Specific)

These interfaces wrap platform integrations defined in [`integration-points.md`](integration-points.md). Each is a thin adapter over a Windows subsystem or OSS library, providing testability and clean separation from vendor APIs.

### `IEncryptionService`
Wraps Windows DPAPI for API key encryption at rest.

```csharp
public interface IEncryptionService
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
    string ProtectString(string plaintext);
    string UnprotectString(string ciphertext);
}
```

**Implementation:** `DpapiEncryptionService` — wraps `System.Security.Cryptography.ProtectedData`.

**Ref:** [integration-points #9](integration-points.md), [tech-sourcing #30](../tech-sourcing.md#30-encryption--api-keys--chat-locking)

---

### `IChatEncryptionService`
Wraps AES-256-GCM for locked chat message encryption.

```csharp
public interface IChatEncryptionService
{
    byte[] Encrypt(byte[] plaintext, string password, byte[] salt);
    byte[] Decrypt(byte[] ciphertext, string password, byte[] salt);
    byte[] GenerateSalt();  // 128-bit random salt per chat
    bool ValidatePassword(string password, byte[] ciphertext, byte[] salt);
}
```

**Implementation:** `AesGcmChatEncryptionService` — wraps `System.Security.Cryptography.AesGcm` (.NET 8+) with PBKDF2 key derivation via `Rfc2898DeriveKey`.

**Ref:** [integration-points #10](integration-points.md), [tech-sourcing #30](../tech-sourcing.md#30-encryption--api-keys--chat-locking)

---

### `IClipboardService`
Wraps Windows Clipboard for Tier 1 capture/apply and Copy MD/Copy Rich.

```csharp
public interface IClipboardService
{
    string? GetText();
    string? GetHtml();
    string? GetRtf();
    IReadOnlyList<string> GetAvailableFormats();
    void SetText(string text);
    void SetHtml(string html);
    void SetRtf(string rtf);
    void SetMultiFormat(Dictionary<string, object> formats);
    bool ContainsImage();
    byte[]? GetImage();
}
```

**Implementation:** `WpfClipboardService` — wraps `System.Windows.Clipboard` with STA thread marshalling.

**Ref:** [integration-points #11](integration-points.md), [tech-sourcing #7](../tech-sourcing.md#7-clipboard-format-preservation)

---

### `IWikiFileWatcher`
Monitors the wiki directory for external `.md` file changes, triggering re-indexing.

```csharp
public interface IWikiFileWatcher : IDisposable
{
    string WatchedDirectory { get; }
    bool IsRunning { get; }
    void Start(string directoryPath);
    void Stop();
    event EventHandler<WikiFileChangedEventArgs>? FileChanged;
}

public record WikiFileChangedEventArgs(
    string RelativePath,
    WikiFileChangeType ChangeType  // Created, Modified, Deleted, Renamed
);

public enum WikiFileChangeType { Created, Modified, Deleted, Renamed }
```

**Implementation:** `FileSystemWatcherAdapter` — wraps `System.IO.FileSystemWatcher` with 500ms debounce and 30s polling fallback.

**Ref:** [integration-points #12](integration-points.md), [tech-sourcing #12](../tech-sourcing.md#12-file-system-watcher-wiki-monitoring)

---

### `ILocalWebSocketServer`
Embedded Kestrel WebSocket server on 127.0.0.1 for external integrations.

```csharp
public interface ILocalWebSocketServer : IDisposable
{
    int Port { get; }
    bool IsRunning { get; }
    string AuthToken { get; }
    Task StartAsync(int? preferredPort = null, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    event EventHandler<string>? MessageReceived;
    Task SendAsync(string message, CancellationToken ct = default);
    string RegenerateAuthToken();
}
```

**Implementation:** `KestrelWebSocketServer` — wraps ASP.NET Core Kestrel with token-based auth and JSON protocol.

**Ref:** [integration-points #13](integration-points.md), [tech-sourcing #8](../tech-sourcing.md#8-local-websocket-server)

---

### `ISystemTrayService`
Manages the Windows system tray icon (NotifyIcon via WinForms interop).

```csharp
public interface ISystemTrayService : IDisposable
{
    bool IsVisible { get; }
    void Show();
    void Hide();
    void SetGenerationIndicator(bool isGenerating);
    void UpdateRecentChats(IReadOnlyList<string> recentChatTitles);
    event EventHandler? OpenStudioRequested;
    event EventHandler? NewChatRequested;
    event EventHandler? CommandBarRequested;
    event EventHandler? SettingsRequested;
    event EventHandler? ExitRequested;
}
```

**Implementation:** `WinFormsSystemTrayService` — wraps `System.Windows.Forms.NotifyIcon`.

**Ref:** [integration-points #14](integration-points.md), [tech-sourcing #9](../tech-sourcing.md#9-system-tray-integration)

---

### `IGlobalHotkeyService`
Manages system-wide hotkey registration for Tier 1 text actions and Tier 2 Command Bar.

```csharp
public interface IGlobalHotkeyService : IDisposable
{
    bool RegisterHotkey(string hotkeyId, ModifierKeys modifiers, VirtualKey key);
    bool UnregisterHotkey(string hotkeyId);
    bool IsRegistered(string hotkeyId);
    IReadOnlyList<HotkeyAssignment> GetRegisteredHotkeys();
    bool DetectConflict(ModifierKeys modifiers, VirtualKey key);
    event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
}

public record HotkeyAssignment(string HotkeyId, ModifierKeys Modifiers, VirtualKey Key);
public record HotkeyTriggeredEventArgs(string HotkeyId);

public enum VirtualKey { /* Windows virtual key codes */ }
```

**Implementation:** `GlobalHotkeyService` — wraps `RegisterHotKey` (P/Invoke, primary) with `WH_KEYBOARD_LL` fallback.

**Ref:** [integration-points #15](integration-points.md), [tech-sourcing #5](../tech-sourcing.md#5-global-keyboard-hooks)

---

### `IHwndCaptureService`
Captures the active window handle, source app name, document title, and captured content for Tier 1 text actions. The graduated UIA capture pipeline — selection text (TextPattern), focused element content (ValuePattern), surrounding context (TreeWalker), full document text (DocumentRange), and screenshot capture (Win32 PrintWindow/BitBlt) — is implemented as part of Feature 13 (Text Actions & Three-Tier System), per the TextAction's captureScope flags.

```csharp
public interface IHwndCaptureService
{
    HwndCaptureResult CaptureActiveWindow();
    bool IsWindowStillOpen(IntPtr hwnd);
    string? GetWindowTitle(IntPtr hwnd);
    string? GetProcessName(IntPtr hwnd);
}

public record HwndCaptureResult(
    IntPtr Hwnd,
    string? AppName,
    string? DocumentTitle,
    string? SelectedText
);
```

**Implementation:** `Win32HwndCaptureService` — P/Invoke `GetForegroundWindow`, `GetWindowText`, `GetWindowThreadProcessId` + UIA `TextPattern.GetSelection()`.

**Ref:** [integration-points #16](integration-points.md), [tech-sourcing #6](../tech-sourcing.md#6-hwnd-capture--text-injection-spatial-anchoring)

---

### `ITextInjectionService`
Injects AI-transformed text back into the source window for Tier 1 Apply, supporting all seven apply modes: `replaceSelection` (HWND injection), `insertAtCursor` (UIA TextPattern), `replaceFocusedElement` (UIA ValuePattern), `appendToFocusedElement`, `prependToFocusedElement`, `clipboardOnly`, and `showOnly` (no injection). Each mode has its own layered fallback chain.

```csharp
public interface ITextInjectionService
{
    Task<TextInjectionResult> InjectAsync(IntPtr targetHwnd, string text, IReadOnlyList<string> availableFormats);
}

public record TextInjectionResult(
    bool Success,
    string MethodUsed,     // "UIA", "WM_SETTEXT", "Clipboard"
    string? ErrorMessage
);
```

**Implementation:** `UiaTextInjectionService` — layered fallback: UIA `ValuePattern.SetValue()` → `WM_SETTEXT` → `SendInput` Ctrl+V.

**Ref:** [integration-points #16](integration-points.md), [tech-sourcing #6](../tech-sourcing.md#6-hwnd-capture--text-injection-spatial-anchoring)

---

### `IAudioService`
Wraps NAudio for microphone recording and audio playback.

```csharp
public interface IAudioService : IDisposable
{
    // Recording
    Task<byte[]> RecordAsync(TimeSpan duration, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> RecordStreamAsync(CancellationToken ct = default);
    bool IsMicrophoneAvailable { get; }

    // Playback
    Task PlayAsync(byte[] audioData, string format, CancellationToken ct = default);
    void StopPlayback();
    event EventHandler<PlaybackPositionEventArgs>? PlaybackPositionChanged;
}

public record PlaybackPositionEventArgs(TimeSpan Position, TimeSpan Duration);
```

**Implementation:** `NaudioAudioService` — wraps NAudio for recording (WAV) and playback with position tracking.

**Ref:** [integration-points #19](integration-points.md), [tech-sourcing #22](../tech-sourcing.md#22-audio-recording--playback)

---

### `ICameraService`
Wraps AForge.NET for webcam enumeration and still-image capture.

```csharp
public interface ICameraService : IDisposable
{
    IReadOnlyList<string> GetAvailableCameras();
    bool IsCameraAvailable { get; }
    Task<byte[]> CaptureStillAsync(string? cameraDeviceId = null, CancellationToken ct = default);
    Task<Stream> GetPreviewStreamAsync(CancellationToken ct = default);
    void StopPreview();
}
```

**Implementation:** `AForgeCameraService` — wraps AForge.Video.DirectShow with Emgu.CV fallback.

**Ref:** [integration-points #20](integration-points.md), [tech-sourcing #23](../tech-sourcing.md#23-webcam-capture)

---

### `IVideoPlayerService`
Video playback abstraction with MediaElement primary and LibVLCSharp fallback.

```csharp
public interface IVideoPlayerService : IDisposable
{
    FrameworkElement CreatePlayer(string filePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    bool IsPlaying { get; }
    event EventHandler<VideoErrorEventArgs>? PlaybackError;
}

public record VideoErrorEventArgs(string FilePath, string ErrorMessage, bool FallbackAvailable);
```

**Implementation:** `WpfVideoPlayerService` — wraps WPF `MediaElement` primary, `LibVLCSharp` fallback.

**Ref:** [integration-points #21](integration-points.md), [tech-sourcing #24](../tech-sourcing.md#24-video-playback)

---

### `ISpellCheckService`
Wraps WeCantSpell.Hunspell for red squiggly underlines and suggestions.

```csharp
public interface ISpellCheckService
{
    bool IsEnabled { get; set; }
    bool CheckWord(string word);
    IReadOnlyList<string> GetSuggestions(string word);
    void AddToCustomDictionary(string word);
    void RemoveFromCustomDictionary(string word);
    bool IsInCustomDictionary(string word);
    void SetLanguage(string languageCode);  // "en-US", "he-IL"
}
```

**Implementation:** `HunspellSpellCheckService` — wraps WeCantSpell.Hunspell with English dictionary and custom SQLite dictionary.

**Ref:** [integration-points #22](integration-points.md), [tech-sourcing #25](../tech-sourcing.md#25-spell-checking)

---

### `IDialogService`
- **Purpose:** Abstract dialog/popup interactions for MVVM ViewModels (confirmation dialogs, file pickers, diff viewers).
- **Methods:** `ShowConfirmationAsync(title, message) → bool`, `ShowDiffViewerAsync(before, after, title) → bool`, `ShowFilePickerAsync(filter) → string`.
- **First Implementation:** `WpfDialogService` (WPF MessageBox + custom DiffViewer window).
- **Used By:** F9a (close confirmation), F10a (delete confirmation), F12 (terminal confirmation), F14 (Write to Wiki Diff Viewer).

---

### `IWikiGitService`
Wraps LibGit2Sharp for wiki directory version control (optional GitHub push).

```csharp
public interface IWikiGitService : IDisposable
{
    bool IsInitialized { get; }
    bool IsRemoteConfigured { get; }
    Task InitializeAsync(string repoPath, CancellationToken ct = default);
    Task CommitAsync(string message, CancellationToken ct = default);
    Task PushAsync(string? remoteName = null, string? branchName = null, CancellationToken ct = default);
    Task<IReadOnlyList<GitLogEntry>> GetLogAsync(int maxCount = 50, CancellationToken ct = default);
    void ConfigureRemote(string remoteUrl, string personalAccessToken);
    event EventHandler<GitCommitEventArgs>? CommitCompleted;
}

public record GitLogEntry(string Sha, string Message, DateTimeOffset Timestamp, string Author);
public record GitCommitEventArgs(int FilesChanged);
```

**Implementation:** `LibGit2SharpGitService` — wraps LibGit2Sharp for init, add, commit (debounced 5s), push. PAT encrypted via DPAPI.

**Ref:** [integration-points #23](integration-points.md), [tech-sourcing #28](../tech-sourcing.md#28-git-integration-wiki-version-control)

---

### `IChatSearchService`
Full-text search across chat messages via SQLite FTS5.

```csharp
public interface IChatSearchService
{
    Task<IReadOnlyList<ChatSearchResult>> SearchAsync(string query, int maxResults = 20, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSearchResult>> SearchTransientAsync(string query, int maxResults = 20, CancellationToken ct = default);
}

public record ChatSearchResult(
    string MessageId,
    string ThreadId,
    string ThreadTitle,
    string Snippet,          // FTS5 snippet with highlights
    string Role,
    DateTimeOffset Timestamp
);
```

**Implementation:** `Fts5ChatSearchService` — queries SQLite FTS5 virtual table with porter stemmer and unicode61 tokenizer; returns ranked results with `snippet()`.

**Ref:** [tech-sourcing #11](../tech-sourcing.md#11-full-text-search)

---

### `IAutoCleanupService`
Background task for transient thread cleanup and Trash auto-purge.

```csharp
public interface IAutoCleanupService : IDisposable
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    Task<int> CleanupTransientAsync(CancellationToken ct = default);  // 7-day expiry
    Task<int> PurgeTrashAsync(CancellationToken ct = default);        // 30-day expiry
    event EventHandler<CleanupCompletedEventArgs>? CleanupCompleted;
}

public record CleanupCompletedEventArgs(int TransientDeleted, int TrashPurged);
```

**Implementation:** `PeriodicAutoCleanupService` — runs on startup + configurable interval (hourly for transient, daily for trash). Respects exclusion rules (favorited, tagged, pinned, archived, has user replies, has artifacts).

**Ref:** [tech-sourcing #31](../tech-sourcing.md#31-message-branching-data-model)

---

## 14. Diagnostics & Logging — Serilog Destructuring Policy

Diagnostics (V) uses the existing logging infrastructure (Feature 3) and [`ISettingsRepository`](#isettingsrepository) for persisting 9 log configuration keys. No new C# interfaces are required — the feature consumes `ILogger<T>` (Microsoft.Extensions.Logging) and `ISettingsRepository`.

### API Key Redaction Policy

A Serilog `IDestructuringPolicy` must be registered to redact `ApiKey` property values in structured log output. This applies globally across ALL log categories:

```csharp
// Registration in Serilog configuration (Program.cs / App.xaml.cs startup)
Log.Logger = new LoggerConfiguration()
    .Destructure.With<ApiKeyDestructuringPolicy>()  // Redacts ApiKey values
    .WriteTo.File(...)
    .CreateLogger();

// Destructuring policy
public class ApiKeyDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        if (value is string s && LooksLikeApiKey(s))
        {
            result = new ScalarValue("[REDACTED]");
            return true;
        }
        result = null;
        return false;
    }
}
```

**Security requirement:** Under no circumstances may the raw API key appear in any log file. This destructuring policy is the enforcement mechanism. It applies to all log categories (LLM API Calls, Tier 1/2, Database, etc.) — any log event that includes an `ApiKey` property is automatically redacted.

**Ref:** [Vision V — Diagnostics & Debug Logging](../vision/features/diagnostics-debug-logging.md), [Vision Data — ApiKey](../vision/data/api-key.md)

---

## Interface Dependency Map

```
UI Layer (Tier1Overlay, Tier2CommandBar, MainWindow)
    │
    ├── IChatThreadService
    │       ├── IChatThreadRepository
    │       ├── IMessageRepository
    │       ├── IChatSearchService
    │       └── ILLMProviderService
    │               ├── ILLMProviderFactory → ILLMProvider × 4
    │               └── ITokenizerFactory → ITokenizer × 3
    │
    ├── IWikiService
    │       ├── IWikiIndexRepository
    │       ├── IWikiFileWatcher
    │       └── IWikiGitService → LibGit2Sharp
    │
    ├── IToolOrchestrator
    │       ├── IToolExecutor × 5
    │       │       ├── ISearchProvider (web_search)
    │       │       └── System.Diagnostics.Process (terminal)
    │       └── ISearchProvider
    │
    ├── IThemeProvider
    ├── IContentRendererRegistry → IContentBlockRenderer × 8
    ├── ISTTProvider
    ├── IBackupProvider
    ├── IUpdateChecker
    ├── IChatImporter × 2
    ├── IAutoCleanupService
    │
    ├── Platform Services
    │   ├── IEncryptionService (DPAPI)
    │   ├── IChatEncryptionService (AES-256-GCM)
    │   ├── IClipboardService
    │   ├── IGlobalHotkeyService
    │   ├── IHwndCaptureService
    │   ├── ITextInjectionService
    │   ├── ISystemTrayService
    │   ├── ILocalWebSocketServer
    │   ├── IAudioService
    │   ├── ICameraService
    │   ├── IVideoPlayerService
    │   └── ISpellCheckService
    │
    └── Data Repositories (IPersonaRepository, IModelConfigurationRepository,
        IApiKeyRepository, IUsageRepository, ISettingsRepository, ...)
            └── EF Core DbContext → Microsoft.Data.Sqlite → SQLite
```

---

*Abstractions document — Batch 1 of planning/ directory. See also: [`architecture.md`](architecture.md), [`tech-stack.md`](tech-stack.md).*
