# Studio Chat Workspace — Feature Spec

## What the User Accomplishes
The user engages in deep, multi-turn AI conversations with full Markdown rendering, code syntax highlighting, streaming responses, multiple chat tabs, and comprehensive message actions. This is the Tier 3 primary workspace.

## Trigger
- Open main application window
- Click system tray → "Open Studio"
- Elevate from Tier 1 (Open in Studio) or Tier 2 (Open in Studio)
- Ctrl+N (new chat)

## Layout
The Studio window uses a sidebar + main content layout with tabbed chats. See [`screens/studio-chat.md`](screens/studio-chat.md) for the full screen specification.

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
- **Stop:** Visible only during generation. Stops and keeps partial response.
- **Regenerate:** Visible on last assistant message. Replaces with new generation.
- **Edit:** Opens message in editable mode (D1)
- **Delete:** Removes message from history (D2)
- **Copy:** Places raw Markdown + Rich Text (HTML/RTF) on clipboard (C6)

### C5a. Conditional [Apply] Button
See [`features/windows-os-integration.md`](features/windows-os-integration.md) P3 for full spatial anchoring spec.

- **Source Indicator Banner:** Appears in chat header when chat originated from Tier 1 text transformation. Shows: "[Source: {App Name} — '{Document Title}']" + [Apply Latest] button
- **Per-Message [Apply]:** On each assistant message that was a direct transformation of captured text. NOT on follow-up query responses.
- **[Apply Latest]:** Pushes most recent assistant message to source application
- **Grayed Out State:** If source window closed: "[Apply] — Source application is no longer available"
- **No HWND Context:** Chats created directly in Studio or elevated from Tier 2 never show [Apply]

### C6. Copy
- Per-message Copy button: places BOTH raw Markdown and Rich Text (HTML/RTF) on clipboard simultaneously
- Destination app auto-selects best format on paste
- Menu option: "Copy Entire Conversation" → copies as Markdown or plain text (user choice)

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
- **Text files:** Content read and included in prompt
- **Other files:** Metadata (filename, type, size) included in prompt context
- **Drop Indicator:** Visual feedback during drag-over

### C10. Multiple Chat Tabs
- Tab bar above chat view. Each tab = independent ChatThread.
- **Reorder:** Drag-and-drop tabs
- **Close:** X button on tab or Ctrl+W
- **Reopen:** Ctrl+Shift+T reopens last closed tab
- **Overflow:** When tabs exceed width, scrollable tab bar with arrows
- **Tab Title:** Chat title (C7), truncated if needed

### C11. Token Usage & Context Display
- **Per-Message:** Each assistant message shows: prompt tokens + completion tokens = total. Estimated cost (if pricing configured).
- **Chat Header:** Shows (a) Current Context Size — "12,450 / 128,000 tokens" with colored bar (green/yellow/red as limit approaches), and (b) Cumulative Chat Cost — total estimated cost across ALL branches
- **Real-Time:** Context size updates as user types, using local tokenizer library (not API response headers)

### C12. Keyboard Shortcuts (Studio)
- Ctrl+N: New Chat (opens Persona picker)
- Ctrl+W: Close Current Tab
- Ctrl+Shift+T: Reopen Last Closed Tab
- Ctrl+Tab: Next Tab
- Ctrl+Shift+Tab: Previous Tab
- Ctrl+F: Search Within Current Chat
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
- Accessible from chat header menu or right-click
- Confirmation dialog: "This will remove all messages from this chat. The chat will remain in your sidebar with its title, tags, and settings."
- After clearing: chat shows empty state with Persona info

### C17. Auto-Scroll Behavior
- **Pause:** When user scrolls up during active generation, auto-scroll pauses. Indicator: "Auto-scroll paused." Click to resume.
- **Media Loading:** When image/video renders (height change), viewport adjusts smoothly without jarring reading position

### C18. Message Selection Mode
- **Enter:** Checkbox on each message OR toolbar "Select Messages" button
- **Select:** Click checkboxes or click message while in selection mode
- **Bulk Actions Bar:** Appears at top when messages selected: Copy Selected, Delete Selected, Quote Selected
- **Deselect:** Click "Done" or exit selection mode

### C19. Offline/Network Status Indicator
- Small dot indicator in status bar/top bar
- Green = online, Yellow = slow connection (high latency detected), Red = offline
- When offline: banner "You are offline. AI responses are unavailable until connection is restored."
- Auto-detects reconnection

### C20. Close Confirmation with Active Generation
- Trigger: Close tab or window while AI is generating
- Dialog: "A response is still being generated. Are you sure you want to close?" Options: "Wait for response" / "Close anyway"
- "Wait for response": keeps tab open, generation continues

### C21. Audio Input (Microphone)
- Microphone button in textbox toolbar
- Click to start recording → button pulses red during recording → click to stop
- Audio sent to configured STT Provider (A10)
- Transcribed text appears in textbox, editable before sending
- **Error:** "Speech recognition failed. Check your microphone and STT settings."

### C22. Camera Capture
- Camera button in textbox toolbar
- Opens webcam capture dialog (live preview + "Capture" button)
- Captured image immediately attached to current message
- Sent to AI (for vision-capable models)

### C23. Pin Window / Always on Top
- Toggle button in Studio window header (pin icon)
- When active: window stays on top of other applications
- State remembered across sessions
- Visual indicator on header when pinned

## Data
- [`data/chat-thread.md`](data/chat-thread.md), [`data/message.md`](data/message.md), [`data/usage-record.md`](data/usage-record.md)

## Success/Failure States
- **Empty State — No Chats:** "No chats yet. Press Ctrl+N to start a new conversation."
- **Empty State — Cleared Chat:** Shows Persona info: "[Persona Name] — [System prompt preview]. Start typing below."
- **Loading State — Chat History:** Skeleton loading placeholders for messages
- **Loading State — AI Generating:** Streaming tokens or "Generating..." spinner (if streaming disabled)
- **Error State — Chat Not Found:** "This chat no longer exists. It may have been deleted."

## Permissions
- Single-user app. All actions available to the sole user.

## Interactions
- References: A3 (themes), A4 (notifications/streaming), A10 (STT), B4 (Persona selection), C5a (Tier 1 elevation), D (branching), E (modes), F (artifacts), G (media), H (tools), I (export), J (prompts), K (text actions), L (organization), M (comparison), N (wiki), P3 (spatial anchoring)
