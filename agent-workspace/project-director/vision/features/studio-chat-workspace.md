# Studio Chat Workspace — Feature Spec

## What the User Accomplishes
The user engages in deep, multi-turn AI conversations with full Markdown rendering, code syntax highlighting, streaming responses, multiple chat tabs (including generic file viewer tabs), 14-tool agent surface, and comprehensive message actions. This is the Tier 3 primary workspace.

## Trigger
- Open main application window
- Click system tray → "Open Studio"
- Elevate from Tier 1 (Open in Studio) or Tier 2 (Open in Studio)
- Ctrl+N (new chat)
- Ctrl+O (open file in viewer tab)
- Drag file onto tab bar (opens in file viewer tab)

## Layout
The Studio window uses a sidebar + main content layout with tabbed chats AND file viewer tabs. See [`screens/studio-chat.md`](screens/studio-chat.md) for the full screen specification.

## Detailed Behavior

### C1. Conversation View
- Full scrolling message history for the active ChatThread
- Messages visually distinguished per selected theme (A3): Classic (user right, assistant left), Compact (minimal spacing), Bubble (chat-bubble style)
- Each message displays: model/Persona that generated it (if different from current), relative timestamp ("2 min ago"), hover reveals full date+time

### C2. Message Content Rendering
Assistant messages render: Markdown (headings H1-H6, bold, italic, inline code, fenced code blocks with syntax highlighting, bulleted/numbered lists, links, tables, blockquotes, horizontal rules), images (inline at full width up to max size, clickable for full resolution), audio (inline mini player with play/pause/seek), video (inline embedded player).

### C3. Code Block Rendering
- Fenced code blocks (` ```language `) display with syntax highlighting based on declared language
- Copy button appears on hover at top-right of each code block
- Language label shown in code block header
- Without language declaration: renders as plain preformatted text, no highlighting

### C4. Streaming Response Display
- Assistant responses render token-by-token as generated
- Markdown and code blocks render progressively (code block appears when opening fence is detected, content streams in, closes when closing fence arrives)
- "Stop" button visible during generation; clicking stops and keeps partial response
- On completion: message footer shows "Generated in X.Xs"
- If streaming disabled (A4): full response appears at once when complete

### C5. Message Actions
- **Send:** Enter key or click Send button. Shift+Enter for newline in input.
- **During Generation:** The Send button transforms into a spinner + red "Stop" button. Clicking Stop preserves partial response and reverts to Send.
- **Regenerate:** Visible on last assistant message. Replaces with new generation. Old response preserved as branch.
- **Edit:** Opens message in editable mode (D1)
- **Delete:** Removes message from history (D2)

### C6. Copy
- **Two explicit copy buttons per message:**
  - **"Copy MD" (📋):** Copies raw Markdown to clipboard — for pasting into VS Code, Obsidian, plain text editors
  - **"Copy Rich" (📝):** Copies Rich Text (HTML/RTF) to clipboard — for pasting into Word, Outlook, browsers with formatting preserved
- Menu option: "Copy Entire Conversation" → copies as Markdown or plain text (user choice)

### C5a. Conditional [Apply] Button
See [`features/windows-os-integration.md`](features/windows-os-integration.md) P3 for full spatial anchoring spec.

- **Source Indicator Banner:** Appears in chat header when chat originated from Tier 1 text transformation. Shows: "[Source: {App Name} — '{Document Title}']" + [Apply Latest] button
- **Per-Message [Apply]:** On each assistant message that was a direct transformation of captured text. NOT on follow-up query responses.
- **[Apply Latest]:** Pushes most recent assistant message to source application
- **Grayed Out State:** If source window closed: "[Apply] — Source application is no longer available"
- **No HWND Context:** Chats created directly in Studio or elevated from Tier 2 never show [Apply]

### C7. Chat Titling
- **Auto-Generation:** When first user message is sent, AI generates a title (3-7 words summarizing the topic). Title appears after assistant responds.
- **Manual Edit:** Click title in sidebar or chat header → inline edit → Enter to save
- **Empty/Loading:** Shows "New Chat" until first message sent; shows "Generating title..." during AI title generation
- **Failure:** If AI title generation fails, title defaults to first 50 characters of first user message

### C8. Continue Generation
- "Continue" button appears near last assistant message when conversation's last message is from assistant
- Sends continue request to model (assistant prefix continuation)
- Useful when response ended mid-sentence or user deleted their last message

### C9. Drag & Drop Files/Media
- Drop zone: entire chat input area
- **Images:** Sent to AI (vision-capable models). Rendered inline after sending.
- **Video/Audio:** Included as input for multi-modal models. Rendered with inline player.
- **Text files (.txt, .md, .csv, code files):** Content read and included in prompt
- **PDF files:** Content extracted (text and embedded images where possible) and included in prompt for models that support PDF input natively (Anthropic, some OpenAI models). For models without native PDF support, text is extracted locally and included as plain text.
- **HTML files:** Content extracted (text content, stripped of tags) and included in prompt
- **Other files:** Metadata (filename, type, size) included in prompt context; content not read
- **Drop Indicator:** Visual feedback during drag-over

### C9a. Paste Image from Clipboard (Ctrl+V)
- When an image is on the clipboard and user presses Ctrl+V in the textbox: the image appears as a thumbnail icon below the textbox (not embedded in the text)
- Multiple pasted images stack as thumbnails in an attachment row below the textbox
- Each thumbnail shows a small preview + filename + size. Hover shows full preview.
- **Click thumbnail:** Opens the image in a file viewer tab in the main content area (full-size viewer with zoom/pan) — see [`features/file-viewer-tabs.md`](file-viewer-tabs.md)
- **Remove:** X button on each thumbnail removes it before sending
- Images in the attachment row are sent with the next message (for vision-capable models)
- **Text still editable:** The textbox remains the primary input; images are attachments, not inline

### C9b. Attach File Button (📎)
- **Location:** Textbox toolbar, alongside microphone (C21) and camera (C22) buttons
- **Behavior:** Opens a standard Windows file picker dialog
- **Supported file types:** All common document and media types — images (.png, .jpg, .gif, .webp, .bmp), documents (.pdf, .html, .htm), text (.txt, .md, .csv, .log), code (.py, .js, .ts, .json, .yaml, .xml, .css, .java, .c, .cpp, .rs, .go, .rb, and other common extensions), audio (.mp3, .wav, .ogg, .m4a), video (.mp4, .webm, .mov)
- **After selection:** File appears as a thumbnail/card in the attachment row below the textbox, identical to drag-and-drop (C9) and clipboard paste (C9a) attachments
- **Multiple files:** File picker supports multi-select. Each file appears as a separate attachment card
- **Remove:** X button on each attachment card removes it before sending
- **Click attachment:** Opens the file in the system default application (for images: inline preview; for documents: system default viewer)
- **File size limit:** Maximum 100MB per file. Warning if exceeded: "File '[filename]' is too large ([size]). Maximum file size is 100MB."

### C9c. Model-Aware File Type Compatibility
- When files are attached (via C9 drag-drop, C9a paste, or C9b file picker), the app checks the active Model Configuration's provider and model capabilities
- **Compatibility mapping per provider:**
  - OpenAI (GPT-4o, GPT-4V): Images ✓, text files ✓, PDF (text extraction ✓). Video/Audio ✗ (metadata only).
  - Anthropic (Claude 3.5 Sonnet, Opus): Images ✓, PDF (native) ✓, text files ✓. Video/Audio ✗ (metadata only).
  - Google (Gemini): Images ✓, video ✓, audio ✓, PDF ✓, text files ✓.
  - DeepSeek, MiMo, Moonshot, Mistral: Text files only. Images/PDF/Video/Audio ✗ (metadata only).
  - OpenAI-Compatible (local): Depends on the specific local model. User configures supported types in Model Configuration (B2). Default: text only.
- **Warning display:** For each attached file that the model does NOT support, a yellow warning badge appears on the attachment card: "⚠️ [Model name] does not support [file type]. Attached as metadata only."
- **Sending with unsupported files:** The message sends normally. Unsupported files have their filename, type, and size included in the prompt as metadata (e.g., "[Attached file: report.pdf (2.3MB) — content not available to this model]"). The AI may acknowledge the file exists but cannot process its content.
- **Provider-specific behavior:**
  - Unsupported file types are NEVER sent to the provider API (prevents API errors)
  - The user is not blocked from sending — they can still get a response about the text portion of their message
  - The warning is informational, not a hard block

### C10. Multiple Chat Tabs
- Tab bar above chat view. Each tab = independent ChatThread OR file viewer tab.
- **Chat tabs:** Chat title (C7), truncated if needed
- **File viewer tabs:** 📄 filename, "Read-Only" badge. See [`features/file-viewer-tabs.md`](file-viewer-tabs.md).
- **Reorder:** Drag-and-drop tabs
- **Close:** X button on tab or Ctrl+W
- **Reopen:** Ctrl+Shift+T reopens last closed tab (chat or file viewer)
- **Overflow:** When tabs exceed width, scrollable tab bar with arrows

### C11. Token Usage & Context Display
- **Per-Message:** Each assistant message shows: prompt tokens + completion tokens = total. Cache tokens (if any). Estimated cost (if pricing configured). Latency: "in 1.2s".
- **Chat Header:** Shows (a) Current Context Size — "12,450 / 128,000 tokens" with colored bar (green/yellow/red as limit approaches), and (b) Cumulative Chat Cost — total estimated cost across ALL branches
- **Real-Time:** Context size updates as user types, using local tokenizer library (not API response headers)

### C12. Keyboard Shortcuts (Studio)
- Ctrl+N: New Chat (opens Persona picker)
- Ctrl+O: Open File (opens file picker → opens in file viewer tab)
- Ctrl+W: Close Current Tab
- Ctrl+Shift+T: Reopen Last Closed Tab
- Ctrl+Tab: Next Tab
- Ctrl+Shift+Tab: Previous Tab
- Ctrl+F: Search Within Current Tab (chat or file viewer)
- Ctrl+Shift+F: Global Search Across All Chats (opens L3)
- Ctrl+S: Export Current Chat (opens I1 dialog)
- Ctrl+/: Searchable keyboard shortcut reference overlay

### C13. Resizable Panels
- Sidebar width, artifact/side panel width, chat navigation bar width — all adjustable by dragging divider
- Minimum width: 150px for sidebar, 200px for panels
- Maximum width: 50% of window
- Panel sizes remembered across sessions

### C14. Error Handling & Retry
- **API Failure:** Assistant message area shows red error banner with specific message (e.g., "Rate limit exceeded. Try again in 30s.") and [Retry] button
- **Authentication Error:** "Invalid API key for [provider]. Check API key in Settings."
- **Network Error:** "Network error. Check your internet connection." with [Retry]
- **Consecutive Failures:** After 3 consecutive failures, escalation message: "Multiple failures detected. Check your API key, network connection, or provider status."
- **Timeout:** "Request timed out after [N] seconds." with [Retry]

### C15. Scroll-to-Bottom Button
- Floating button at bottom-right of chat view
- Appears when: user scrolled up AND new assistant message is streaming
- Click: smooth scroll to latest message
- Auto-hides when at bottom

### C16. Clear Conversation
- **Access:** Chat header three-dot (⋯) menu → "Clear Conversation"
- Confirmation dialog: "This will remove all messages from this chat. The chat will remain in your sidebar with its title, tags, and settings."
- After clearing: chat shows empty state with Persona info
- **Undo:** Brief toast with "Undo" option (5 second window, Ctrl+Z also works per D9)

### C16a. Chat Header Three-Dot Menu (⋯)
The chat header contains a three-dot menu with the following items:
- **Clear Conversation** (C16)
- **Export Chat** (I1) — Opens export format dialog
- **Duplicate Chat** (D7) — Creates fork from current point
- **Chat Tree** (D4) — Opens tree visualization
- **Edit System Message** (E5) — Opens system message editor
- **Summarize Chat** (C32) — AI generates summary
- **Make Temporary** (C30) — Toggle incognito mode

### C17. Auto-Scroll Behavior
- **Pause:** When user scrolls up during active generation, auto-scroll pauses. Indicator: "Auto-scroll paused." Click to resume.
- **Media Loading:** When image/video renders (height change), viewport adjusts smoothly without jarring reading position

### C18. Message Selection Mode
- **Enter:** "Select Messages" button in chat header three-dot (⋯) menu, OR checkboxes appear on message hover
- **Checkbox Behavior:** Checkbox appears at the left edge of each message on hover. Click to select/deselect.
- **Bulk Actions Bar:** Appears as a floating bar at the top of the conversation view when messages are selected: "[N] selected — Copy Selected | Delete Selected | Quote Selected | Cancel"
- **Deselect:** Click "Cancel" in bulk bar or click outside messages
- **Visual:** Selected messages have a highlighted border

### C19. Offline/Network Status Indicator
- Small colored dot in the status bar at the very bottom of the Studio window (right side)
- Green = online, Yellow = slow connection, Red = offline
- When offline: yellow banner below chat header: "You are offline. AI responses are unavailable until connection is restored."
- Auto-detects reconnection; banner auto-dismisses

### C20. Close Confirmation with Active Generation
- Trigger: Close tab or window while AI is generating
- Dialog: "A response is still being generated. Are you sure you want to close?" Options: "Wait for response" / "Close anyway"

### C21. Audio Input (Microphone)
- Microphone button in textbox toolbar
- Click to start recording → button pulses red during recording → click to stop
- Audio sent to configured STT Provider (A10)
- Transcribed text appears in textbox, editable before sending

### C22. Camera Capture
- Camera button in textbox toolbar
- Opens webcam capture dialog (live preview + "Capture" button)
- Captured image immediately attached to current message

### C23. Pin Window / Always on Top
- Toggle button in Studio window header (pin icon)
- State remembered across sessions

### C24. Dark/Light Mode Quick Toggle
- Sun/Moon icon button in the chat header bar (right side, near pin toggle)
- Click toggles between dark mode and light mode (A5)
- Instant transition with no page reload
- State remembered across sessions

### C25. Font Size Quick Adjust
- Two small buttons (A⁻ / A⁺) in the chat header bar
- Range: 10-24px. Current size displayed (e.g., "14")
- Only affects chat messages, not UI chrome

### C26. Model Comparison Button
- "Compare" button (⚖ icon) in the textbox toolbar (K2)
- Click opens Model Comparison setup (M2)

### C27. Dynamic System Message Editing Access
- Click the Persona name in the chat header → popover appears
- Also accessible from: Chat Navigation Bar (D6), three-dot menu

### C28. Duplicate / Fork Chat Access
- Right-click any message → "Fork from here" (D7)
- Also in chat header three-dot menu → "Duplicate Chat"

### C29. Chat Header Layout (Complete)
Top-to-bottom in the main content area:
1. **Tab Bar:** Tabs (chat tabs + file viewer tabs) + "+" new chat button
2. **Chat Header Bar (left to right):**
   - Active Persona name (clickable → system message editor C27)
   - Chat theme selector: Classic / Compact / Bubble (A3)
   - 📡 API History button (opens `_api_history.json` in file viewer tab)
   - Context window bar: "12,450 / 128,000 tokens" with colored fill
   - Cumulative cost: "$0.42 total"
   - [Source: App — 'Document'] banner + [Apply Latest] (only for Tier 1 elevation, C5a)
   - Spacer
   - A⁻ 14 A⁺ (font size, C25)
   - ☀/🌙 (dark mode toggle, C24)
   - 📌 (pin window toggle, C23)
   - ? (Help icon — dropdown: App Data Locations, Keyboard Shortcuts, About)
   - ⋯ (three-dot menu, C16a)
3. **Conversation View** (C1-C8, C14-C15, C17-C18)
4. **Message Input Area** with toolbar (C21-C22, C26, K2)

### C30. Incognito / Temporary Studio Chat
- **Toggle:** "Incognito" toggle in chat header three-dot (⋯) menu or right-click chat tab → "Make Temporary"
- **When Active:** Chat marked IsTransient=true. 🕶️ indicator on tab.
- **Elevation:** Same elevation rules as Tier 1/2 (O3)

### C31. Locked Chats
- Password-protected encryption (AES-256-GCM). Global default + per-chat override.
- ⚠️ FLAGGED: No recovery if password lost.

### C32. Chat Summarization
- Three-dot menu → "Summarize Chat" → AI generates summary → save as artifact, export, or copy.

### C33. Message Favoriting
- Star (★/☆) per message. Filter in Chat Nav and global search.

### C34. Spell Check in Textbox
- Red squiggly underline. Right-click suggestions. Toggle in Settings.

### C35. Cross-Tab Completion Alert
- Pulsing green dot on inactive tab when generation completes. Configurable in Settings.

### C36. Auto-Save Message Drafts
- Textbox content auto-saves every 5 seconds. Recover unsent drafts after crash.

### C37. Right Panel Layout
Two vertically stacked resizable sections:
- **Top: Artifacts (F2).** WebView2-powered. Lists presented artifacts.
- **Bottom: Chat Navigation (D6).** Scrollable message list with favorited filter.

### NEW: C38. API History Button

"📡 API History" button in the chat header bar (between chat theme selector and context window bar):
- Always visible when a chat is active
- Click → generates/retrieves the per-chat raw API call JSON log → opens in a new read-only file viewer tab
- Tab title: "API History — [Chat Title]"
- Full spec: [`features/api-history-viewer.md`](api-history-viewer.md)

### NEW: C39. Generic File Viewer Tabs

Read-only file viewer tabs open alongside chat tabs in the main content area:
- **Supported:** text, markdown (rendered), code (syntax highlighting), images (zoom/pan)
- **Open via:** Ctrl+O, drag & drop onto tab bar, double-click in file picker, present_files results, API History button, click image thumbnail
- **Tab behavior:** Same as chat tabs (reorder, close, reopen). 📄 icon prefix + "Read-Only" badge.
- **Drag to textbox:** Include file content as chat context
- Full spec: [`features/file-viewer-tabs.md`](file-viewer-tabs.md)

### NEW: C40. "?" Help Icon

"?" icon in the app header bar (right side, near ⋯ menu):
- Click opens dropdown: App Data Locations, Keyboard Shortcuts, About MySecondBrain
- "App Data Locations" navigates to Settings → System Info
- Full spec: [`features/app-data-locations.md`](app-data-locations.md)

## Data
- [`data/chat-thread.md`](data/chat-thread.md)
- [`data/message.md`](data/message.md)
- [`data/usage-record.md`](data/usage-record.md)
- Per-chat raw JSON log: `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/_api_history.json`

## Success/Failure States
- **Empty State — No Chats:** "No chats yet. Press Ctrl+N to start a new conversation."
- **Empty State — Cleared Chat:** Persona info card
- **Loading State — Chat History:** Skeleton loading placeholders
- **Error State — Chat Not Found:** "This chat no longer exists."

## Permissions
- Single-user app. All actions available to the sole user.

## Interactions
- C38: API History → [`features/api-history-viewer.md`](api-history-viewer.md), [`features/file-viewer-tabs.md`](file-viewer-tabs.md)
- C39: File Viewer → [`features/file-viewer-tabs.md`](file-viewer-tabs.md)
- C40: Help → [`features/app-data-locations.md`](app-data-locations.md)
- References: A3 (themes), H (14 tools), K (text actions), E (chat modes), F (artifacts)
