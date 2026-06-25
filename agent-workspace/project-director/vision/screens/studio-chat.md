# Studio Chat — Screen Specification

## Purpose

The Studio Chat is the Tier 3 primary workspace where the user engages in deep, multi-turn AI conversations. It provides the full chat experience: Markdown rendering, code syntax highlighting, streaming responses, multiple chat tabs, message branching, WebView2-powered artifacts panel, model comparison, and all quality-of-life features. The user spends 90% of their time on this screen.

## Layout

Three-column resizable layout:
- **Left:** Sidebar (app navigation + chat list)
- **Center:** Main content (tab bar + chat header + conversation view [WPF] + message input)
- **Right:** Collapsible side panel (Artifacts [WebView2] + Chat Nav [WPF])

## Regions

### Region 1: Sidebar (Left Column)

**Width:** Resizable, min 150px, max 50% of window. Default ~280px.

**App Navigation (top section):**

Seven icon+label navigation items:
1. 💬 Chats (active by default — this screen)
2. 📝 Wiki — navigates to [`wiki-browser.html`](wiki-browser.html)
3. 🖼️ Media — navigates to [`media-library.html`](media-library.html)
4. 📄 Artifacts — navigates to [`global-artifacts-browser.html`](global-artifacts-browser.html)
5. 📊 Usage — navigates to [`usage-dashboard.html`](usage-dashboard.html)
6. ⚙️ Settings — navigates to [`settings.html`](settings.html)

**Search Bar:**
- Full-text search input (Ctrl+Shift+F). Searches all chat messages.
- Results appear inline below search bar, grouped by chat.

**Tabs:**
- "Chats" (active) | "Timeline"
- Toggle between permanent chat list and transient actions feed

**Chat List:**
- Sort dropdown: Most Recent (default), Name A-Z, Date Created, Last Activity
- Pinned chats section (top, above date groups)
- Date groups: Today, Yesterday, This Week, This Month, Older
- Each entry: title, last message preview (1 line, truncated), relative timestamp, star (★/☆), tags (max 3), color dot, ⋯ (three-dot menu button)
- ⋯ button (visible on hover): Opens context menu with Rename, Delete, Archive, Duplicate, Export, Pin, Tags, Move to Folder, Color Label, Lock Chat (C31). Right-click also opens same menu.
- "Reveal Locked Chats" button (🔒 icon) at bottom of chat list (if hidden locked chats enabled, C31)
- "+ New Chat" button at top of chat list

**Empty State (no chats):**
"No chats yet. Press Ctrl+N to start a new conversation."

### Region 2: Main Content (Center Column) — WPF-Native

**Tab Bar:**
- Horizontal row of tabs — chat tabs AND file viewer tabs
- **Chat tabs:** chat title (truncated if needed) + close X
- **File viewer tabs:** 📄 filename + "Read-Only" badge + close X (C39)
- "+" button at right end — opens Persona picker → creates new chat
- Draggable reordering (both chat and file viewer tabs)
- Overflow: scrollable with left/right arrows
- Active tab highlighted
- Incognito tabs show 🕶️ icon
- Tab completion indicator: green dot on inactive tabs (C35)
- Right-click tab: Close, Close Others, Close All, Make Temporary (C30), Lock Chat (C31)
- **Drag file viewer tab to textbox:** Includes file content as chat context (C39)

**Chat Header Bar (left to right):**
1. Active Persona name (clickable → system message editor popover, C27)
2. Chat theme selector: Classic / Compact / Bubble (A3)
3. 📡 API History button (opens `_api_history.json` in file viewer tab, C38)
4. Context window bar: "12,450 / 128,000 tokens" with colored fill (green→yellow→red)
5. Cumulative cost: "$0.42 total" (across all branches)
6. [Source: App — 'Document'] banner + [Apply Latest] (only for Tier 1 elevation, C5a)
7. Spacer
8. A⁻ 14 A⁺ (font size quick adjust, C25)
9. ☀/🌙 (dark mode quick toggle, C24)
10. 📌 (pin window toggle, C23)
11. ? (Help icon — dropdown: App Data Locations, Keyboard Shortcuts, About, C40)
12. ⋯ (three-dot menu): Clear Conversation, Export Chat, Duplicate Chat, Chat Tree, Edit System Message, Summarize Chat, Make Temporary

**Conversation View (WPF FlowDocument):**
- Scrollable message history for the active ChatThread
- Messages rendered per selected visual theme (A3): Classic, Compact, or Bubble
- Each message shows:
  - Role label: "You" (user) or Persona name (assistant)
  - Relative timestamp ("2 min ago"), hover for full date+time
  - Message content with full Markdown rendering (C2)
  - Code blocks with syntax highlighting + copy button on hover (C3)
  - Streaming tokens during generation (C4)
  - **Thinking Block (when Thinking is enabled, E3):**
    - Collapsible section within the assistant message, ABOVE the final response
    - **During thinking (streaming):** Collapsed by default. Header shows "🧠 Thinking... [N]s" with real-time second counter. Click to expand.
    - **When thinking completes:** Header updates to "🧠 Thinking complete ([N]s)". Final response streams below.
    - **When Thinking disabled (E3 off):** No thinking block.
  - Token count + estimated cost (C11)
  - Generation time on completion ("Generated in 3.2s")
- Per-message action bar (visible on hover or always, per theme):
  - ⭐ Star/Unstar (C33)
  - 📋 Copy (Markdown + Rich Text, C6)
  - ✏️ Edit (D1)
  - 🗑️ Delete (D2)
  - 🔄 Regenerate (assistant only, C5)
  - [Apply] (only for Tier 1 direct transformations, C5a)
  - 👍/👎 Thumbs up/down (D8)
- Branch indicators on edited messages: "v2/3" with ← → arrows (D3)
- "Continue" button when last message is from assistant (C8)
- Message selection checkboxes on hover (C18)
- Bulk actions bar: "[N] selected — Copy | Delete | Quote"
- Floating "Scroll to bottom" button (C15)
- "Auto-scroll paused" indicator when scrolled up during generation (C17)

**Tool Call Messages (rendered inline):**
- Tool call system messages shown as styled cards:
  - **read_file:** "📖 Reading: [path]"
  - **list_files:** "📂 Listing: [path]"
  - **search_files:** "🔎 Searching: [regex] in [path]"
  - **apply_diff:** "✏️ Editing: [path]"
  - **write_to_file:** "✍️ Writing: [path]"
  - **bash:** "🖥 bash: [command preview]"
  - **web_search:** "🔍 Searching: [query]"
  - **web_fetch:** "🌐 Fetching: [URL]"
  - **image_search:** "🖼️ Image search: [query]"
  - **memory:** "🧠 Memory: [store/retrieve] [key]"
  - **wiki_search:** "📚 Wiki search: [query]"
  - **skill_load:** "📚 Loaded skill: [name]"
  - **ask_user_input:** "❓ Asking: [question]"
  - **present_files:** "📄 Presented: [filename(s)]"
- **Parallel execution indicator:** "⚡ Running [N] tools in parallel…" → "⚡ [N] tools completed in [T]ms"
- Tool results shown as collapsible result blocks
- During streaming: tool calls appear progressively as model generates them

**Message Input Area (bottom of main content):**

**Toolbar row (above textbox) — left to right:**
1. **Persona selector dropdown** (B4) — [Persona Name ▼]
2. **Thinking toggle** (E3) — 🧠 icon, toggle on/off
3. **Mute toggle** (E4) — 🔇 icon, toggle on/off
4. **Tools dropdown** (H) — 🔧 "Tools ▼" with checkboxes for all 14 tools (read_file, list_files, search_files, apply_diff, write_to_file, bash, web_search, web_fetch, image_search, wiki_search, memory, skill_load, ask_user_input, present_files). "All on/off" at top. Auto-approval submenu per tool (out-of-workspace read access: Auto-Approve/Ask/Disabled per read tool).
5. **Skills dropdown** (W6) — 📚 "Skills ▼" with checkboxes for each discovered skill + "All on/off"
6. **Memory toggle** (W8) — 🧠 "Mem" toggle on/off
7. 📎 Attach File button (C9b)
8. ⚖ Model Comparison button (C26)
9. 📋 Prompt Library button (J1)
10. ✨ Text Actions dropdown (K2)
11. 🎤 Microphone button (C21)
12. 📷 Camera button (C22)
13. 📝 "Write to Wiki" button (N5)

**Textbox:** Multi-line, resizable. Spell check with red squiggly underlines (C34). Real-time token counter with pre-send warning when approaching context limit (C11).

**Attachment row (below textbox):** Thumbnails for pasted/dropped images and files (C9, C9a). Each with X to remove.

**Bottom row:** Send button (or Enter). Character/token count. "Stop" button (visible during generation, C5).

### Region 3: Right Panel (Collapsible)

**Width:** Resizable, min 200px, max 50%. Default ~350px. Collapsible via toggle in chat header or divider.

**Two vertically stacked sections:**

**Artifacts Panel (Top — WebView2-Powered):**
- Embedded Microsoft Edge WebView2 control renders all artifact content
- **Artifact List (WPF-native chrome):** Lists all presented artifacts from current chat: filename + type icon + version badge
- Click artifact → WebView2 loads and renders content:
  - Code files: syntax highlighting (200+ languages via Prism.js/highlight.js), line numbers, copy button
  - Markdown: rendered via marked.js with proper typography
  - Interactive HTML/React: full React app rendering for web-artifacts-builder output (F6)
  - Diff view: side-by-side or unified via diff2html.js with change navigation (F4)
  - SVG: scalable vector rendering
  - PDF: browser-native PDF viewer
  - Unknown types: plain text with monospace font
- **Preview/Code toggle:** Switch between rendered view and raw source
- **Version selector dropdown** (WPF-native): v1, v2, v3... with timestamps
- **Action buttons** (WPF-native): "Save to Disk" and "Save to Wiki"
- **Theme:** Dark/light injected via JavaScript bridge from WPF theme
- **Empty state:** "No artifacts in this chat yet. The AI can create files and present them here."
- **WebView2 unavailable:** "WebView2 runtime not available. Install Microsoft Edge WebView2 for full artifact rendering." Fallback to WPF-based rendering with limited highlighting.

**Chat Nav Panel (Bottom — WPF-Native):**
- Scrollable list of all messages in current branch (D6)
- Each entry: message number (#1, #2...), role icon, first line preview
- Active message highlighted
- Click to scroll conversation to that message
- "Favorited" filter (C33)

## Data Displayed

- Chat list from [`data/chat-thread.md`](data/chat-thread.md)
- Messages from [`data/message.md`](data/message.md)
- Active Persona from [`data/persona.md`](data/persona.md)
- Context/cost from [`data/usage-record.md`](data/usage-record.md)
- Source context (HWND data) from ChatThread when applicable
- Artifacts from [`data/artifact.md`](data/artifact.md)
- Skills from in-memory discovery (not persisted)

## Actions (with behavioral tooltips)

| Element | Action | Tooltip |
|---------|--------|---------|
| 💬 Chats nav | Active screen | — |
| 📝 Wiki nav | Navigate to Wiki Browser | "Would open the Wiki Browser to browse and search your personal wiki files." |
| 🖼️ Media nav | Navigate to Media Library | "Would open the Media Library showing all images, audio, and video across all chats." |
| 📄 Artifacts nav | Navigate to Artifacts Browser | "Would open the Global Artifacts Browser showing all code, documents, and config files from all chats." |
| 📊 Usage nav | Navigate to Usage Dashboard | "Would open the Usage Dashboard with token usage charts, cost breakdowns, and budget alerts." |
| ⚙️ Settings nav | Navigate to Settings | "Would open the Settings screen for API keys, Personas, appearance, hotkeys, and all other configuration." |
| Search bar | Full-text search | "Would search all chat messages and display results grouped by chat." |
| + New Chat button | Create new chat | "Would open the Persona picker to create a new chat tab." |
| Persona name (header) | Edit system message | "Would open a popover showing the current system message for editing." |
| ⋯ three-dot menu | Context menu | "Would open menu: Clear Conversation, Export Chat, Duplicate Chat, Chat Tree, Edit System Message, Summarize Chat, Make Temporary." |
| 📌 Pin toggle | Always on top | "Would pin this window to stay on top of other applications." |
| ☀/🌙 Dark mode | Toggle theme | "Would instantly switch between dark mode and light mode for the entire application." |
| A⁻ / A⁺ | Font size adjust | "Would decrease/increase chat message font size by 1px. Range: 10-24px." |
| 📋 Copy | Copy message | "Would copy both raw Markdown and Rich Text to clipboard." |
| ✏️ Edit | Edit message | "Would open message in editable mode. 'Edit in Place' or 'Edit as Branch'." |
| 🔄 Regenerate | Regenerate response | "Would replace this assistant response with a new AI generation." |
| [Apply] | Apply to source | "Would push this text back into the original source application." |
| 👍/👎 | Message feedback | "Would record your feedback on this AI response." |
| Continue button | Continue generation | "Would send a continuation request to the AI." |
| ⚖ Compare | Model comparison | "Would open Model Comparison setup: select 2+ Personas, side-by-side comparison." |
| Send button | Send message | "Would validate token count against context limit and send message to AI." |
| Stop button | Stop generation | "Would immediately stop AI response generation. Partial response preserved." |
| 🔧 Tools ▼ | Tools config | "Would show checkboxes for each tool (bash, text_editor, web_search, web_fetch, wiki_search, memory, skill_load, present_files). Disabled tools are removed from the API call entirely. Auto-approval submenu per tool." |
| 📚 Skills ▼ | Skills config | "Would show checkboxes for each installed skill. Disabled skills removed from catalog and skill_load enum. 'All on/off' at top." |
| 🧠 Mem | Memory toggle | "Would toggle AI memory on/off for this chat. When on, AI can store and retrieve facts about you across conversations." |
| 🎤 Microphone | Voice input | "Would start recording from your microphone. Audio sent to configured STT provider." |
| 📷 Camera | Camera capture | "Would open webcam capture dialog for vision-capable models." |
| 📎 Attach File | Attach files | "Would open a file picker to attach images, PDFs, text/code files, audio, or video." |
| Write to Wiki | Create wiki entry | "Would open the Write to Wiki dialog to create or update a wiki file from this conversation." |
| 🔒 Reveal Locked | Show locked chats | "Would prompt for password to reveal hidden locked chats in the sidebar." |

## Empty States

- **No chats:** "No chats yet. Press Ctrl+N to start a new conversation."
- **Cleared chat:** Shows Persona info card: "[Persona Name] — [System prompt preview]. Start typing below."
- **No artifacts:** "No artifacts in this chat yet. The AI can create files and present them here."
- **Chat Nav empty:** Only appears with messages in the chat.

## Loading States

- **Chat list loading:** Skeleton placeholder entries (gray bars)
- **Chat history loading:** Skeleton message placeholders (alternating wide/narrow bars)
- **AI generating:** Streaming tokens, or "Generating..." spinner if streaming disabled (A4)
- **WebView2 loading:** Brief spinner in artifacts panel while content renders
- **Model comparison loading:** All panels show simultaneous spinners

## Error States

- **API failure:** Red error banner on assistant message with specific error + [Retry]
- **Chat not found:** "This chat no longer exists. It may have been deleted."
- **Network offline:** Yellow banner below header: "You are offline. AI responses are unavailable."
- **Locked chat (no password):** Password prompt. Wrong password: "Incorrect password."
- **WebView2 unavailable:** Warning in artifacts panel: "WebView2 runtime not available. Install Microsoft Edge WebView2 for full artifact rendering."

## Navigation

- **Arrive from:** App launch (session restore or new chat), system tray → Open Studio, elevation from Tier 1 or Tier 2
- **Navigate to:** Wiki Browser, Media Library, Artifacts Browser, Usage Dashboard, Settings, Model Comparison (inline view), Onboarding Wizard (first launch)

## Cross-References

- Displays data from: [`data/chat-thread.md`](data/chat-thread.md), [`data/message.md`](data/message.md), [`data/persona.md`](data/persona.md), [`data/usage-record.md`](data/usage-record.md), [`data/artifact.md`](data/artifact.md)
- Supports features: C (Studio Chat), D (Branching), E (Chat Modes), F (Artifacts, WebView2), H (Tool Use, 10 tools), J (Prompts), K (Text Actions), L (Organization), M (Comparison), N (Wiki), W (Skills, Memory)
