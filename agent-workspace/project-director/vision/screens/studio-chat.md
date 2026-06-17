# Studio Chat — Screen Specification

## Purpose
The Studio Chat is the Tier 3 primary workspace where the user engages in deep, multi-turn AI conversations. It provides the full chat experience: Markdown rendering, code syntax highlighting, streaming responses, multiple chat tabs, message branching, artifacts, model comparison, and all quality-of-life features. The user spends 90% of their time on this screen.

## Layout
Three-column resizable layout:
- **Left:** Sidebar (app navigation + chat list)
- **Center:** Main content (tab bar + chat header + conversation view + message input)
- **Right:** Collapsible side panel (Artifacts tab + Chat Nav tab)

## Regions

### Region 1: Sidebar (Left Column)
**Width:** Resizable, min 150px, max 50% of window. Default ~280px.

**App Navigation (top section):**
Six icon+label navigation items:
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
- Each entry: title, last message preview (1 line, truncated), relative timestamp, star (★/☆), tags (max 3), color dot
- Right-click context menu: Rename, Delete, Archive, Duplicate, Export, Pin, Tags, Move to Folder, Color Label, Lock Chat (C31)
- "Reveal Locked Chats" button (🔒 icon) at bottom of chat list (if hidden locked chats enabled, C31)
- "+ New Chat" button at top of chat list

**Empty State (no chats):**
"No chats yet. Press Ctrl+N to start a new conversation."

### Region 2: Main Content (Center Column)

**Tab Bar:**
- Horizontal row of chat tabs. Each: chat title (truncated if needed) + close X
- "+" button at right end — opens Persona picker → creates new chat
- Draggable reordering
- Overflow: scrollable with left/right arrows
- Active tab highlighted
- Incognito tabs show 🕶️ icon
- Tab completion indicator: green dot on inactive tabs (C35)
- Right-click tab: Close, Close Others, Close All, Make Temporary (C30), Lock Chat (C31)

**Chat Header Bar (left to right):**
1. Active Persona name (clickable → system message editor popover, C27)
2. Context window bar: "12,450 / 128,000 tokens" with colored fill (green→yellow→red)
3. Cumulative cost: "$0.42 total" (across all branches)
4. [Source: App — 'Document'] banner + [Apply Latest] (only for Tier 1 elevation, C5a)
5. Spacer
6. A⁻ 14 A⁺ (font size quick adjust, C25)
7. ☀/🌙 (dark mode quick toggle, C24)
8. 📌 (pin window toggle, C23)
9. ⋯ (three-dot menu): Clear Conversation, Export Chat, Duplicate Chat, Chat Tree, Edit System Message, Summarize Chat, Make Temporary

**Conversation View:**
- Scrollable message history for the active ChatThread
- Messages rendered per selected visual theme (A3): Classic, Compact, or Bubble
- Each message shows:
  - Role label: "You" (user) or Persona name (assistant)
  - Relative timestamp ("2 min ago"), hover for full date+time
  - Message content with full Markdown rendering (C2)
  - Code blocks with syntax highlighting + copy button on hover (C3)
  - Streaming tokens during generation (C4)
  - Token count + estimated cost (C11)
  - Generation time on completion ("Generated in 3.2s")
- Per-message action bar (visible on hover or always, per theme):
  - ⭐ Star/Unstar (message favoriting, C33)
  - 📋 Copy (Markdown + Rich Text to clipboard, C6)
  - ✏️ Edit (D1)
  - 🗑️ Delete (D2)
  - 🔄 Regenerate (assistant messages only, C5)
  - [Apply] (only for Tier 1 direct transformations, C5a)
  - 👍/👎 Thumbs up/down (D8)
- Branch indicators on edited messages: "v2/3" with ← → arrows (D3)
- "Continue" button when last message is from assistant (C8)
- Message selection checkboxes on hover (C18)
- Bulk actions bar when messages selected: "[N] selected — Copy | Delete | Quote"
- Floating "Scroll to bottom" button (C15)
- "Auto-scroll paused" indicator when scrolled up during generation (C17)

**Message Input Area (bottom of main content):**
- **Toolbar row (above textbox):**
  - Persona selector dropdown (B4)
  - Thinking toggle (E3)
  - Mute notifications toggle (E4)
  - Tools enable/disable toggle (H)
  - Auto-approval per-chat override (H5)
  - Memory toggle: "Memory: On/Off" (N12)
  - Model Comparison button (⚖, C26)
  - Prompt Library button (J1)
  - Text Actions dropdown (K2)
  - Microphone button (C21)
  - Camera button (C22)
  - "Write to Wiki" button (N5)
  - "Update Memory" button (N12)
- **Textbox:** Multi-line, resizable. Spell check with red squiggly underlines (C34). Real-time token counter with pre-send warning when approaching context limit (C11).
- **Attachment row (below textbox):** Thumbnails for pasted/dropped images and files (C9, C9a). Each with X to remove.
- **Bottom row:** Send button (or Enter). Character/token count. "Stop" button (visible during generation, C5).

### Region 3: Right Panel (Collapsible)
**Width:** Resizable, min 200px, max 50%. Default ~350px. Collapsible via toggle in chat header or divider.

**Two tabs:**

**Artifacts Tab (F2):**
- Lists all artifacts from current chat: name + type icon
- Click to view content with syntax highlighting (F6)
- Version dropdown per artifact (F3)
- "Save to Disk" and "Save to Wiki" buttons (F6)
- Empty state: "No artifacts in this chat yet."

**Chat Nav Tab (D6):**
- Scrollable list of all messages in current branch
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

## Actions (with behavioral tooltips)

| Element | Action | Tooltip |
|---------|--------|---------|
| 💬 Chats nav | Active screen | — |
| 📝 Wiki nav | Navigate to Wiki Browser | "Would open the Wiki Browser to browse and search your personal wiki files." |
| 🖼️ Media nav | Navigate to Media Library | "Would open the Media Library showing all images, audio, and video across all chats." |
| 📄 Artifacts nav | Navigate to Artifacts Browser | "Would open the Global Artifacts Browser showing all code, documents, and config files from all chats." |
| 📊 Usage nav | Navigate to Usage Dashboard | "Would open the Usage Dashboard with token usage charts, cost breakdowns, and budget alerts." |
| ⚙️ Settings nav | Navigate to Settings | "Would open the Settings screen for API keys, Personas, appearance, hotkeys, and all other configuration." |
| Search bar | Full-text search | "Would search all chat messages and display results grouped by chat. Click a result to open that chat and scroll to the matching message." |
| + New Chat button | Create new chat | "Would open the Persona picker. Selecting a Persona creates a new chat tab with that Persona's system prompt, model, and mode." |
| Chat tab + button | Create new chat | "Would open the Persona picker to create a new chat tab." |
| Chat tab X button | Close chat tab | "Would close this chat tab. If unsaved, chat is preserved in sidebar. Ctrl+Shift+T to reopen." |
| Persona name (header) | Edit system message | "Would open a popover showing the current system message for editing. Changes take effect for subsequent messages. 'Reset to Persona Default' available." |
| ⋯ three-dot menu | Context menu | "Would open menu: Clear Conversation, Export Chat, Duplicate Chat, Chat Tree, Edit System Message, Summarize Chat, Make Temporary." |
| 📌 Pin toggle | Always on top | "Would pin this window to stay on top of other applications. State remembered across sessions." |
| ☀/🌙 Dark mode | Toggle theme | "Would instantly switch between dark mode and light mode for the entire application." |
| A⁻ / A⁺ | Font size adjust | "Would decrease/increase chat message font size by 1px. Current size displayed between buttons. Range: 10-24px." |
| ⭐ on message | Favorite message | "Would star this message for quick access. Favorited messages can be filtered in Chat Nav and global search." |
| 📋 Copy | Copy message | "Would copy both raw Markdown and Rich Text (HTML/RTF) to clipboard. Destination app selects best format." |
| ✏️ Edit | Edit message | "Would open message in editable mode. Choose 'Edit in Place' (overwrite) or 'Edit as Branch' (new version). AI sees updated history." |
| 🗑️ Delete | Delete message | "Would remove this message from the current conversation history. Branch data preserved. Undo with Ctrl+Z." |
| 🔄 Regenerate | Regenerate response | "Would replace this assistant response with a new AI generation. Old response preserved as a branch." |
| [Apply] | Apply to source | "Would push this text back into the original source application. Uses HWND injection or clipboard fallback. Only available for Tier 1 elevated chats with live source window." |
| 👍/👎 | Message feedback | "Would record your feedback on this AI response. Stored locally for your reference. Toggle to remove." |
| Continue button | Continue generation | "Would send a continuation request to the AI, picking up where the last response left off. For models supporting assistant prefix continuation." |
| ⚖ Compare | Model comparison | "Would open Model Comparison setup: select 2+ Personas, enter a prompt, and see responses side-by-side in real time." |
| Send button | Send message | "Would validate token count against context limit. On success: sends message to AI. On warning: shows pre-send token warning if approaching limit." |
| Stop button | Stop generation | "Would immediately stop the AI response generation. Partial response preserved in chat." |
| 🎤 Microphone | Voice input | "Would start recording from your microphone. Click again to stop. Audio sent to configured STT provider. Transcribed text appears in textbox." |
| 📷 Camera | Camera capture | "Would open webcam capture dialog. Take a photo to attach to the current message for vision-capable models." |
| Write to Wiki | Create wiki entry | "Would open the Write to Wiki dialog: create new or update existing wiki file from this conversation. AI generates polished summary with cross-links." |
| Update Memory | Update AI memory | "Would trigger AI to read this chat and update _memory.md with new facts and preferences about you. You review changes via Diff Viewer before saving." |
| Memory toggle | Memory aware | "Would toggle whether _memory.md content is injected into system context for this chat. When On, AI knows what it has learned about you." |
| 🔒 Reveal Locked | Show locked chats | "Would prompt for password to reveal hidden locked chats in the sidebar." |
| Chat context menu > Lock | Lock chat | "Would encrypt this chat with a password (or global default). Without password, chat becomes permanently inaccessible. Can be hidden from sidebar." |
| Chat context menu > Make Temporary | Incognito mode | "Would mark this chat as temporary (IsTransient=true). Subject to 7-day auto-cleanup unless elevated by sending a reply." |

## Empty States
- **No chats:** "No chats yet. Press Ctrl+N to start a new conversation."
- **Cleared chat:** Shows Persona info card: "[Persona Name] — [System prompt preview]. Start typing below."
- **No artifacts:** "No artifacts in this chat yet."
- **Chat Nav empty:** Only appears with messages in the chat.

## Loading States
- **Chat list loading:** Skeleton placeholder entries (gray bars)
- **Chat history loading:** Skeleton message placeholders (alternating wide/narrow bars)
- **AI generating:** Streaming tokens, or "Generating..." spinner if streaming disabled (A4)
- **Model comparison loading:** All panels show simultaneous spinners

## Error States
- **API failure:** Red error banner on assistant message with specific error + [Retry]
- **Chat not found:** "This chat no longer exists. It may have been deleted."
- **Network offline:** Yellow banner below header: "You are offline. AI responses are unavailable." Status bar dot: red.
- **Locked chat (no password):** Password prompt dialog. If wrong password: "Incorrect password. This chat is encrypted and cannot be accessed without the correct password."

## Navigation
- **Arrive from:** App launch (session restore or new chat), system tray → Open Studio, elevation from Tier 1 (Open in Studio) or Tier 2 (Open in Studio)
- **Navigate to:** Wiki Browser (`wiki-browser.html`), Media Library (`media-library.html`), Artifacts Browser (`global-artifacts-browser.html`), Usage Dashboard (`usage-dashboard.html`), Settings (`settings.html`), Model Comparison (inline view, M), Onboarding Wizard (`onboarding-wizard.html` — on first launch)

## Cross-References
- Displays data from: [`data/chat-thread.md`](data/chat-thread.md), [`data/message.md`](data/message.md), [`data/persona.md`](data/persona.md), [`data/usage-record.md`](data/usage-record.md)
- Supports features: C (Studio Chat Workspace), D (Message Branching), E (Chat Modes), F (Artifacts), H (Tool Use), J (Prompt Library), K (Text Actions), L (Chat Organization), M (Model Comparison), N (Personal Wiki), C30-C35
