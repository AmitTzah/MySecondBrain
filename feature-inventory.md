# Feature Inventory

## Core Features

Features without which the app has no reason to exist. These MUST be present in the initial release.

### A. Settings & Configuration

- **A1. Global Settings Screen:** A dedicated settings screen accessible from the Studio UI. Contains all global configuration options organized into sections (Providers, Profiles, Appearance, Wiki, Backup, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Maintenance).

- **A2. Default Profile Selection:** The user selects which model profile is automatically assigned when creating a new chat. All new chats start with this profile unless the user explicitly changes it.

- **A3. Appearance Settings:** The user selects from at least three visual themes for the chat view (e.g., Classic, Compact, Bubble). Customizable text font family, font size, and font weight for chat messages.

- **A4. Notification Settings:** Toggle sound notification when an assistant message completes streaming. Option to disable streaming entirely (responses appear all at once when complete). Per-chat mute toggle available in the textbox toolbar.

- **A5. Dark Mode / Light Mode:** The user can toggle between dark mode and light mode for the entire application UI. Independent of the chat visual themes (A3), which control message layout only. Dark mode is the default.

- **A6. Startup Behavior:** Option to launch MySecondBrain on Windows startup. Option to restore the last session (reopen all chats and tabs that were open when the app was last closed).

- **A7. Auto-Update:** The app checks for updates automatically. The user can configure: check on startup, check periodically, or manual check only. When an update is available, the user is notified and can choose to install now or later.

- **A8. Onboarding Wizard:** On first launch, a guided setup wizard walks the user through: (1) add first API key and provider, (2) create first model profile, (3) select wiki directory, (4) configure hotkeys. Each step can be skipped. The wizard can be re-launched from Settings at any time.

- **A9. Database Maintenance:** A "Compact Database" button in Settings runs SQLite VACUUM to reclaim disk space after large deletions (especially from 7-day transient cleanup). The app displays current database size before and after compaction.

- **A10. Speech-to-Text (STT) Provider:** The user configures a dedicated STT provider and model for voice dictation (C21). This is separate from text-generation Model Configurations (B2). Supported STT providers: OpenAI Whisper API, local Whisper model, or any OpenAI-compatible STT endpoint. The STT provider is a global setting, not per-Persona.

### B. Model Configurations & Personas

The app uses a two-layer model for organizing AI behavior: **Model Configurations** (hardware/engine — what model runs) and **Personas** (behavior — how the model behaves).

- **B1. API Key Management:** The user can add, view, edit, and delete API keys for supported providers. Keys are encrypted at rest using Windows DPAPI (Data Protection API), which ties decryption to the local Windows user account. When adding a key, a "Test Key" button validates the key against the provider's API and shows success/failure. Built-in provider types: OpenAI, Anthropic (Claude), Google, DeepSeek, Xiaomi MiMo, Moonshot, Mistral, "OpenAI-Compatible" (user specifies endpoint URL).

- **B2. Model Configurations (The Engine):** The user creates named Model Configurations. Each defines ONLY hardware/model parameters: display name, provider, API key (or use global), model identifier, temperature, max output tokens, max context window, thinking on/off, pricing (cost per 1K tokens), and Context Overflow Strategy (see B8). Model Configurations do NOT contain system prompts — behavior is defined separately by Personas. Configurations are managed in Settings.

- **B3. Personas (The Behavior):** The user creates named Personas. Each Persona defines: display name (e.g., "Python Expert", "Writing Coach"), a system prompt, a default Model Configuration (from B2), and a default chat mode (Standard or Text Completion). Personas replace the old separate concepts of "system message templates" and "chat templates" — everything behavioral is in one place. Personas are managed in Settings.

- **B4. Persona Selection per Chat:** When creating a new chat (Ctrl+N), the user picks a Persona from a list. The new chat is pre-configured with that Persona's system prompt, Model Configuration, and chat mode. The user can switch Persona at any time from the textbox toolbar — this changes the system prompt and Model Configuration for all subsequent messages. The Persona dropdown shows recently used Personas at the top.

- **B5. Local Open-Source Model Support:** The user can connect to locally running models via an "OpenAI-Compatible" provider pointing to a local endpoint (e.g., localhost:1234).

- **B6. "OpenAI-Compatible" Provider Type:** A generic provider type where the user specifies: provider display name, API endpoint URL, API key (optional for local models). This enables any OpenAI-API-compatible service or local model server.

- **B7. Auto-Fetch Available Models:** When the user adds an API key for a provider, the app can fetch the list of available models from that provider's API. The model list is displayed as a dropdown when creating or editing a Model Configuration. The user can also manually enter a model identifier. The fetched model list is cached and can be refreshed manually. For local or OpenAI-compatible providers, the user manually enters the model identifier.

- **B8. Context Window Overflow Strategy:** Per Model Configuration, the user selects what happens when the conversation context approaches the model's max context window: (1) Sliding Window — automatically drops the oldest messages (behind the system prompt), (2) Hard Stop — prevents sending when the limit is reached, showing a warning, or (3) Auto-Summarize — an AI agent silently summarizes the oldest 50% of the chat and replaces it with a summary block. Configurable per Model Configuration and changeable mid-chat.

### C. Studio Chat Workspace

- **C1. Conversation View:** Full scrolling message history displaying all messages in the current ChatThread. Messages are visually distinguished according to the selected theme (see A3). Each message shows the model/profile that generated it (if different from the current profile) and a timestamp (hover to see full date+time, always visible relative time like "2 min ago"). The user can choose from at least three visual themes in Settings: Classic (user messages aligned right, assistant left), Compact (minimal spacing, subtle alignment), Bubble (chat-bubble style with colored backgrounds).

- **C2. Message Content Rendering:** Assistant messages render: Markdown (headings, bold, italic, inline code, code blocks with syntax highlighting, bulleted and numbered lists, links, tables, blockquotes, horizontal rules), images (rendered inline at full width up to a max size, clickable to view full resolution), audio (inline mini player with play/pause/seek), and video (inline embedded player). Media rendering adapts to the message width.

- **C3. Code Block Rendering:** Fenced code blocks display with syntax highlighting based on the declared language. A copy button appears on hover at the top-right of each code block.

- **C4. Streaming Response Display:** Assistant responses render token-by-token as they are generated, giving real-time visual feedback. Markdown and code blocks render progressively. When generation completes, the message footer displays the total generation time (e.g., "Generated in 3.2s").

- **C5. Message Actions:** Send message (Enter or click Send button). Stop generation (click Stop button while AI is generating). Regenerate last assistant response (click Regenerate button).

- **C5a. Conditional [Apply] Button (Tier 1 Elevation Only):** When a Studio chat originated from a Tier 1 hotkey text transformation that captured highlighted text from a source application, the chat header displays a source indicator banner: "[Source: {App Name} — '{Document Title}']" alongside an **[Apply Latest]** button. Additionally, each assistant message that was a direct transformation of the captured text displays an **[Apply]** button alongside Copy, Regenerate, and other per-message actions. Messages from follow-up queries (not direct transformations) do not show [Apply]. If the source application window has been closed, [Apply] and [Apply Latest] are grayed out with the tooltip: "Source application is no longer available." Chats created directly in Studio or elevated from Tier 2 (Command Bar) never show [Apply] buttons. See P3 for the full apply mechanism and fallback behavior.

- **C6. Copy:** Each message (user and assistant) has a visible "Copy" button that places BOTH raw Markdown and Rich Text (HTML/RTF) onto the Windows clipboard simultaneously. The destination application (Word, Outlook, VS Code, browser) automatically selects the best format upon pasting. Additionally, the user can copy the entire conversation as text or Markdown via a menu option.

- **C7. Chat Titling:** Each chat has a title. Auto-generated from the first user message (using AI summarization). The user can manually edit the title at any time by clicking it in the sidebar or chat header.

- **C8. Continue Generation:** A "Continue" button appears near the last assistant message (adjacent to the Regenerate button) when the conversation's last message is from the assistant. This occurs naturally when the AI response ends mid-sentence, or when the user deletes their last user message leaving an assistant message as the final message. Sends a continue request to models that support assistant prefix continuation.

- **C9. Drag & Drop Files/Media:** The user can drag and drop files, images, video files, or audio files into the chat input area. Images are sent to the AI for vision-capable models. Video and audio files are included as input for multi-modal models that support them. Text files are read and their content included in the prompt. Other file types are attached and their metadata (filename, type, size) is included in the prompt context.

- **C10. Multiple Chat Tabs:** The user can open multiple chats in tabs within the Studio window, similar to a browser. Each tab is an independent ChatThread. Tabs can be reordered by drag-and-drop, closed, and reopened from the sidebar. The tab bar appears above the chat view.

- **C11. Token Usage & Context Display:** Each assistant message displays the token count used (prompt tokens + completion tokens) and estimated cost (if pricing is configured per B2). The chat header displays: (a) Current Context Size — the total token count of the current branch's message history, shown alongside the model's maximum context window (e.g., "12,450 / 128,000 tokens"), and (b) Cumulative Chat Cost — the total estimated cost across ALL branches. Context size is calculated using a local tokenizer library providing real-time feedback as the user types, rather than waiting for API response headers.

- **C12. Keyboard Shortcuts (Studio):** The following keyboard shortcuts operate within the Studio window: Ctrl+N = New Chat, Ctrl+W = Close Current Tab, Ctrl+Shift+T = Reopen Last Closed Tab, Ctrl+Tab = Next Tab, Ctrl+Shift+Tab = Previous Tab, Ctrl+F = Search Within Current Chat, Ctrl+Shift+F = Global Search Across All Chats, Ctrl+S = Export Current Chat. A keyboard shortcut reference is accessible from the Help menu or by pressing Ctrl+/ — it displays a searchable overlay listing all available shortcuts.

- **C13. Resizable Panels:** The sidebar width, artifact/side panel width, and chat navigation bar width are all adjustable by dragging the divider between panels. Minimum and maximum widths are enforced. Panel sizes are remembered across sessions.

- **C14. Error Handling & Retry:** When an API call fails (network error, rate limit, authentication error, provider error), the assistant message area displays the specific error message with a "Retry" button. Clicking Retry re-attempts the exact same request. Consecutive failures show an escalating message suggesting the user check their API key, network connection, or provider status.

- **C15. Scroll-to-Bottom Button:** When the user has scrolled up to read older messages and a new assistant response is streaming in, a floating "Scroll to bottom" button appears at the bottom-right of the chat view. Clicking it smoothly scrolls to the latest message. The button disappears when already at the bottom.

- **C16. Clear Conversation:** The user can clear all messages from a chat without deleting the chat itself. This resets the conversation history while preserving the chat in the sidebar with its title, tags, and settings. A confirmation dialog warns: "This will remove all messages from this chat. The chat will remain in your sidebar."

- **C17. Auto-Scroll Behavior:** When the user scrolls up to read older messages during active generation, auto-scroll is automatically paused with an indicator: "Auto-scroll paused." The user can click it or scroll to the bottom to resume. Auto-scroll also handles media loading — when an AI generates or the user uploads an image/video, the sudden height change from media rendering does not jar the reading position; the viewport adjusts smoothly.

- **C18. Message Selection Mode:** The user can enter a selection mode (via checkbox on each message or a toolbar button) to select multiple messages. Once selected, bulk actions are available: Copy Selected, Delete Selected, Quote Selected (inserts all as quoted blocks).

- **C19. Offline/Network Status Indicator:** A small indicator in the Studio window (e.g., status bar or top bar) shows the current network status. Green = online, Yellow = slow connection, Red = offline. When offline, a banner appears: "You are offline. AI responses are unavailable until connection is restored."

- **C20. Close Confirmation with Active Generation:** When the user attempts to close a chat tab or the Studio window while an AI response is actively generating, a confirmation dialog appears: "A response is still being generated. Are you sure you want to close?" Options: "Wait for response" or "Close anyway."

- **C21. Audio Input (Microphone):** A microphone button in the textbox toolbar enables voice dictation. When active, the user's speech is captured and sent to the configured STT Provider (A10) for transcription. The transcribed text appears directly in the textbox. The user can edit the transcribed text before sending.

- **C22. Camera Capture:** A camera button in the textbox toolbar allows the user to take a photo using their webcam. The captured image is immediately attached to the current message and sent to the AI (for vision-capable models).

- **C23. Pin Window / Always on Top:** A toggle in the Studio window header pins the window to stay on top of other applications. Useful when referencing the Studio Chat while working in VS Code, Word, or other applications. Toggle is remembered across sessions.

### D. Message Manipulation & Branching

- **D1. Edit Any Past Message:** The user can edit any past message — both user messages and assistant messages. Editing opens the message content in an editable field. The user chooses between two modes: "Edit in Place" (overwrites the message without creating a branch) or "Edit as Branch" (creates a new version branch at that point). Regardless of mode, all subsequent LLM calls use the edited message history — the AI sees the updated conversation.

- **D2. Delete Any Past Message:** The user can delete any past message. Deleting a message removes it from the current conversation history. Subsequent LLM calls use the updated history (without the deleted message). If the message had branches, the branch data is preserved and can still be navigated to.

- **D3. Branch Navigation:** Messages that have been edited display a branch indicator (e.g., "2/3" meaning "version 2 of 3"). The user can cycle through branches using arrow buttons on the message. Selecting a different branch re-renders all subsequent messages in the conversation according to that branch.

- **D4. Chat Tree Visualization:** A visual tree/graph view accessible from each chat showing all branches and message paths. Nodes represent messages; edges show the conversation flow. The active branch is highlighted. The user can click any node to navigate to that branch point.

- **D5. Quote from Chat:** The user can select text from any past message and click "Quote" to insert it as a quoted block in the current message input.

- **D6. Chat Navigation Bar:** A navigation panel (collapsible) showing an overview of all messages in the current branch as a scrollable list. Each entry shows a message preview and the message number. Clicking any entry scrolls the conversation view directly to that message. Useful for quickly jumping to specific points in long conversations.

- **D7. Duplicate / Fork Chat:** The user can duplicate a chat from any message point. The duplicate starts as a new ChatThread containing all messages up to and including the selected message. The user can then continue the conversation in a new direction without affecting the original chat.

- **D8. Message Feedback:** Each assistant message has thumbs-up and thumbs-down buttons. The user can provide feedback on AI responses. Feedback is stored with the message and can be reviewed later. Useful for the user to track which prompts and models produce the best results.

- **D9. Undo/Redo Message Edits:** After editing or deleting a message, the user can undo (Ctrl+Z) and redo (Ctrl+Y) the action. The undo/redo stack is per-chat and persists until the chat is closed.

### E. Chat Modes & Controls

- **E1. Standard Chat Mode:** Default mode. Messages follow the user/assistant conversational structure. Each user message is paired with an assistant response.

- **E2. Text Completion Mode:** An alternative mode where the chat does not use user/assistant structure. The user provides a raw text prompt, and the AI returns a raw text completion. No conversation history is maintained in this mode (each prompt is independent). Suitable for models that support text completion APIs.

- **E3. Thinking Toggle:** A control in the textbox toolbar that enables or disables AI "thinking" (extended reasoning) for the current chat. When disabled, the AI responds directly without showing its reasoning chain. When enabled, the AI may display its thinking process before the final answer.

- **E4. Mute Notifications Toggle:** A control in the textbox toolbar that mutes sound notifications for this specific chat. When muted, the assistant completion sound (see A4) is suppressed for this chat only. Other chats continue to play notifications normally.

- **E5. Dynamic System Message Editing:** The user can view and edit the current chat's system message at any time — not just at chat creation. Accessed from the chat header or Chat Navigation Bar (D6). Edits to the system message take effect for all subsequent messages. This is separate from editing past messages (D1).

### F. Artifacts & Side Panel File Editing

- **F1. AI-Generated Artifacts:** During a conversation, the AI can produce named artifacts — files with content such as code, recipes, documents, configuration files, etc. Each artifact has a name and type (inferred from language/file extension).

- **F2. Side Panel:** A resizable panel that opens to the right of the chat (or as a separate pane) displaying artifacts from the current chat. The panel lists all artifacts by name. Clicking an artifact displays its content.

- **F3. Version History:** Each artifact maintains version history (v1, v2, v3, ...). When the user requests changes to an artifact, the AI produces a new version rather than overwriting. All versions are preserved.

- **F4. Diff View:** The user can select any two versions of an artifact and view a side-by-side or unified diff highlighting additions, deletions, and changes.

- **F5. Version Switching:** The user can switch which version is "active" (displayed). The active version is what new changes are based on. Reverting to an older version and then requesting changes creates a new branch from that version.

- **F6. Artifact Viewer:** Displays artifact content with syntax highlighting for code files and rendered view for Markdown files. Includes a "Save to Disk" button to export as a file AND a "Save to Wiki" button. Clicking "Save to Wiki" launches the same Write-to-Wiki pipeline as N5: Preview Panel with inline editing, AI cross-linking (N10), [Refine in Chat] option, and for updates to existing wiki files the mandatory Diff Viewer. The artifact's content pre-fills the draft (instead of AI generating from chat conversation). This bridges chat artifacts and the Second Brain — an artifact that was iteratively refined in the side panel becomes a permanent wiki entry through the same review process.

- **F7. Global Artifacts Browser:** A dedicated tab/screen accessible from the Studio sidebar that lists ALL artifacts from ALL chats. Displays artifact name, type, parent chat, created date, and version count. The user can search, sort, and filter artifacts. Clicking an artifact opens it in the side panel viewer with full version history. This is separate from the per-chat artifact panel (F2) and the Media Library (G). Artifacts are strictly text-based, editable files (code, Markdown, config files, etc.) — media generation belongs in Section G.

### G. Media Library & Multi-Modal Generation

- **G1. Media Library Overview:** A dedicated browsable gallery of all media across all chats. Accessible from the Studio sidebar. Includes: AI-generated images (G4), AI-generated audio (G5), user-uploaded images, user-uploaded video, user-uploaded audio, webcam captures, and screenshots.

- **G2. Media Library Filtering & Search:** Media can be filtered by type (Images, Audio, Video, All), by source chat, and by date range. A search bar searches by filename. Media items display as thumbnails (images/video) or waveform icons (audio) in a grid layout.

- **G3. Media Actions:** From the Media Library, the user can: view/play the media, download to disk, copy to clipboard, open in system default app, delete from library, or navigate to the chat where the media originated.

- **G4. Image Generation:** When using a model that supports image generation (e.g., DALL-E, Stable Diffusion via API), the AI can generate images directly in the conversation. Generated images render inline in the chat and are automatically saved to the Media Library.

- **G5. Audio Generation:** When using a model that supports audio/speech generation, the AI can generate audio clips. Generated audio appears as an inline mini player in the chat and is automatically saved to the Media Library.

- **G6. Inline Media in Chat:** All media (generated or uploaded) renders inline in the chat conversation where it was created. Images display at message width. Audio shows a mini player. Video shows an embedded player. Each media item has a "Save to Disk" and "View in Library" button.

### H. Tool Use (Agent Capabilities)

- **H1. Browser Search:** The AI can request a web search. The app executes the search (opens browser or uses search API) and feeds results back to the AI. The user sees what search was performed.

- **H2. Terminal/Script Execution:** The AI can request execution of a shell command or script. Terminal execution ALWAYS requires explicit user confirmation — it cannot be auto-approved (overriding H5 auto-approval settings). The user must click "Approve" for each terminal command before it runs. The app displays the exact command to be executed. The app runs it in a terminal and captures output, feeding it back to the AI.

- **H3. File Generation:** The AI can create new files on disk. The user specifies or approves the target path.

- **H4. File Editing:** The AI can modify existing files on disk. The user approves the target file and can review changes.

- **H5. Tool Auto-Approval:** The user configures which tool types auto-execute without prompting. Global defaults are set in Settings. Per-chat overrides are available in the textbox toolbar. Example: "Always auto-approve browser search, but always ask before terminal execution."

- **H6. Deep Research:** The AI can conduct autonomous multi-step research. When invoked (by user request in chat), the AI: (1) formulates a research plan, (2) performs multiple web searches, (3) reads and extracts information from source pages, (4) synthesizes findings, (5) produces a structured report with inline citations. The user sees progress in real-time ("Searching... Reading 3 of 8 sources... Synthesizing..."). The final report appears as a chat message with clickable citations. Deep Research may take several minutes to complete.

- **H7. Wiki Search Tool:** The AI can query the user's local wiki index to find relevant .md files and their content. This enables the AI to answer questions based on the user's personal knowledge base ("What did I write about X?"). The AI sees matching file names, headings, and content snippets from the wiki index and can incorporate them into its responses.

### I. Import & Export

- **I1. Export Chat:** The user can export a chat conversation. Supported formats: Markdown (.md), PDF, and JSON. The export includes all messages in the current branch, with metadata (timestamps, model used, token counts). The user selects the format and destination path.

- **I2. Import Chats:** The user can import conversations from other platforms. Supported import formats: ChatGPT export (JSON), Claude export (JSON). Imported chats are created as new ChatThreads with all messages preserved. Conversation structure (user/assistant roles) is maintained.

### J. Prompt Library

- **J1. Prompt Library:** A collection of saved, reusable prompts accessible from the chat input area. The user can save the current textbox content as a named prompt. Prompts support dynamic variables that are resolved at insertion time: {{clipboard}} (current clipboard content), {{selected_text}} (currently highlighted text in the active application), {{date}} (current date), {{current_wiki_file}} (name of the currently open wiki file). Prompts are organized with tags and can be searched. Selecting a saved prompt inserts it into the textbox with variables resolved. The prompt library is separate from Prompt Enhancements (which transform existing text rather than inserting saved text).

- **J2. Prompt Management:** Prompts can be edited, deleted, and organized into folders/categories. The prompt library is accessible from a button in the textbox toolbar.

### K. Text Actions & Three-Tier Interaction

Text Actions are the unified mechanism for AI-powered text transformations. Define once, use anywhere — via global hotkey (Tier 1) or textbox toolbar dropdown (Studio).

- **K1. Text Actions (Unified):** The user defines named Text Actions in Settings. Each Text Action consists of: a display name (e.g., "Fix Grammar", "Rewrite", "Summarize"), a system prompt instructing the AI how to transform text, and a Model Configuration (B2) to use for execution. Built-in default Text Actions: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt. The user can create custom Text Actions. Once defined, a Text Action is available in TWO places: (a) as a global Windows hotkey (Tier 1), and (b) as a dropdown option in the Studio textbox toolbar (K2).

- **K2. Textbox Toolbar:** A row of controls immediately above or adjacent to the chat message text input. Contains: Persona selector (B4), thinking toggle, mute notifications toggle, tools enable/disable toggle, tool auto-approval per-chat override, prompt library button, **Text Actions dropdown** (select a Text Action → transforms current textbox content → preview popup → accept/discard/edit).

- **K3. Tier 1 — Global Hotkey Text Actions:** The user assigns global hotkeys to Text Actions (e.g., Alt+Q = Rewrite, Alt+W = Summarize). Highlight text in any Windows application, press the hotkey, and the flow proceeds in three phases:

  **Phase 1 — Capture:** The app captures the highlighted text from the active window, along with its HWND (P2) and clipboard format information (P4). A minimal "Thinking…" overlay (small pill-shaped indicator, non-intrusive) appears near the cursor while the AI processes.

  **Phase 2 — Result Popup:** When the AI response completes, the "Thinking…" overlay expands into a result popup near the cursor. The popup contains:
  - The AI-transformed text in an **editable text area** (the user can modify the AI output before applying)
  - A **header** showing the Text Action name and the source application name
  - Four action buttons: **[Accept]** (pushes the result back to the source application), **[Discard]** (dismisses the popup, no changes made), **[Open in Studio]** (elevates this interaction to a full Studio ChatThread for further refinement — the result text becomes the first assistant message), and a **Retry** button (visible only if the AI call failed, re-attempts with the same input)
  - An optional **"Additional Instructions"** text field at the bottom: the user can type extra guidance (e.g., "make it more formal") and press Enter to re-run the Text Action with the additional instruction appended

  **Phase 3 — Apply:** When the user clicks [Accept]:
  - The app first attempts direct HWND text injection into the source window (replacing the originally highlighted text)
  - If HWND injection fails (e.g., the source app doesn't support it), the app falls back to clipboard paste: the result is placed on the clipboard (preserving format per P4) and the app simulates Ctrl+V into the source window
  - A brief confirmation toast appears: "Text applied — [Text Action name]" with an "Undo" option (places original text back on clipboard)
  - If the user clicked [Open in Studio], the interaction becomes a permanent ChatThread (IsTransient flips to false per O3) and the Studio window opens with the result as the first assistant message

  **Error State:** If the AI call fails (network error, API error, rate limit), the popup displays the error message with a [Retry] button and a [Discard] button. The user can also edit their additional instructions and retry.

  **Empty/No-Selection State:** If the user presses the hotkey without highlighting any text, the "Thinking…" overlay appears briefly then shows: "No text selected. Highlight text in any application and try again." The overlay auto-dismisses after 3 seconds.

  This is the same Text Action as in K2 — not a separate feature to configure.

- **K4. Tier 2 — Command Bar:** Alt+Space opens a Spotlight-style command bar overlay at the horizontal center of the screen, near the top (approximately 15% from the top edge). The Command Bar operates in two states: **Inline** and **Popped Out**.

  **Initial Inline State:**
  - The overlay appears as a rounded search-bar-style input field (~600px wide, centered) with a subtle drop shadow
  - The input field contains placeholder text: "Ask anything…" and a blinking cursor
  - The default Persona name (from A2) is displayed as a small label to the left of the input
  - A small "?" icon to the right opens a tooltip listing available commands and shortcuts
  - The user types a prompt and presses Enter to send
  - While the AI is processing, a thin progress bar animates along the top edge of the overlay; the input remains visible but dimmed

  **Inline Q&A Display:**
  - When the AI response arrives, the bar expands downward to reveal the response in a compact message area below the input
  - The response renders Markdown (headings, bold, italic, code blocks with syntax highlighting, lists, links) within the expanded area
  - The user's query appears as a minimal label above the response (e.g., "You: [query text]")
  - Multiple Q&A pairs stack vertically as the conversation continues; each new query appears above its response
  - The bar grows vertically with each Q&A pair, up to a maximum of 70% of the screen height, after which older messages scroll internally
  - The text input remains pinned at the bottom of the bar at all times

  **Inline Controls:**
  - A **[Pop-out]** button (icon: expand/detach) appears in the top-right corner of the command bar at all times — clicking it detaches the conversation into a floating mini-window (see below)
  - A **[Close]** button (icon: X) dismisses the entire Command Bar; the conversation is saved as a transient ChatThread (IsTransient=true per O2)
  - A **[Copy]** button on each AI response copies the response text to clipboard
  - A small **persona indicator** shows which Persona is active; clicking it opens a compact Persona switcher dropdown

  **Popped-Out Floating Mini-Window:**
  - Triggered by clicking [Pop-out]. The Command Bar detaches from its anchored position and becomes a floating, resizable window
  - Default size: 500px wide × 400px tall. Minimum: 350×250. Maximum: no hard limit (constrained by screen)
  - Title bar displays "Command Bar" and the active Persona name
  - Title bar controls: **[Open in Studio]** (elevates to permanent chat, see below), **[Pin Always on Top]** toggle, **[Minimize to system tray]**, **[Close]** (dismisses, saves as transient)
  - Message area: scrollable compact chat view showing all Q&A pairs with the same rendering as the inline state
  - Text input at the bottom with Send button
  - The mini-window remembers its last position and size across sessions
  - The mini-window can be moved by dragging the title bar and resized by dragging any edge or corner
  - Clicking [Open in Studio] closes the mini-window and opens the full Studio with this conversation as a permanent ChatThread (IsTransient flips to false per O3)

  **Elevation to Studio:**
  - Either from the inline [Pop-out] → [Open in Studio], or from the mini-window's [Open in Studio] button
  - The Studio window opens (or gains focus if already open)
  - A new tab is created containing the full conversation from the Command Bar as a permanent ChatThread
  - The Command Bar overlay/mini-window closes

  **Dismissal:**
  - Closing the Command Bar (inline or mini-window) saves the conversation as a transient ChatThread
  - Transient threads are visible in the Timeline tab (L5) and subject to 7-day auto-cleanup (O4)
  - Pressing Escape when the input is empty dismisses the Command Bar; if the input has text, Escape clears the text first, second Escape dismisses

  **Error State:** If the AI call fails, the error message appears inline below the input with a [Retry] link. The user can edit their query and press Enter to retry.

  **Empty/No-Input State:** If the user presses Enter with an empty input, the bar briefly shakes (visual nudge) and does nothing.

- **K5. Tier 3 — Studio Chat:** The full chat workspace (Section C). Opened via the main application window, system tray icon, or by elevating a Tier 1 or Tier 2 interaction.

### L. Chat Organization & Search

- **L1. Sidebar Chat List:** Left sidebar displaying all permanent chats (IsTransient=false). Sorted according to the selected sort order (L13). Pinned chats appear at the top. Grouped by date: Today, Yesterday, This Week, This Month, Older. Each entry shows: chat title, last message preview, timestamp, favorite star indicator, tags (if any).

- **L2. Chat Favoriting:** Star/unstar any chat. A filter toggle in the sidebar shows only favorited chats.

- **L3. Full-Text Search:** Search bar searches across all chat messages (both permanent and transient, within their retention window). Results display as a list showing matching message snippets with the parent chat name, timestamp, and highlighted search terms. Clicking a result opens that chat and scrolls to the matching message.

- **L4. Delete Chat:** Delete a chat with a confirmation dialog. Deleting a permanent chat removes it and all its messages/branches/artifacts. Any media or artifacts exclusively linked to the deleted chat (not saved elsewhere) are also deleted from disk (see O5).

- **L5. Timeline Tab:** A separate tab in the sidebar showing a chronological feed of all transient micro-actions (Tier 1 and Tier 2 interactions). Each entry shows: action type, a preview of the content, timestamp, and the source application (if HWND context was captured). Entries are grouped by date.

- **L6. Sidebar Filtering:** Default sidebar view shows permanent chats only. Switching to the Timeline tab shows transient actions. The user can toggle between these views.

- **L7. Chat Tags/Labels:** The user can assign custom tags/labels to chats (beyond just favoriting). Tags are user-defined (e.g., "coding", "writing", "research"). Tags appear on chat entries in the sidebar and can be used for filtering.

- **L8. Pin Chats:** The user can pin important chats to the top of the sidebar. Pinned chats appear above all other chats and are not affected by date sorting.

- **L9. Chat Folders/Collections:** The user can create folders or collections to organize chats. A chat can belong to one folder at a time. The sidebar can display chats grouped by folder.

- **L10. Chat Archiving:** The user can archive chats without deleting them. Archived chats are hidden from the default sidebar view but remain accessible via an "Archived" filter. Archived chats are not deleted by the 7-day cleanup.

- **L11. Bulk Operations:** The user can select multiple chats in the sidebar (via checkboxes or Ctrl+click) and perform bulk actions: delete, archive, export, tag, or move to folder.

- **L12. Right-Click Context Menu:** Right-clicking a chat in the sidebar opens a context menu with: Rename, Delete, Archive/Unarchive, Duplicate, Export, Pin/Unpin, Add/Edit Tags, Move to Folder. Destructive actions show a confirmation dialog.

- **L13. Chat Sorting Options:** The user can sort the sidebar chat list by: Most Recent (default), Name (A-Z), Date Created, Last Activity. Sort order is remembered across sessions.

- **L14. Chat Color Labels:** The user can assign a color label (colored dot) to any chat from a preset palette. The color dot appears on the chat entry in the sidebar for quick visual identification.

### M. Model Comparison

- **M1. Model Comparison Mode:** The user can send the same prompt to multiple Personas simultaneously and view responses side-by-side. Accessed via a "Compare" button in the textbox toolbar or from a dedicated menu option. Model Comparison operates as a transient view — responses are displayed temporarily for comparison only.

- **M2. Comparison Setup:** The user selects 2 or more Personas to compare. A single text input is used. Each Persona's response appears in its own panel, arranged horizontally or vertically (user-configurable).

- **M3. Comparison Results:** Each panel shows: Persona name, response time, token count, and estimated cost. Responses stream simultaneously. The user can stop all or individual generations.

- **M4. Accepting a Comparison Result:** The user clicks "Accept" on one of the compared responses. That response is then appended to the permanent ChatThread as a normal assistant message. The other (unaccepted) responses are discarded from the chat (they remain visible in the comparison view until the user closes it). Optionally, the user can save unaccepted responses as alternate branches on the same message node for future reference.

### N. Personal Wiki / Second Brain

The Personal Wiki is the user's curated, AI-assisted knowledge base — plain .md files on disk, indexed and searchable within the app, and editable by any external tool (VS Code, Obsidian, etc.). The wiki is built through a "discuss then confirm" model: every entry originates from a chat conversation, is reviewed by the user, and is explicitly approved before saving. The user owns the wiki structure; the AI assists with content and cross-linking.

- **N1. Wiki Directory Configuration:** The user selects a directory on disk containing .md files as their personal wiki. The app watches this directory for file changes using a file system watcher. The wiki directory is just a folder of plain .md files — the user can independently initialize it as a git repo (`git init`) for additional backup without any app integration required.

- **N2. Wiki Indexing:** All .md files in the wiki directory are indexed for full-text search. The index stores: filenames, full heading hierarchy (H1, H2, H3 headings and their text), and full file content. The index updates automatically when files change (detected via file system watcher). This index powers wiki search (N3), the Related Sections panel (N4), @ mentions (N7), and the AI's cross-linking pipeline (N10). No API cost — purely local.

- **N3. Wiki Search:** A dedicated search scope within the app that searches only wiki entries. Separate from chat search (L3). Results show matching .md file names, matching headings (with heading level indicated), and content snippets with highlighted search terms. Clicking a result opens that file in the Wiki Browser (N4), scrolled to the matching section.

- **N4. Wiki Browser:** Within the app, the user can browse the wiki directory tree and open .md files for viewing. Files render as formatted Markdown. The Wiki Browser layout is split into three regions: (Left) File Tree — collapsible directory tree of the wiki folder; clicking a file opens it in the main viewer. (Center) Markdown Viewer — renders the selected .md file with full formatting; includes an "Open in External Editor" button that launches the file in the system default .md editor (e.g., VS Code). (Right) Info Panel with two tabs: Related Sections (uses local wiki index to find sections in other files sharing keywords with the current file — zero API cost, pure local computation) and Backlinks (shows which other wiki files contain links TO the current file, detected by scanning for cross-reference patterns across the local index).

- **N5. Write to Wiki — Core Workflow:** The primary mechanism for creating and updating wiki content. Every wiki change follows the "discuss then confirm" model.

  **Trigger:** (a) "Write to Wiki" button in the Studio chat textbox toolbar; (b) "Write to Wiki" option in the sidebar chat right-click context menu (L12).

  **Pipeline:** (1) User triggers "Write to Wiki." (2) Dialog: "Create new wiki file" or "Update existing" (user picks target from directory tree). (3) AI reads the chat conversation and generates a polished .md summary while running the cross-linking pipeline (N10) to identify relevant existing sections to link to. (4) Preview Panel appears with editable AI-generated content; suggested cross-links are highlighted. (5) Action buttons: [Save to Wiki] (new files: saves immediately; updates: opens mandatory Diff Viewer), [Refine in Chat] (opens new Studio tab with draft as an artifact for iterative discussion), [Append Only] toggle (see N9), [Cancel] (discards).

  **For UPDATES:** Mandatory side-by-side Diff Viewer (red = removed, green = added). "Commit to Wiki" button only clickable after user has scrolled through the full diff. Silent backup snapshot saved before modification (N6). After committing, backlink suggestions appear (N10).

- **N6. Automatic Wiki Versioning:** Every time the AI (or the user via the app) modifies an existing .md file in the wiki, the app saves a snapshot of the file's previous state into the local SQLite database. Retention: maximum 30 snapshots per file (oldest auto-deleted when exceeded) AND total snapshot storage capped at 50MB (oldest deleted across all files when exceeded). Recovery: right-click any file in Wiki Browser (N4) → "Version History" → preview any snapshot → "Restore" (restoring creates a new snapshot of current state first, so restore is undoable). Separate from Google Cloud backups (Section R) — snapshots are for instant in-app undo; backups are for disaster recovery.

- **N7. @ Mentions for Wiki Files:** In the chat textbox, typing @ opens a quick-search dropdown of wiki .md files from the local index (N2). Real-time filtering as the user types. Each result shows filename and H1 title. Selecting a file injects its full content into chat context. If content exceeds ~8,000 tokens, a summarized excerpt (H1 + all H2 headings + first paragraph of each section) is injected instead with a note: "[Full content available in Wiki Browser]". The AI can also autonomously query the wiki using the Wiki Search Tool (H7).

- **N8. AI Wiki Access Restrictions:** Hard-coded least-privilege rules: (a) No Deletions — AI tool-calls to delete wiki files are rejected by the app; only the human user can delete. (b) No Renaming — AI cannot rename wiki files (prevents breaking cross-links from N10); AI can suggest renames for manual execution. (c) Write-to-Wiki Only — AI can only write to wiki through the explicit N5 workflow; generic tool use (H3, H4) cannot target the wiki directory.

- **N9. Append-Only Mode:** Toggle in the N5 Preview Panel. When active, AI cannot modify existing text in the target file. AI-generated content is appended under `## AI Addition — [YYYY-MM-DD]`. Diff Viewer still appears but only shows the appended section (no red/removed text). Ideal for journals, logs, or preserving user's own words. Toggle state remembered per chat session.

- **N10. AI Cross-Linking (Forward + Backlinks):** Tiered, cost-efficient pipeline for suggesting section-level cross-links between wiki files.

  **Forward Links (New File → Existing Files):** (1) AI reads `index.md` (N11) to get the complete wiki catalog — directory tree, all H1/H2/H3 headings with links, and existing cross-links — in a single read, near-zero token cost. The AI also uses `index.md` to suggest which subfolder the new file belongs in. (2) AI selects candidate files/headings relevant to new content. (3) AI requests full content only of selected relevant sections. (4) AI generates draft with suggested `[text](../path/file.md#heading)` links, highlighted in the Preview Panel (N5). (5) User reviews, accepts, or rejects each link individually before saving.

  **Backlinks (Existing Files → New File):** After user saves the new file, AI evaluates which existing files could benefit from linking TO it. A separate "Suggested Backlinks" panel appears: "{N} existing files could link to this page." User can [Apply All], select and [Apply Selected], or [Dismiss]. Each approved backlink opens its own Diff Viewer for that existing file — preserving the "every change is confirmed" principle. If Append-Only Mode (N9) is active for a target file, backlinks are appended under the dated heading.

- **N11. Auto-Generated index.md:** The app automatically maintains an `index.md` file at the root of the wiki directory. This file is regenerated after every wiki change (file creation, update, deletion detected by the file system watcher). It contains:

  **Structure:**
  - A collapsible directory tree showing the full wiki folder hierarchy
  - For each `.md` file in the wiki: filename with link, H1 title, all H2 and H3 headings with anchor links, creation date, last modified date, and a list of cross-links (which other wiki files this file links TO, and which files link TO this file — derived from scanning `[text](file.md#heading)` patterns across all wiki content)
  - A "Recently Modified" section at the top listing the 10 most recently changed files
  - An "Orphan Pages" section listing files with zero inbound links from other wiki files

  **Generation:** The `index.md` is generated purely from the local wiki index (N2) and file system metadata. No AI/API calls are involved. It is a plain `.md` file — the user can open and read it in any editor, and it is itself indexed and searchable.

  **Usage by AI:** During the Write-to-Wiki cross-linking pipeline (N10 step 1), the AI reads `index.md` (a single file) instead of querying the database index piecemeal. This gives the AI the complete wiki structure, all headings, and all existing cross-links in one read — enabling it to select relevant candidates and suggest where in the directory tree a new file should be placed. The AI also uses `index.md` to avoid suggesting duplicate cross-links that already exist.

### O. Data Model & Lifecycle

All application data (ChatThreads, Messages, Personas, Model Configurations, Artifacts, wiki version snapshots, settings, usage records, and all other persistent state) is stored in a local SQLite database on the user's machine. The database file resides in the app's local data directory. Wiki .md files, exported files, and media saved to disk are stored as regular files on the file system — not inside the database. The SQLite database is the single source of truth for all app-internal data; the wiki directory is the single source of truth for wiki content.

- **O1. Unified ChatThread Model:** Every interaction — Tier 1 hotkey action, Tier 2 command bar query, Tier 3 Studio conversation — creates a ChatThread containing one or more Messages. The data model is identical regardless of origin tier.

- **O2. IsTransient Flagging:** Tier 1 and Tier 2 interactions are tagged IsTransient=true. Tier 3 (Studio-originated or elevated) chats are tagged IsTransient=false.

- **O3. Chat Elevation:** When the user opens a transient thread in the Studio UI and sends a reply, the IsTransient flag flips to false. The chat becomes permanent and appears in the default sidebar view.

- **O4. 7-Day Auto-Cleanup:** A background task runs periodically and deletes all ChatThreads where IsTransient=true and the thread age exceeds 7 days. EXCEPTION: Any transient thread that has been Favorited, Tagged, Pinned, archived, or contains user-created branches or user replies is excluded from auto-cleanup (its IsTransient flag is automatically flipped to false, elevating it to permanent). The deletion of qualifying threads is permanent and automatic.

- **O5. Garbage Collection Policy:** When a ChatThread is hard-deleted (manually or via 7-day auto-cleanup), any media files and artifact files exclusively linked to that chat — and not explicitly saved to the wiki, permanent Media Library, or disk — are also deleted from storage. Media/artifacts shared across multiple chats or saved to the permanent library are preserved.

- **O6. Database Compaction:** The 7-day auto-cleanup can fragment the SQLite database. A maintenance routine (accessible via A9) runs VACUUM to reclaim disk space.

### P. Windows OS Integration

- **P1. Global Keyboard Hooks:** System-wide keyboard hooks detect hotkey combinations regardless of which application has focus.

- **P2. HWND Capture:** Before drawing any overlay UI, the app captures the currently active window handle (HWND) to prevent focus-stealing race conditions.

- **P3. Spatial Anchoring:** When a Tier 1 hotkey action captures highlighted text from a source application, the app saves the captured HWND, source application name, source document/window title (if detectable), and original highlighted text with the ChatThread. This context enables the "Apply back" workflow in Studio (see C5a for the [Apply] button UI).

  When the chat is opened in Studio and originated from a Tier 1 text transformation (i.e., has valid HWND context and original text), the following UI elements appear:

  **Chat Header Source Indicator:** A small non-intrusive banner in the chat header displays: "[Source: {App Name} — '{Document Title}']" (e.g., "[Source: Word — 'chapter3.docx']"). Next to it, an **[Apply Latest]** button pushes the most recent assistant message back to the source application.

  **Per-Message [Apply] Button:** Each assistant message that was a direct transformation of the captured text displays an [Apply] button (alongside Copy, Regenerate, etc.). Clicking [Apply] on a specific message pushes that message's content back to the source window. Messages from subsequent follow-up questions (that are new queries, not transformations of the original text) do NOT show [Apply].

  **Apply Mechanism:** When [Apply] or [Apply Latest] is clicked:
  - The app first attempts direct HWND text injection into the source window (replacing the originally highlighted text with the new content)
  - If HWND injection fails (source app doesn't support it, window no longer exists, or the text control can't be targeted), the app falls back to clipboard paste: the result is placed on the clipboard (preserving format per P4) and the app simulates Ctrl+V into the source window
  - A brief confirmation toast appears: "Text applied to {App Name}" with an "Undo" option

  **Missing Source Window:** If the source application window has been closed since the Tier 1 action, the [Apply] and [Apply Latest] buttons are grayed out with a tooltip: "Source application is no longer available."

  **No HWND Context:** If the chat was elevated from Tier 2 (Command Bar) or was created directly in Studio, no source indicator or [Apply] buttons appear — there is nothing to apply back to.

- **P4. Clipboard Format Preservation:** The app checks clipboard DataFormats during hotkey capture. If the source placed rich-text formats (HTML, RTF) on the clipboard, the AI returns its response in the corresponding format.

- **P5. Local WebSocket Server:** The app hosts a local WebSocket server on localhost for direct integrations (e.g., Microsoft Word Add-in pipeline).

- **P6. System Tray:** The app minimizes to the Windows system tray. Left-clicking the tray icon restores the Studio window (or opens it if closed). Right-clicking the tray icon opens a context menu with:
  - **New Chat** — Opens Studio and creates a new blank chat with the default Persona
  - **Open Studio** — Restores or focuses the main Studio window
  - **Command Bar** — Opens the Tier 2 Command Bar overlay (equivalent to Alt+Space)
  - **Recent Chats** — Submenu listing the 5 most recently active permanent chats by title; clicking one opens that chat in Studio
  - Separator
  - **Settings** — Opens the Settings screen
  - Separator
  - **Exit** — Fully closes the application (including background tasks like the WebSocket server and file watcher); a confirmation dialog appears if any AI responses are actively generating (see C20)

- **P7. Session Restore:** On launch, the previous session's chats and tabs are restored (if A6 is enabled).

- **P8. Per-Monitor DPI Awareness:** The app is fully per-monitor DPI aware. All UI renders crisply at any DPI scaling and adapts when moved between monitors with different DPI settings.

### Q. Language & RTL Support

- **Q1. English (LTR):** Default language. All UI labels in English. Text rendering is left-to-right.

- **Q2. Hebrew (RTL):** Messages containing Hebrew text automatically render right-to-left based on Unicode character ranges.

- **Q3. Mixed LTR/RTL Messages:** Messages with both English and Hebrew render each segment in its correct direction.

### R. Backup & Recovery

- **R1. Google Cloud Storage Backup:** Automatic backup of the full SQLite database, all wiki .md files, and all artifact files to a user-configured Google Cloud Storage bucket.

- **R2. Backup Schedule:** Configurable: daily, weekly, manual only. Default: daily at a user-specified time.

- **R3. Manual Backup:** Trigger immediate backup from Settings.

- **R4. Restore:** Restore from backup replaces current data with backup contents. Confirmation dialog warns of data overwrite.

### S. Usage & Pricing Dashboard

- **S1. Usage Overview Screen:** A dedicated screen showing comprehensive usage statistics — total tokens and estimated cost across all providers. Filterable by provider, model, profile, or chat.

- **S2. Time Range Filters:** Today, This Week, This Month, Custom Date Range, All Time. Default: This Month.

- **S3. Usage Charts:** Line chart of token usage over time, bar chart of cost over time, pie charts breaking down usage by provider and by model.

- **S4. Per-Chat Breakdown:** Table listing all chats in the selected time range with token count, cost, and model(s) used. Clicking opens the chat.

- **S5. Budget Alerts:** User sets a monthly spending limit. Warning at configurable threshold (e.g., 80%). Option to block further API calls when exceeded.

### T. Nice-to-Have Features

Features that are part of the long-term vision but can wait indefinitely. The data model and architecture should be designed to accommodate these without rework.

- **T1. Macro Genesis:** Compile successful chat sequences into permanent Tier 1 hotkeys.

- **T2. Context-Aware Grouping:** Auto-tag threads by window context (e.g., group all VS Code threads).

- **T3. Passive Autonomous Threads:** Local vision watchdogs proactively spawn threads.

- **T4. Screenshot/Screen Awareness:** Include screenshot of active window in Tier 1/Tier 2 actions for visual context analysis.

- **T5. Video Generation:** AI generates video clips when future multi-modal models support it.

## Secondary Features

Not applicable — all features in Sections A through S are considered core and required for the complete initial vision. The Nice-to-Have section (T) captures future features.

## Explicitly Out of Scope

Features we are consciously NOT building. These will not be part of the app — now or in the future — unless the vision is explicitly revised.

- **Multi-User Support:** The app is strictly single-user. There will never be user accounts, login screens, role-based access control, or collaboration features.

- **Non-Windows Platforms:** The app is Windows-only. It will never support macOS, Linux, iOS, Android, or web-based access.

- **Web Interface / Browser Access:** There is no web frontend. All interaction is through the native Windows application.

- **Built-in API Proxy Service:** The app does not proxy API calls through a third-party service. API keys are used directly by the app to call provider APIs from the user's machine. The user is responsible for obtaining and managing their own API keys.

- **Cloud Sync (Beyond Backup):** The app does not synchronize data across multiple devices. Backup to Google Cloud Storage is for disaster recovery only, not for multi-device access.

- **Built-in Provider Accounts:** The app does not create or manage accounts with AI providers. The user brings their own API keys.
