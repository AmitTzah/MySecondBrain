# Feature Implementation Plan: Studio Chat — Core Workspace

## 1. Overall Project Context

MySecondBrain is a **local-first .NET 8.0 WPF desktop application** — a "second brain" knowledge management and AI chat studio. It is organized as a 3-project layered architecture (Core → Data → Services → UI), uses EF Core with SQLite for persistence (15 entities), CommunityToolkit.Mvvm for MVVM, and supports 8 LLM providers via a Provider/Adapter pattern. The application features a WebView2-based artifacts panel, 14-tool agent surface with per-chat workspace isolation, comprehensive settings (16 categories), personas, model configurations, and a full E2E test suite (62 tests). Features 1-11 are complete: solution scaffold, DI container (76+ registrations), Serilog logging, SQLite data layer, 3-region MainWindow shell with 8 navigable screens + Dark/Light theming + 3 chat visual themes, Windows platform infrastructure, API key management, Model Configuration CRUD, Persona CRUD, Settings, Onboarding Wizard, Diagnostics, E2E test suite, 14-tool codebase realignment, and Agent Skills subsystem.

## 2. Feature-Specific Context

**Feature 12 — Studio Chat — Core Workspace** is the centerpiece of the MySecondBrain application — the Tier 3 primary workspace where users spend 90% of their time. This feature transforms the existing shell (ChatView with static placeholders, stub services, and stub content renderers) into a fully functional AI chat studio.

The feature delivers: conversation view with VirtualizingStackPanel for memory-efficient message rendering; full Markdig→WPF FlowDocument Markdown rendering (headings, bold, italic, code blocks with AvalonEdit syntax highlighting for 100+ languages, lists, links, tables, blockquotes); streaming token-by-token progressive rendering with auto-scroll management; message actions (Send/Stop with partial response preservation, Regenerate, Continue Generation); Copy MD and Copy Rich per message; auto-generated chat titling via AI; error handling with specific messages and Retry; scroll-to-bottom floating button; clear conversation with undo; chat header three-dot menu; message selection mode with bulk actions; offline/network status indicator; close confirmation during active generation; pin window/always on top; dark/light quick toggle; font size quick adjust; chat header full layout (Persona name, context bar, cost, API History button, help icon, source banner, font size, dark mode, pin, three-dot menu); file viewer tabs (read-only, syntax-highlighted); incognito/temporary chat toggle; locked chats (AES-256-GCM); chat summarization; message favoriting; cross-tab completion alert; right panel layout (Artifacts top + Chat Nav bottom); [Apply] button shell (grayed-out); chat modes (Standard, TextCompletion, Thinking toggle, Mute); and Hebrew RTL auto-detection with mixed LTR/RTL rendering.

**Dependencies:** Features 4 (Data Layer), 5 (App Shell & Theming), 7 (Model Configs & Personas), and 11 (14-Tool Surface & Vision Alignment) provide the foundation. All repositories, services, LLM providers, tools, skills, and content renderer interfaces are registered in DI but are stubs or shell implementations.

## 3. Architecture and Extensibility

### Design Patterns

**MVVM with CommunityToolkit.Mvvm:** `ChatThreadViewModel` is the central ViewModel, receiving `IChatThreadService`, `IPersonaRepository`, `IModelConfigurationRepository`, `ISettingsRepository`, `ISkillService`, and `ILogger<T>` via constructor injection. It manages chat tabs, active chat state, message sending, streaming state, and all chat controls. Properties use `[ObservableProperty]`, commands use `[RelayCommand]`, cross-VM communication uses `WeakReferenceMessenger`.

**Plugin/Registry Pattern for Content Rendering:** The existing `IContentBlockRenderer` interface with 8 registered implementations in `ContentRendererRegistry` is the extensibility point for Markdown rendering. Each renderer converts a `Markdig.Syntax.MarkdownObject` AST node to WPF `FlowDocument` elements. Adding a new content type (e.g., Mermaid diagrams) requires only implementing the interface and one `AddSingleton` line — the registry auto-discovers it via `IEnumerable<IContentBlockRenderer>`.

**Streaming Renderer — Progressive FlowDocument Updates:** A new `MarkdownStreamRenderer` service (in `MySecondBrain.Services/Chat/`) manages the token-by-token progressive rendering pipeline. It receives `StreamChunk` deltas, accumulates text into a buffer, incrementally parses via Markdig, and updates a WPF `FlowDocument` in-place. This is decoupled from the ViewModel — the ViewModel owns the streaming state and cancellation, while the renderer owns the parsing and FlowDocument mutation logic. The renderer is testable in isolation with mock `FlowDocument` targets.

**Tab Management — ObservableCollection Pattern:** Chat tabs are managed as an `ObservableCollection<ChatTabItem>` in `ChatThreadViewModel`. Each `ChatTabItem` wraps a `ChatThread` with its own message list, scroll position, and streaming state. The WPF `TabControl` binds to this collection. Tab reordering uses drag-and-drop via WPF attached behaviors. This pattern supports future extensibility: file viewer tabs (C39) are `FileViewerTabItem` subclasses sharing the same tab bar.

**RTL Detection — Per-Message FlowDirection:** A `BidiHelper` static utility in `MySecondBrain.Core/Utilities/` scans message content for Hebrew Unicode range (U+0590–U+05FF). If >30% of alphabetic characters are Hebrew, the message container's `FlowDirection` is set to `RightToLeft`. Code blocks always enforce `FlowDirection="LeftToRight"`. Mixed LTR/RTL segments rely on WPF FlowDocument's built-in Unicode Bidirectional Algorithm.

**Locked Chat Encryption — AES-256-GCM:** The existing `IChatEncryptionService` (AesGcmChatEncryptionService) with PBKDF2 key derivation provides the encryption backbone. A new `LockedChatService` in `MySecondBrain.Services/Encryption/` wraps this with password-prompt UI integration (via `IConfirmationService`), per-chat salt management, and password validation.

**Extensibility for Future Features:**
- F13 (Input & Media): Textbox toolbar already has placeholder buttons; attachment row and drag-drop zones are designed into the layout
- F14 (Message Branching): Message branching attributes (branchId, versionNumber, isActiveBranch) are already on the entity; branch navigation UI slots into the Chat Nav panel
- F15 (Data Lifecycle): Soft-delete (isDeleted, deletedAt) is already on ChatThread; Clear Conversation uses this pattern
- F16 (Artifacts): Right panel artifacts section header already exists; WebView2 host is registered
- F17 (Tool Use): 14 IToolExecutor implementations are already registered; ToolCallRenderer already exists
- F18 (Text Actions): [Apply] button shell is included but grayed-out; source context is stored on ChatThread

## 4. Final Expected Project Structure

```
src/
  MySecondBrain.Core/
    Interfaces/
      [EXISTING] IChatThreadService.cs
      [EXISTING] IChatThreadRepository.cs
      [EXISTING] IMessageRepository.cs
      [EXISTING] IContentBlockRenderer.cs
      [EXISTING] IContentRendererRegistry.cs
      [EXISTING] IChatEncryptionService.cs
      [EXISTING] IThemeProvider.cs
    Models/
      [EXISTING] DomainModels.cs (ChatThread, Message, Persona, etc.)
      [EXISTING] Enums.cs (ScreenType, AppTheme, ChatTheme, etc.)
      [NEW] ChatTabItem.cs (~50 lines — ObservableObject wrapper for ChatThread + tab state)
      [NEW] StreamRenderState.cs (~40 lines — streaming state DTO)
    Utilities/
      [NEW] BidiHelper.cs (~60 lines — RTL detection, FlowDirection resolution)
      [NEW] MarkdownHelper.cs (~60 lines — Markdig pipeline configuration, extensions)

  MySecondBrain.Data/
    Entities/
      [MODIFIED] ChatThread.cs (~25 properties — verify all vision fields present)
      [MODIFIED] Message.cs (~18 properties — verify all vision fields present)
    Repositories/
      [MODIFIED] ChatThreadRepository.cs (~200 lines — fill stub with EF Core queries)
      [MODIFIED] MessageRepository.cs (~180 lines — fill stub with EF Core queries, recursive CTE)
    Migrations/
      [NEW] AddChatThreadFields.cs (auto-generated — isFavorite, isPinned, isArchived, colorLabel, tags, folderId, isLocked, lockSalt)

  MySecondBrain.Services/
    Chat/
      [MODIFIED] ChatThreadService.cs (~150 lines — orchestrator composing sub-services, delegates all IChatThreadService methods)
      [NEW] ChatThreadLifecycleService.cs (~200 lines — thread CRUD, soft-delete, permanent delete, elevate, transient management + persona resolution)
      [NEW] ChatMessageService.cs (~200 lines — send, edit, delete messages + cost calculation + usage recording)
      [NEW] ChatBranchService.cs (~150 lines — branch navigation, search, chat tree)
      [NEW] ChatDraftService.cs (~80 lines — draft save/get/delete)
      [EXISTING] MarkdownStreamRenderer.cs (~300 lines — progressive token-to-FlowDocument rendering)
      [EXISTING] ChatTitleGenerator.cs (~80 lines — AI-powered title generation via LLM)
    Encryption/
      [NEW] LockedChatService.cs (~150 lines — AES-256-GCM chat lock/unlock with password)
    [NEW] ChatExportService.cs (~150 lines — Export to Markdown/JSON)

  MySecondBrain.UI/
    Views/
      [MODIFIED] ChatView.xaml (~350 lines — full conversation view, tab bar, message templates)
      [MODIFIED] ChatView.xaml.cs (~100 lines — code-behind for scroll management, drag-drop)
      [MODIFIED] MainWindow.xaml (~370 lines — tab bar integration, right panel wiring)
      [MODIFIED] MainWindow.xaml.cs (~50 lines — tab management, keyboard shortcuts)
      [NEW] ChatHeaderBar.xaml (~120 lines — standalone header UserControl)
      [NEW] ChatHeaderBar.xaml.cs (~30 lines)
      [NEW] MessageActionBar.xaml (~60 lines — per-message action buttons)
      [NEW] MessageActionBar.xaml.cs (~20 lines)
      [NEW] FileViewerTab.xaml (~80 lines — read-only file viewer with syntax highlighting)
      [NEW] FileViewerTab.xaml.cs (~40 lines)
      [NEW] LockedChatPasswordDialog.xaml (~50 lines — password prompt dialog)
      [NEW] LockedChatPasswordDialog.xaml.cs (~20 lines)
    ViewModels/
      [MODIFIED] ChatThreadViewModel.cs (~600 lines — full implementation with tab management)
      [NEW] FileViewerTabViewModel.cs (~100 lines — file viewer tab ViewModel)
      [NEW] LockedChatViewModel.cs (~80 lines — password prompt ViewModel)
    Controls/
      [MODIFIED] MarkdownTextRenderer.cs (~250 lines — Markdig AST → FlowDocument paragraphs, headings, etc.)
      [MODIFIED] CodeBlockRenderer.cs (~150 lines — AvalonEdit highlighting, copy button)
      [MODIFIED] ThinkingRenderer.cs (~100 lines — collapsible thinking block)
      [MODIFIED] ToolCallRenderer.cs (~120 lines — styled tool call cards)
      [NEW] ScrollToBottomButton.cs (~50 lines — floating button control)
      [NEW] TokenContextBar.xaml (~40 lines — colored context window fill bar)
      [NEW] TokenContextBar.xaml.cs (~20 lines)
    Converters/
      [NEW] FlowDirectionConverter.cs (~30 lines — bool/String to FlowDirection)
      [NEW] RelativeTimeConverter.cs (~40 lines — DateTimeOffset → "2 min ago")
      [NEW] TokenCountToColorConverter.cs (~40 lines — usage% → green/yellow/red)
      [NEW] BoolToScrollBarVisibilityConverter.cs (~30 lines)
    Services/
      [MODIFIED] WpfThemeProvider.cs (~10 lines — add PinWindow persistence)
      [NEW] WpfClipboardService.cs (already exists, verify Copy Rich support)

tests/
  unit/MySecondBrain.Tests.Unit/
    [MODIFIED] ChatThreadViewModelTests.cs (~500 lines — expanded to cover all new functionality)
    [NEW] ChatThreadRepositoryTests.cs (~150 lines — repository CRUD and FTS5 search tests)
    [NEW] MessageRepositoryTests.cs (~150 lines — repository CRUD and branch navigation tests)
    [NEW] MarkdownStreamRendererTests.cs (~150 lines)
    [NEW] BidiHelperTests.cs (~120 lines)
    [NEW] ChatThreadServiceTests.cs (~200 lines)
    [NEW] ChatTitleGeneratorTests.cs (~80 lines)
    [NEW] LockedChatServiceTests.cs (~120 lines)
    [NEW] MarkdownTextRendererTests.cs (~100 lines)
    [NEW] RelativeTimeConverterTests.cs (~60 lines)
    [NEW] TokenCountToColorConverterTests.cs (~60 lines)
  integration/MySecondBrain.Tests.Integration/
    [NEW] ChatWorkflowIntegrationTests.cs (~200 lines)
    [NEW] StreamingRenderIntegrationTests.cs (~100 lines)
  e2e/MySecondBrain.Tests.E2E/
    [NEW] StudioChatE2ETests.cs (~400 lines — comprehensive chat workflow tests)
```

## 5. Execution Steps

### [x] Step 1: Fill Repository Stubs — ChatThread & Message Data Access
- **Goal:** Replace all stub methods in `ChatThreadRepository` and `MessageRepository` with real EF Core queries against `AppDbContext`. Add any missing entity fields to `ChatThread` and `Message` for vision compliance (organization fields: isFavorite, isPinned, isArchived, colorLabel, tags, folderId; locking fields: isLocked, lockSalt on ChatThread; favoriting field: isFavorited on Message). Create the EF Core migration.
- **Actions:**
  - Add missing properties to `ChatThread` entity: `isFavorite`, `isPinned`, `isArchived`, `colorLabel`, `tags` (JSON string), `folderId`, `isLocked`, `lockSalt`, `lockNonce`
  - Add missing properties to `Message` entity: `isFavorited`, `thinkingContent`
  - Create EF Core migration `AddChatOrganizationFields`
  - Fill `ChatThreadRepository`: `GetByIdAsync` (with Includes), `GetAllPermanentAsync` (with sort ordering, isDeleted=false, isTransient=false), `GetTransientInWindowAsync`, `GetTrashAsync`, `SearchAsync` (FTS5), `CreateAsync`, `UpdateAsync`, `SoftDeleteAsync`, `PermanentDeleteAsync` (cascade), `CleanupTransientAsync`, `PurgeTrashAsync`
  - Fill `MessageRepository`: `GetByIdAsync`, `GetActiveBranchAsync` (recursive CTE following isActiveBranch=true), `GetBranchAsync`, `GetAllBranchesForThreadAsync`, `SearchAsync` (FTS5), `CreateAsync`, `UpdateAsync`, `SetActiveBranch`, `GetBranchCountAsync`
  - Add indexes on frequently-queried columns: `ChatThread.LastActivityAt`, `Message.ThreadId`, `Message.IsActiveBranch`
- **Unit Tests to Write:**
  - `ChatThreadRepositoryTests.cs`: Test CreateAsync persists entity; GetByIdAsync returns with includes; GetAllPermanentAsync filters transient/deleted; SoftDeleteAsync sets flags; SearchAsync returns FTS5 matches
  - `MessageRepositoryTests.cs`: Test CreateAsync; GetActiveBranchAsync returns correct chain; SetActiveBranch switches active version; GetBranchCountAsync
- **Integration Tests to Write:**
  - `ChatWorkflowIntegrationTests.cs`: Test full create-thread→add-messages→query flow with real SQLite
- **Live Smoke Test (Mandatory):** Build the project, run a temporary console test program that: (1) creates a ChatThread via repository, (2) creates 3 Messages in the thread, (3) retrieves messages via GetActiveBranchAsync, (4) verifies FTS5 search finds a message by content, (5) soft-deletes the thread and verifies it's excluded from GetAllPermanentAsync. Verify all operations succeed with no EF Core exceptions.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: fill ChatThread and Message repository stubs with EF Core queries`

---

### [x] Step 2: Fill ChatThreadService — Core Chat Operations with LLM Integration
- **Goal:** Replace all stub methods in `ChatThreadService` with real implementations. Wire `CreateThreadAsync`, `SendMessageAsync` (full message lifecycle: create user message → build context → call LLM → stream chunks → persist assistant message), `GetActiveBranchMessagesAsync`, `RegenerateAsync`, `ContinueGenerationAsync`, `ElevateToPermanentAsync`, `SaveDraftAsync`/`GetDraftAsync`. Integrate with `ILLMProviderService` for actual LLM calls. Create the `MarkdownStreamRenderer` service for progressive FlowDocument updates. Create `ChatTitleGenerator` for AI-powered auto-titling.
- **Actions:**
  - Fill `CreateThreadAsync`: instantiate `ChatThread` with persona defaults (chatMode, thinkingEnabled from persona; isTransient flag), persist via `_threadRepo.CreateAsync`
  - Fill `SendMessageAsync`: (a) create user `Message` entity, (b) build conversation context via `GetActiveBranchMessagesAsync`, (c) resolve Persona + ModelConfiguration, (d) call `_llmService.ChatStreamAsync()`, (e) accumulate response text from stream chunks, (f) create assistant `Message` entity with token counts, cost, generation time, (g) trigger auto-title on first message pair, (h) update `ChatThread.LastActivityAt`
  - Fill `GetActiveBranchMessagesAsync`: delegate to `_messageRepo.GetActiveBranchAsync` (recursive CTE)
  - Fill `RegenerateAsync`: find last assistant message, create new version (increment versionNumber, same branchId), call LLM with same context, set old version `isActiveBranch=false`
  - Fill `ContinueGenerationAsync`: find last assistant message, call LLM with continuation prefix, append content
  - Fill `ElevateToPermanentAsync`: set `isTransient=false`
  - Fill `SaveDraftAsync`/`GetDraftAsync`/`DeleteDraftAsync`: use `AppDbContext.MessageDrafts` directly (upsert pattern)
  - Create [`MarkdownStreamRenderer`](src/MySecondBrain.Services/Chat/MarkdownStreamRenderer.cs) in `Services/Chat/`: receives `IAsyncEnumerable<StreamChunk>`, accumulates text buffer, uses Markdig incremental parsing, invokes `IContentRendererRegistry` to render AST nodes to a `FlowDocument`, supports cancellation mid-stream
  - Create [`ChatTitleGenerator`](src/MySecondBrain.Services/Chat/ChatTitleGenerator.cs): takes first user message + assistant response, sends a small prompt to LLM ("Generate a 3-7 word title..."), returns title or falls back to first 50 chars
  - Register new services in DI (`DependencyInjectionConfig.cs`)
- **Unit Tests to Write:**
  - `ChatThreadServiceTests.cs`: Test CreateThreadAsync creates thread with persona defaults; SendMessageAsync creates user+assistant messages and calls LLM; GetActiveBranchMessagesAsync returns ordered messages; RegenerateAsync creates new version; ContinueGenerationAsync appends; ElevateToPermanentAsync flips flag; SaveDraftAsync persists draft
  - `MarkdownStreamRendererTests.cs`: Test progressive text accumulation; cancellation mid-stream; empty stream handling; Markdig parse error resilience
  - `ChatTitleGeneratorTests.cs`: Test title generation from message pair; fallback on LLM failure; empty message handling
- **Integration Tests to Write:**
  - `ChatWorkflowIntegrationTests.cs`: End-to-end send message flow with real SQLite and mock LLM provider
- **Live Smoke Test (Mandatory):** Build the project, run a temporary console test program that: (1) creates a ChatThread, (2) sends a user message "Hello, introduce yourself in one sentence", (3) verifies the assistant response Message entity is persisted with non-empty content, (4) verifies token counts are populated, (5) verifies ChatThread.LastActivityAt is updated, (6) retrieves messages via GetActiveBranchMessagesAsync and confirms both user and assistant messages are present in correct order. Use a mock LLM provider returning a fixed response if no API key is available.
- **Smoke Test Classification:** Model
- **Suggested Commit Message:** `feat: fill ChatThreadService stubs with LLM integration and streaming renderer`

---

### [x] Step 3: Structural Refactoring — Split ChatThreadService into Focused Modules
- **Goal:** Split oversized ChatThreadService (634 lines) into focused, single-concern service classes without changing behavior.
- **Actions:**
  - Extract from ChatThreadService into new files:
    * `ChatThreadLifecycleService.cs` — CreateThreadAsync, GetThreadAsync, GetPermanentThreadsAsync, GetTransientThreadsAsync, SoftDeleteThreadAsync, RestoreThreadAsync, PermanentDeleteThreadAsync, ElevateToPermanentAsync + private helpers for persona resolution
    * `ChatMessageService.cs` — SendMessageAsync, EditMessageAsync, DeleteMessageAsync + cost calculation + usage recording helpers
    * `ChatBranchService.cs` — GetActiveBranchMessagesAsync, SetActiveBranchAsync, GetBranchCountAsync, GetChatTreeAsync, SearchMessagesAsync
    * `ChatDraftService.cs` — SaveDraftAsync, GetDraftAsync, DeleteDraftAsync
    * ChatThreadService.cs remains as the orchestrator that composes all 4 sub-services together and implements IChatThreadService by delegating to them. The RegenerateAsync and ContinueGenerationAsync methods also delegate.
  - ChatTitleGenerator.cs and MarkdownStreamRenderer.cs stay as separate files (already extracted).
- **Unit Tests to Write:** None — pure structural change, existing tests cover behavior.
- **Integration Tests to Write:** None — no infrastructure changes.
- **Live Smoke Test (Mandatory):** Run full test suite — verify zero failures (716 unit + 43 integration). Check that all imports resolve correctly (build succeeds).
- **Smoke Test Classification:** Model (self-verifiable via terminal).
- **Suggested Commit Message:** `refactor: split ChatThreadService into focused modules (Lifecycle, Message, Branch, Draft)`

---

### [x] Step 4: ChatThreadViewModel — Multi-Tab Architecture & Chat State Management
- **Goal:** Build the complete `ChatThreadViewModel` with multi-tab management, message sending, streaming state, persona switching, tool/skill toggles, and auto-save drafts. Implement `MainWindow` tab bar integration with chat tabs managed via `ObservableCollection<ChatTabItem>`. Wire all toolbar buttons to ViewModel commands. Integrate `WeakReferenceMessenger` for cross-tab completion alerts.
- **Actions:**
  - Create [`ChatTabItem`](src/MySecondBrain.Core/Models/ChatTabItem.cs): wraps `ChatThread` with `ObservableCollection<Message> Messages`, `bool IsStreaming`, `string TextboxContent`, `int CursorPosition`, `double ScrollOffset`
  - Implement `ChatThreadViewModel`:
    - `ObservableCollection<ChatTabItem> ChatTabs`, `ChatTabItem? ActiveTab`
    - `[RelayCommand] NewChatAsync()`: creates thread via service, adds tab, selects it
    - `[RelayCommand] CloseTabAsync(ChatTabItem)`: confirmation if streaming, removes tab
    - `[RelayCommand] SendMessageAsync()`: validates input, calls `_chatService.SendMessageAsync`, manages streaming state
    - `[RelayCommand] StopGeneration()`: cancels CTS, preserves partial response
    - `[RelayCommand] RegenerateAsync()`: regenerates last assistant message
    - `[RelayCommand] ContinueGenerationAsync()`: continues from last assistant message
    - `[ObservableProperty]` for: `ActivePersona`, `PersonaList`, `ToolToggles`, `SkillToggles`, `MemoryEnabled`, `ThinkingEnabled`, `IsMuted`, `IsStreaming`, `StreamingContent`, `ThinkingContent`, `ContextTokens`, `ContextMaxTokens`, `CumulativeCost`
    - `[RelayCommand]` for: `SwitchPersona`, `ToggleThinking`, `ToggleMute`, `ToggleTool`, `ToggleSkill`, `ToggleMemory`, `IncreaseFont`, `DecreaseFont`, `ToggleTheme`, `TogglePinWindow`
    - Auto-save drafts: `PeriodicTimer` every 5 seconds saves `ActiveTab.TextboxContent`
    - Cross-tab alert: subscribe to `GenerationCompletedMessage` via `WeakReferenceMessenger`, set `HasCompletionAlert` on inactive tab
  - Modify [`MainWindow.xaml`](src/MySecondBrain.UI/MainWindow.xaml): replace static placeholder with data-bound `TabControl` (or custom `ItemsControl` tab bar) in center area Row 0, bind `ItemsSource="{Binding ChatThreadViewModel.ChatTabs}"`, tab header template with title + close button + green dot indicator
  - Modify [`MainWindow.xaml.cs`](src/MySecondBrain.UI/MainWindow.xaml.cs): keyboard shortcut handlers (Ctrl+N, Ctrl+W, Ctrl+Shift+T, Ctrl+Tab), delegate to ChatThreadViewModel commands
  - Wire `MainWindowViewModel` to expose `ChatThreadViewModel` for tab bar binding
- **Unit Tests to Write:**
  - `ChatThreadViewModelTests.cs`: Expand existing tests — `NewChatAsync_CreatesTab`, `CloseTab_RemovesTab`, `SendMessage_CallsService`, `StopGeneration_CancelsAndPreserves`, `SwitchPersona_UpdatesActivePersona`, `ToggleThinking_UpdatesFlag`, `AutoSaveDraft_PersistsOnTimer`, `ReopenLastClosed_RestoresTab`, `CloseConfirmation_ShowsWhenStreaming`
- **Integration Tests to Write:**
  - `ChatWorkflowIntegrationTests.cs`: Test full ViewModel→Service→Repository chain for message send
- **Live Smoke Test (Mandatory):** Launch the application. (1) Press Ctrl+N → verify new chat tab appears with Persona picker. (2) Select a Persona → verify chat header shows Persona name. (3) Type a message in the textbox and click Send → verify user message appears in conversation area. (4) Press Ctrl+N again → verify second tab opens. (5) Switch between tabs → verify each maintains independent state. (6) Close a tab via X button → verify tab is removed. (7) Press Ctrl+Shift+T → verify last closed tab reopens. (8) Toggle Thinking button → verify visual state change. (9) Toggle Mute button → verify visual state change. (10) Verify font size A⁻/A⁺ buttons adjust displayed size.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: implement ChatThreadViewModel with multi-tab architecture and chat state management`

---

### [x] Step 5: Conversation View — VirtualizingStackPanel + Markdown Rendering Engine
- **Goal:** Replace the static sample messages in `ChatView.xaml` with a data-bound `VirtualizingStackPanel`-based `ItemsControl`. Implement the full Markdig→WPF FlowDocument rendering pipeline by filling all 8 `IContentBlockRenderer` stubs. Integrate AvalonEdit's `HighlightingManager` for syntax highlighting in code blocks (100+ languages). Render all Markdown constructs: headings H1-H6, bold, italic, inline code, fenced code blocks, bulleted/numbered lists, links, tables, blockquotes, horizontal rules, images.
- **Actions:**
  - Create [`MarkdownHelper`](src/MySecondBrain.Core/Utilities/MarkdownHelper.cs): configure Markdig pipeline with all extensions (tables, task lists, footnotes, emoji, auto-links, etc.), provide `Parse()` and `RenderToFlowDocument()` entry points
  - Fill [`MarkdownTextRenderer`](src/MySecondBrain.UI/Controls/MarkdownTextRenderer.cs): handle `ParagraphBlock`, `HeadingBlock`, `QuoteBlock`, `ListBlock`/`ListItemBlock`, `ThematicBreakBlock`; inline elements: `LiteralInline`, `EmphasisInline`, `CodeInline`, `LinkInline`, `LineBreakInline`. Convert each to corresponding WPF `Block`/`Inline` elements (`Paragraph`, `Bold`, `Italic`, `Hyperlink`, `Run`, `List`/`ListItem`, `Section` for blockquotes, `Table`/`TableRowGroup`/`TableCell` for tables)
  - Fill [`CodeBlockRenderer`](src/MySecondBrain.UI/Controls/CodeBlockRenderer.cs): detect `FencedCodeBlock`, extract language from info string, resolve `IHighlightingDefinition` from `HighlightingManager.Instance`, apply syntax colors to `Run` elements in a `Paragraph` with monospace font. Add "Copy" button (visible on hover) and language label in header. Horizontal scrolling (no wrapping). Handle missing language → plain preformatted text.
  - Fill [`ThinkingRenderer`](src/MySecondBrain.UI/Controls/ThinkingRenderer.cs): render as collapsible `Expander` with "🧠 Thinking..." header. During streaming: collapsed by default, header shows real-time second counter. When complete: "🧠 Thinking complete (Ns)". Content rendered as plain monospace `Paragraph`.
  - Fill [`ToolCallRenderer`](src/MySecondBrain.UI/Controls/ToolCallRenderer.cs): styled `Border` card with tool icon + name + parameters. Tool results as collapsible result blocks. Parallel execution indicator: "⚡ Running N tools in parallel…"
  - Fill [`ImageRenderer`](src/MySecondBrain.UI/Controls/ImageRenderer.cs): convert `![]()` Markdown images to WPF `Image` control with click-to-enlarge
  - Fill [`CitationRenderer`](src/MySecondBrain.UI/Controls/CitationRenderer.cs): inline `[N]` markers as clickable superscript, scroll to footnote `[^N]:` via `BringIntoView()`
  - Verify [`ArtifactReferenceRenderer`](src/MySecondBrain.UI/Controls/ArtifactReferenceRenderer.cs) and [`MediaRenderer`](src/MySecondBrain.UI/Controls/MediaRenderer.cs) are functional (may have been filled in F11)
  - Modify [`ChatView.xaml`](src/MySecondBrain.UI/Views/ChatView.xaml): replace static `ItemsControl` with `ListBox` using `VirtualizingStackPanel` (`VirtualizationMode="Recycling"`), bind `ItemsSource="{Binding ActiveTab.Messages}"`. Create `MessageDataTemplateSelector` that selects between user/assistant/system message templates. Each message template contains a `FlowDocumentScrollViewer` bound to the message's rendered `FlowDocument`.
  - Create per-message action bar overlay (visible on hover): star, copy MD, copy rich, edit, delete, regenerate (assistant only), thumbs up/down
  - Create relative timestamp display ("2 min ago") with hover for full date+time
  - Create empty states: "No chats yet. Press Ctrl+N to start." / "Start typing below."
- **Unit Tests to Write:**
  - `MarkdownTextRendererTests.cs`: Test heading rendering (H1-H6 produce correct font sizes); bold/italic inline rendering; list rendering (bulleted, numbered, nested); table rendering (simple, merged cells edge case); link rendering (external URLs open browser); blockquote rendering; horizontal rule
  - `BidiHelperTests.cs`: Test Hebrew detection (pure Hebrew → RTL, pure English → LTR, mixed 40% Hebrew → RTL, mixed 20% Hebrew → LTR, empty string → LTR, code block always LTR)
- **Integration Tests to Write:**
  - `StreamingRenderIntegrationTests.cs`: Parse Markdown→FlowDocument, verify block count, inline formatting
- **Live Smoke Test (Mandatory):** Launch the application. Create a new chat. The conversation area must render sample messages with full Markdown: (1) headings H1-H6 at correct sizes, (2) **bold** and *italic* text, (3) `inline code` with monospace background, (4) fenced code blocks with syntax highlighting (verify C#, Python, JavaScript at minimum), code block copy button visible on hover, (5) bulleted lists with proper indentation, (6) numbered lists, (7) [hyperlinks](https://example.com) — clickable, (8) tables with headers and rows, (9) blockquotes with left border, (10) horizontal rules. Verify messages are visually distinct per chat theme (Classic/Compact/Bubble). Verify relative timestamps update.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: implement VirtualizingStackPanel conversation view with full Markdig-to-FlowDocument rendering`

---

### [ ] Step 6: Streaming Response Display + Auto-Scroll + Message Actions + Error Handling
- **Goal:** Implement token-by-token progressive FlowDocument rendering during LLM streaming. Implement auto-scroll behavior (pause when user scrolls up, resume button, smooth scroll-to-bottom). Implement message actions: Send/Stop button transformation, Copy MD and Copy Rich per message, Regenerate, Continue Generation. Implement error handling with specific error messages and Retry button. Display token usage and generation time per message.
- **Actions:**
  - Complete `MarkdownStreamRenderer` integration with ViewModel: ViewModel subscribes to `IAsyncEnumerable<StreamChunk>` from `ChatThreadService`, feeds chunks to renderer, renderer updates `ActiveTab.RenderedDocument` property, UI binds to it
  - Send/Stop button: Bind button `Content` and `Command` to `IsStreaming` state. When `IsStreaming=true`: button shows red "⬛ Stop" with spinner. Clicking Stop: cancel `CancellationTokenSource`, preserve partial response as `Message.Content`, button reverts to "Send"
  - Auto-scroll management: Subscribe to `ScrollViewer.ScrollChanged`. If user scrolls up during streaming: pause auto-scroll, show "Auto-scroll paused" indicator. Create [`ScrollToBottomButton`](src/MySecondBrain.UI/Controls/ScrollToBottomButton.cs): floating `Button` at bottom-right, visible when scrolled up and streaming, click smooth-scrolls to bottom
  - Copy MD: `Clipboard.SetText(message.Content)` (raw Markdown)
  - Copy Rich: Generate HTML from Markdig `Markdown.ToHtml()`, generate RTF via a simple converter or use `Clipboard.SetDataObject` with `DataFormats.Html` + `DataFormats.Rtf` + `DataFormats.Text`
  - Regenerate: `[RelayCommand]` calls `_chatService.RegenerateAsync`, replaces last assistant message content with streaming response
  - Continue Generation: visible when last message is from assistant (no trailing user message), calls `_chatService.ContinueGenerationAsync`
  - Error handling: catch exceptions from `SendMessageAsync` flow, display red error banner in assistant message area with specific message ("Rate limit exceeded", "Invalid API key", "Network error", "Request timed out") and [Retry] button. Accumulate consecutive failures → escalation message after 3 failures.
  - Token usage display: per-message footer shows "📊 1,234 prompt + 567 completion = 1,801 tokens · $0.042 · Generated in 3.2s". Chat header context bar: "12,450 / 128,000 tokens" with colored fill (green <70%, yellow 70-90%, red >90%)
  - Create [`TokenContextBar`](src/MySecondBrain.UI/Controls/TokenContextBar.xaml): custom `ProgressBar`-like control with color segments
  - Create [`TokenCountToColorConverter`](src/MySecondBrain.UI/Converters/TokenCountToColorConverter.cs): maps usage percentage to green/yellow/red brush
  - Create [`RelativeTimeConverter`](src/MySecondBrain.UI/Converters/RelativeTimeConverter.cs): maps `DateTimeOffset` to "just now", "2 min ago", "1 hour ago", "Yesterday", "Jun 15"
- **Unit Tests to Write:**
  - `MarkdownStreamRendererTests.cs`: Test progressive text accumulation builds correct FlowDocument; test cancellation mid-stream leaves partial document; test empty stream; test rapid token bursts
  - `RelativeTimeConverterTests.cs`: Test conversion for seconds ago, minutes ago, hours ago, yesterday, older dates
  - `TokenCountToColorConverterTests.cs`: Test green/yellow/red thresholds
- **Integration Tests to Write:**
  - `StreamingRenderIntegrationTests.cs`: Test full stream→render pipeline with mock LLM chunks; verify FlowDocument content matches accumulated text
- **Live Smoke Test (Mandatory):** Launch the application. (1) Send a message to a configured LLM provider — verify tokens stream progressively in the conversation view (characters appear one by one, Markdown formatting updates as blocks complete). (2) During streaming, scroll up — verify auto-scroll pauses and "Auto-scroll paused" indicator appears. (3) Click the floating ↓ scroll-to-bottom button — verify smooth scroll to latest message. (4) Click Stop during generation — verify partial response is preserved and Send button returns. (5) Hover over assistant message — verify Copy MD and Copy Rich buttons appear. (6) Click Copy MD — paste into Notepad, verify raw Markdown. (7) Click Copy Rich — paste into Word, verify formatted text. (8) Click Regenerate on last assistant message — verify new response replaces old. (9) Disconnect internet, send message — verify specific error banner with Retry. (10) Reconnect, click Retry — verify successful response.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: implement streaming response display, auto-scroll, message actions, and error handling`

---

### [ ] Step 7: Chat Header Full Layout + Chat Modes + RTL + Controls
- **Goal:** Build the complete chat header bar with all 12 elements specified in C29. Implement chat modes: Standard mode, Text Completion mode, Thinking toggle with collapsible reasoning display, Mute notifications toggle, dynamic system message editing. Implement Hebrew RTL auto-detection with per-message `FlowDirection` and code block LTR enforcement. Wire all header controls to ViewModel commands.
- **Actions:**
  - Create [`ChatHeaderBar.xaml`](src/MySecondBrain.UI/Views/ChatHeaderBar.xaml): standalone `UserControl` containing (left to right):
    1. Active Persona name (clickable `Button` → opens system message editor popover)
    2. Chat theme selector `ComboBox` (Classic/Compact/Bubble)
    3. 📡 API History `Button` (opens `_api_history.json` in file viewer tab)
    4. `TokenContextBar` control (context tokens with colored fill)
    5. Cumulative cost `TextBlock` ("$0.42 total")
    6. Source banner `Border` (only visible when chat has source context: "[Source: Word — 'document.docx']")
    7. `[Apply Latest]` `Button` (grayed out, enabled only when source HWND is valid)
    8. Spacer
    9. A⁻ `Button` | `FontSizeDisplay` `TextBlock` | A⁺ `Button`
    10. ☀/🌙 dark mode toggle `Button`
    11. 📌 pin window toggle `Button`
    12. ? help `Button` (dropdown: App Data Locations, Keyboard Shortcuts, About)
    13. ⋯ three-dot menu `Button` (Clear Conversation, Export Chat, Duplicate Chat, Chat Tree, Edit System Message, Summarize Chat, Make Temporary)
  - Implement three-dot menu as `ContextMenu` with all items from C16a. Wire each to ViewModel commands. "Clear Conversation" shows confirmation dialog, then removes all messages from thread. "Make Temporary" toggles `isTransient`. "Summarize Chat" triggers AI summarization (stub for now, full implementation in later sub-step).
  - Implement `[Apply]` button shell: always visible but grayed out with tooltip "Text action integration coming in a future update". The button exists in the layout now; validation logic is deferred to Feature 18.
  - Standard Chat Mode (E1): default, already implemented via existing message flow
  - Text Completion Mode (E2): add mode indicator label in chat header. When active, `SendMessageAsync` uses raw prompt-to-completion API instead of chat API. No conversation history sent. Add `[RelayCommand] SwitchChatMode()` that shows warning dialog about history loss
  - Thinking Toggle (E3): wire existing `ThinkingToggleBtn` to `ChatThreadViewModel.ThinkingEnabled`. When enabled during streaming: render `StreamChunk.ThinkingDelta` tokens via `ThinkingRenderer` in a collapsible block above the response. Track thinking duration with a `Stopwatch`. On completion: "🧠 Thinking complete (3.2s)"
  - Mute Toggle (E4): wire existing `MuteToggleBtn` to `ChatThreadViewModel.IsMuted`. Suppress sound notification on completion for this chat
  - Dynamic System Message Editing (E5): clicking Persona name opens a `Popup` with editable `TextBox` pre-filled with current system message. "Save" applies to subsequent messages. "Reset to Persona Default" reverts. Edited system message stored on `ChatThread.SystemMessage`
  - Create [`BidiHelper`](src/MySecondBrain.Core/Utilities/BidiHelper.cs): `static FlowDirection GetMessageFlowDirection(string content)` — counts Hebrew chars (U+0590–U+05FF), if >30% of letters → RTL. `static FlowDirection CodeBlockFlowDirection => FlowDirection.LeftToRight` (always LTR)
  - Create [`FlowDirectionConverter`](src/MySecondBrain.UI/Converters/FlowDirectionConverter.cs): `IValueConverter` that calls `BidiHelper.GetMessageFlowDirection()`
  - Apply `FlowDirection` binding on message container in `ChatView.xaml`: `FlowDirection="{Binding Content, Converter={StaticResource FlowDirectionConverter}}"`. Code blocks in `CodeBlockRenderer` hardcode `FlowDirection="LeftToRight"`
  - Wire pin window toggle: `MainWindow.Topmost = isPinned`, persist via `ISettingsRepository` key `"PinWindow"`
  - Wire help button: dropdown with three items, "App Data Locations" navigates to Settings→SystemInfo, "Keyboard Shortcuts" shows Ctrl+/ overlay, "About" shows version dialog
- **Unit Tests to Write:**
  - `BidiHelperTests.cs`: Already covered in Step 4 tests — verify Hebrew detection thresholds, code block LTR enforcement
  - `ChatThreadViewModelTests.cs`: Expand — `SwitchToTextCompletion_WarnsAboutHistoryLoss`, `ToggleThinking_UpdatesFlag`, `EditSystemMessage_SavesToThread`, `ClearConversation_RemovesMessages`, `MakeTemporary_TogglesIsTransient`, `PinWindow_TogglesTopmost`
- **Integration Tests to Write:**
  - `ChatWorkflowIntegrationTests.cs`: Test system message edit → message send uses edited prompt; test mode switch persistence
- **Live Smoke Test (Mandatory):** Launch the application. (1) Verify chat header shows all 12 elements in correct left-to-right order. (2) Click Persona name → verify popover opens with editable system message. (3) Edit system message, click Save → send a message, verify AI behavior reflects new system prompt. (4) Switch chat theme via ComboBox → verify message styling changes instantly. (5) Click 📡 API History → verify file viewer tab opens (even if empty JSON). (6) Verify context bar shows "0 / 128,000 tokens" initially. (7) Toggle ☀/🌙 → verify entire app theme switches. (8) Toggle 📌 → verify window stays on top of other apps. (9) Click ⋯ menu → verify all menu items appear. (10) Click "Clear Conversation" → verify confirmation dialog, then empty chat. (11) Toggle 🧠 Thinking → send message to model supporting thinking (e.g., Claude) → verify thinking block appears collapsible above response. (12) Type Hebrew text (e.g., "שלום עולם") → verify message aligns right (RTL). Type mixed Hebrew+English → verify segments render correctly. Verify code blocks always LTR even when containing Hebrew text.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: implement chat header full layout, chat modes, RTL support, and all header controls`

---

### [ ] Step 8: QoL Features — File Viewer Tabs, Incognito, Locked Chats, Titling, Favoriting, Cross-Tab Alerts, Message Selection, Right Panel
- **Goal:** Implement the remaining quality-of-life features: generic file viewer tabs, incognito/temporary chat toggle, locked chat encryption (AES-256-GCM), AI-powered chat auto-titling, chat summarization, message favoriting (★), cross-tab completion alert (green dot), message selection mode with bulk actions, offline/network status indicator, close confirmation during active generation, and right panel layout (Artifacts top + Chat Nav bottom with resizable divider).
- **Actions:**
  - **File Viewer Tabs (C39):**
    - Create [`FileViewerTabViewModel`](src/MySecondBrain.UI/ViewModels/FileViewerTabViewModel.cs): `FilePath`, `FileContent`, `IsReadOnly=true`, `FileType` (Text/Code/Markdown/Image)
    - Create [`FileViewerTab.xaml`](src/MySecondBrain.UI/Views/FileViewerTab.xaml): TabItem with 📄 icon + "Read-Only" badge. Content: `FlowDocumentScrollViewer` for text/code/markdown, `Image` with zoom/pan for images. Syntax highlighting via `CodeBlockRenderer` for code files.
    - Open via: Ctrl+O (file picker), drag-drop onto tab bar, click image thumbnail, API History button
    - Tab behavior: same as chat tabs (reorder, close, reopen via Ctrl+Shift+T)
  - **Incognito/Temporary Chat (C30):**
    - Three-dot menu → "Make Temporary": sets `ChatThread.IsTransient = true`, 🕶️ indicator on tab
    - Three-dot menu → "Make Permanent": reverse (elevation)
    - Auto-elevation on first user reply (already in ChatThreadService)
  - **Locked Chats (C31):**
    - Create [`LockedChatService`](src/MySecondBrain.Services/Encryption/LockedChatService.cs): `LockChatAsync(threadId, password)`, `UnlockChatAsync(threadId, password)`, `IsLocked(threadId)`. Uses `IChatEncryptionService` for AES-256-GCM. Generates 128-bit salt per chat. Encrypts all message content before storage, decrypts on retrieval.
    - Create [`LockedChatPasswordDialog.xaml`](src/MySecondBrain.UI/Views/LockedChatPasswordDialog.xaml): modal dialog with password input, "Lock"/"Unlock" button, warning text "If you lose this password, chat content cannot be recovered."
    - Create [`LockedChatViewModel`](src/MySecondBrain.UI/ViewModels/LockedChatViewModel.cs): password input, confirm, error states
    - Three-dot menu → "Lock Chat": opens password dialog, encrypts messages, shows 🔒 indicator on tab
    - Sidebar "🔒 Reveal Locked Chats" button: shows hidden locked chats
    - Wrong password: "Incorrect password." — no recovery
    - "Hide locked chats from sidebar" setting: if enabled, locked chats hidden until revealed via password
  - **Auto Chat Titling (C7):**
    - Integrate `ChatTitleGenerator` into `SendMessageAsync`: after first assistant response, fire-and-forget title generation. Update `ChatThread.Title` when complete.
    - Manual edit: click title in chat header → inline `TextBox` → Enter to save
    - Title display in tab: truncated if >30 chars
  - **Chat Summarization (C32):**
    - Three-dot menu → "Summarize Chat": sends all messages to LLM with summarization prompt, returns summary as artifact or inserts into chat
  - **Message Favoriting (C33):**
    - Star (★/☆) toggle button on each message (visible on hover)
    - `Message.IsFavorited` boolean, persisted via repository
    - Filter in Chat Nav: "Favorited Only" toggle
  - **Cross-Tab Completion Alert (C35):**
    - When generation completes in background tab: pulsing green dot on tab header, brief "✓" overlay
    - Sound notification (if not muted globally or per-chat)
    - Configurable in Settings (A4): `CrossTabCompletionAlert` setting
    - Uses `WeakReferenceMessenger` — `GenerationCompletedMessage` sent from `ChatThreadService`
  - **Message Selection Mode (C18):**
    - Three-dot menu → "Select Messages": checkboxes appear on left edge of each message on hover
    - Click checkbox to select/deselect
    - Bulk actions bar at top: "[N] selected — Copy Selected | Delete Selected | Quote Selected | Cancel"
    - Visual: selected messages have highlighted border
  - **Offline/Network Indicator (C19):**
    - `NetworkChange.NetworkAvailabilityChanged` event → update status
    - Small colored dot in status bar: green (online), yellow (slow), red (offline)
    - When offline: yellow banner below chat header: "You are offline. AI responses are unavailable."
    - Auto-detect reconnection; banner auto-dismisses
  - **Close Confirmation (C20):**
    - `MainWindow.OnClosing` override: check if any chat is streaming (`ChatTabs.Any(t => t.IsStreaming)`)
    - Show confirmation dialog: "A response is still being generated. Are you sure you want to close?" Options: "Wait for response" / "Close anyway"
    - Same check for individual tab close
  - **Right Panel Layout (C37):**
    - Verify existing right panel (Column 4 in MainWindow) has two vertically stacked resizable sections
    - Top: "Artifacts" header + placeholder (will be populated by F16)
    - Horizontal `GridSplitter`
    - Bottom: "Chat Navigation" header + message list (will be populated by F14)
    - Both sections collapsible via right panel toggle button
  - **Empty/Loading/Error States:**
    - Empty: "No chats yet. Press Ctrl+N to start a new conversation."
    - Cleared chat: Persona info card
    - Chat history loading: skeleton placeholder messages (gray bars)
    - Chat not found: "This chat no longer exists."
- **Unit Tests to Write:**
  - `LockedChatServiceTests.cs`: Test LockChatAsync encrypts messages; UnlockChatAsync decrypts correctly; wrong password fails; salt generation is unique per chat
  - `ChatTitleGeneratorTests.cs`: Already covered in Step 2
  - `ChatThreadViewModelTests.cs`: Expand — `ToggleFavorite_UpdatesFlag`, `SelectMessages_ShowsBulkBar`, `CloseConfirmation_ShowsWhenStreaming`, `NetworkOffline_ShowsBanner`, `LockChat_EncryptsContent`
- **Integration Tests to Write:**
  - `ChatWorkflowIntegrationTests.cs`: Test lock/unlock cycle with real encryption; test summarization
- **Live Smoke Test (Mandatory):** Launch the application. (1) Press Ctrl+O → select a text file → verify file viewer tab opens with 📄 icon, "Read-Only" badge, and syntax-highlighted content. (2) Drag a .md file onto the tab bar → verify it opens as rendered Markdown in file viewer tab. (3) Click ⋯ → "Make Temporary" → verify 🕶️ icon appears on tab. Click again → "Make Permanent" → icon disappears. (4) Click ⋯ → "Lock Chat" → enter password → verify 🔒 icon and re-open requires password. Wrong password → "Incorrect password." (5) Create new chat, type message, send → verify auto-generated title appears after assistant responds. Click title → edit → Enter → verify new title. (6) Click ⋯ → "Summarize Chat" → verify summary appears. (7) Hover over message → click ★ → verify star fills. Click again → star empties. (8) Open two chat tabs, send message in background tab → verify green dot appears on inactive tab when generation completes. (9) Click ⋯ → "Select Messages" → select 2 messages → verify "2 selected" bulk bar with Copy/Delete/Quote buttons. (10) Disconnect internet → verify red dot in status bar and yellow offline banner. Reconnect → verify auto-dismiss. (11) Start generation, immediately try to close tab → verify confirmation dialog. Click "Wait" → dialog closes. (12) Verify right panel divider is draggable between Artifacts (top) and Chat Nav (bottom) sections.
- **Smoke Test Classification:** HUMAN/SHT REQUIRED
- **Suggested Commit Message:** `feat: implement file viewer tabs, locked chats, auto-titling, favoriting, cross-tab alerts, and quality-of-life features`

---

### [ ] Step 9: E2E Tests + Integration Tests + Visual Polish
- **Goal:** Create comprehensive E2E test suite covering all Studio Chat workflows. Write integration tests for cross-component chat scenarios. Polish visual details and edge cases. Verify the full feature against all acceptance criteria from vision docs.
- **Actions:**
  - Create [`StudioChatE2ETests.cs`](tests/e2e/MySecondBrain.Tests.E2E/StudioChatE2ETests.cs): ~15 test cases following the self-cleaning pattern (create→verify→delete via 🗑️):
    1. `CreateNewChat_ShouldShowEmptyConversationView`
    2. `SendMessage_ShouldDisplayUserAndAssistantMessages`
    3. `MultipleTabs_ShouldMaintainIndependentState`
    4. `CloseTab_ShouldRemoveTab`
    5. `ReopenLastClosedTab_ShouldRestoreTab`
    6. `SwitchChatTheme_ShouldChangeMessageAppearance`
    7. `AdjustFontSize_ShouldUpdateMessageText`
    8. `ToggleDarkMode_ShouldSwitchTheme`
    9. `ToggleThinking_ShouldShowThinkingBlock`
    10. `MessageFavoriting_ShouldToggleStar`
    11. `ClearConversation_ShouldEmptyChat`
    12. `CopyMarkdown_ShouldCopyRawMarkdown`
    13. `ErrorHandling_ShouldDisplayRetryButton`
    14. `PinWindow_ShouldKeepWindowOnTop`
    15. `HebrewRtl_ShouldRenderRightToLeft`
  - Add `AutomationProperties.AutomationId` to all interactable elements in chat UI (buttons, inputs, message areas) following the naming conventions in e2e-authoring-guide.md
  - Create [`ChatWorkflowIntegrationTests.cs`](tests/integration/MySecondBrain.Tests.Integration/ChatWorkflowIntegrationTests.cs): test full send→stream→render→persist pipeline with real SQLite and mock LLM
  - Create [`StreamingRenderIntegrationTests.cs`](tests/integration/MySecondBrain.Tests.Integration/StreamingRenderIntegrationTests.cs): test progressive Markdown rendering with mock stream chunks
  - Visual polish pass:
    - Ensure all message templates are pixel-perfect across all 3 chat themes
    - Verify spacing, padding, and alignment match the HTML mock reference
    - Verify code block copy button positioning and hover behavior
    - Verify smooth scroll transitions
    - Verify RTL messages have correct text alignment and padding
  - Edge case testing:
    - Very long messages (>10K chars) — verify VirtualizingStackPanel handles efficiently
    - Rapid tab switching during streaming — verify no crashes
    - Empty messages (send with no text) — verify send button is disabled
    - Maximum context window exceeded — verify hard stop / sliding window behavior
    - Hebrew+code block mixed content — verify code block stays LTR
    - Multiple consecutive file viewer tabs — verify memory usage
- **Unit Tests to Write:** None — all unit tests covered in previous steps
- **Integration Tests to Write:**
 - `ChatWorkflowIntegrationTests.cs` (expanded from Steps 1-2): full end-to-end with all QoL features
 - `StreamingRenderIntegrationTests.cs`: progressive rendering with various Markdown constructs
- **Live Smoke Test (Mandatory):**
 - **Part A (Model):** Run `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug --verbosity normal`. All 15 new Studio Chat E2E tests must pass (0 failures, 0 skipped). Run `dotnet test tests/integration/MySecondBrain.Tests.Integration --configuration Debug`. All integration tests must pass. Run `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug`. All existing + new unit tests must pass. Total test count should be 77+ E2E tests (62 existing + 15 new).
 - **Part B (HUMAN/SHT REQUIRED — Visual Polish):** Launch the application. (1) Verify message spacing and padding match the HTML mock across all 3 chat themes (Classic/Compact/Bubble). (2) Hover over code block — verify copy button appears only on hover. (3) Verify scroll-to-bottom button uses smooth animation. (4) Verify RTL messages have correct right alignment and padding mirroring LTR. (5) Verify empty states display centered text with muted color. (6) Verify dark/light mode transition is smooth with no flicker. (7) Verify font size changes apply instantly to all message text.
- **Smoke Test Classification:** Model (Part A) + HUMAN/SHT REQUIRED (Part B)
- **Suggested Commit Message:** `feat: add comprehensive E2E and integration tests for Studio Chat workspace`

---

## 6. Shared Technical Context

- **Project Test Commands:**
  - `dotnet test tests/unit/MySecondBrain.Tests.Unit --configuration Debug`
  - `dotnet test tests/integration/MySecondBrain.Tests.Integration --configuration Debug`
  - `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug --verbosity normal`
- **Build Command:** `dotnet build MySecondBrain.sln --configuration Debug`
- **Key NuGet Dependencies (already in .csproj):** `Markdig`, `AvalonEdit`, `CommunityToolkit.Mvvm`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.Extensions.DependencyInjection`, `Serilog`
- **New NuGet Dependency:** `Microsoft.Web.WebView2` (for artifacts panel — already in F11)
- **Database:** SQLite at `%LOCALAPPDATA%\MySecondBrain\msb.db`. E2E tests use `MSB_DB_PATH` env var for isolation.
- **DI Registration Pattern:** All new services register in [`DependencyInjectionConfig.cs`](src/MySecondBrain.UI/DependencyInjectionConfig.cs). Multi-implementation interfaces use repeated `AddSingleton<TInterface, TImpl>()`. ViewModels are Transient.
- **XAML Naming Convention:** All interactable elements need `AutomationProperties.AutomationId` for E2E testability. Follow pattern: `{Action}{Entity}Button`, `{Entity}{Field}Input`, `{Feature}View`.
- **Messenger Pattern:** Cross-VM communication via `WeakReferenceMessenger.Default`. Registration in constructor. Key messages: `GenerationCompletedMessage`, `ChatThreadCreatedMessage`, `ChatThreadDeletedMessage`.
- **Stub Convention:** Services/repos initially stubbed with `Task.FromResult<T?>(null)` / `Task.CompletedTask`. This feature fills those stubs.
- **File-Scoped Namespaces:** All C# files use `file_scoped` namespaces per `.editorconfig`.
- **Step 1 — ChatThread Entity Fields (added):** IsFavorite, IsPinned, IsArchived, ColorLabel, Tags (JSON string), FolderId, IsLocked, LockSalt, LockNonce. Migration: `20260626104420_AddChatOrganizationFields`.
- **Step 1 — Message Entity Fields (added):** IsFavorited, ThinkingContent. New index: `IX_Messages_IsActiveBranch`.
- **Step 1 — Repository Mapping:** ChatThreadRepository.UpdateAsync/MapToDomain/MapToEntity updated with 9 new fields. MessageRepository.UpdateAsync/MapToDomain/MapToEntity updated with 6 fields (RawContent, EstimatedCost, GenerationTimeMs, Feedback, IsFavorited, ThinkingContent).
- **Step 1 — Integration Tests:** `ChatWorkflowIntegrationTests.cs` — FullWorkflow (create→messages→active branch→FTS5→soft-delete→trash), BranchingWorkflow (create branches→switch active), ThreadWithLockFields (lock/unlock cycle).
- **Step 1 — Unit Tests:** 6 new tests in `ChatMessageRepositoryTests.cs` for organization fields (CreateWithOrgFields, UpdateOrgFields, ResetLockFields, CreateWithNewFields, UpdateNewFields, SearchAlongsideNewFields). EntitySchemaTests updated (ChatThread: 26 props, Message: 19 props).
- **Step 2 — ChatThreadService:** 634 lines. Full implementation of all 18 IChatThreadService methods. `SendMessageAsync` — creates user+assistant messages, resolves persona/modelConfig via repo, calls ILLMProviderService.ChatStreamAsync, accumulates response, preserves partial on OperationCanceledException, auto-titles via ChatTitleGenerator, records UsageRecord. `RegenerateAsync` — deactivates old message, creates new version with VersionNumber+1. `ContinueGenerationAsync` — preserves original in RawContent, appends continuation. `GetActiveBranchMessagesAsync` — delegates to MessageRepository.GetActiveBranchAsync. Draft CRUD via AppDbContext.MessageDrafts (upsert pattern). Constructor takes 8 dependencies.
- **Step 2 — MarkdownStreamRenderer:** 123 lines. Progressive FlowDocument rendering via Markdig + IContentRendererRegistry. AttachDocument/DetachDocument pattern. AppendToken accumulates buffer, re-parses full buffer each call. Parse failures silently swallowed (unclosed code fence mid-stream). Markdig pipeline via MarkdownHelper.Pipeline.
- **Step 2 — ChatTitleGenerator:** 86 lines. AI-powered title generation via lightweight ChatAsync call. Strips quotes. Falls back to first 50 chars of user message on failure, "New Chat" for empty input.
- **Step 2 — MarkdownHelper:** 36 lines. Shared Markdig pipeline with UseAdvancedExtensions, UseEmojiAndSmiley, UseAutoLinks, UseBootstrap. Parse() and ToHtml() methods.
- **Step 2 — Unit Tests:** ChatThreadServiceTests (11 tests: CreateThread, SendMessage normal+cancellation+autoTitle, GetActiveBranch, Regenerate normal+nonAssistant guard, ContinueGeneration, ElevateToPermanent, Draft CRUD, ChatTree, Search). ChatTitleGeneratorTests (9 tests: generation, quote stripping, LLM failure, empty response, whitespace, >100 char guard, short message). MarkdownStreamRendererTests (9 tests: accumulation, attach/detach, empty/null, malformed recovery).
- **Step 2 — Integration Tests:** ChatWorkflowIntegrationTests expanded: SendMessage_FullFlow (creates thread→sends via mock LLM→verifies user+assistant+activity), DraftWorkflow (save→update→delete).
- **Initial State:** ChatView.xaml has visual layout with static placeholders. ChatThreadService, ChatThreadRepository, MessageRepository are all stubs returning null/empty. Content block renderers are stubs. ChatThreadViewModel has basic persona/tool/skill management but no chat/message/tab functionality. The shell (MainWindow, sidebar, right panel) is fully functional.
