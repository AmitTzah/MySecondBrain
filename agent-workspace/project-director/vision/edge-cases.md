# Edge Cases & Error Handling

## Global Edge Cases

Scenarios that apply across the entire application. These are the cross-cutting concerns that every screen and feature must handle.

---

### First-Time User Experience

**Scenario:** A brand new user launches MySecondBrain for the first time. No settings, no API keys, no chats, no wiki.

**Behavior:**
1. The Onboarding Wizard auto-launches ([`screens/onboarding-wizard.md`](screens/onboarding-wizard.md)). The Studio window is not yet visible.
2. The user can complete the wizard (Welcome → API Keys → Persona → Wiki → Hotkeys → Finish → Launch Studio) or skip any step.
3. If the wizard is closed mid-way, completed steps are saved. On next launch, the wizard resumes from the first incomplete step.
4. After onboarding, the Studio opens with a new chat tab using the configured (or default) Persona.
5. If the user skipped the Wiki step, a non-intrusive banner appears in the sidebar after the first chat: "📝 Set up your wiki to save AI conversations as permanent notes."

**Re-launching the wizard:** Available anytime from Settings → "Re-run Onboarding Wizard." Pre-populates existing settings.

**Full flow:** See [`flows/first-launch-onboarding.md`](flows/first-launch-onboarding.md).

---

### Empty States (Global Pattern)

MySecondBrain follows a consistent empty state pattern across all screens:

1. **Descriptive message:** Explains what WOULD be here and what the user should do next. Never blank.
2. **Actionable:** Where possible, includes a button or link to create the first item (e.g., "+ New Chat", "Add API Key").
3. **Not error states:** Empty states are normal (first use, cleared data). They use neutral styling — no red, no warning icons.

**Per-screen empty states are documented in each screen's `.md` spec.** Key examples:
- **Studio — no chats:** "No chats yet. Press Ctrl+N to start a new conversation."
- **Wiki Browser — no wiki dir:** "No wiki directory configured. Set up in Settings → Wiki." with link.
- **Media Library — no media:** "No media files yet. Upload images, generate AI images, or capture webcam photos during chats."
- **Settings — no API keys:** "No API keys configured. Add one to start using AI models." with "Add API Key" button.

---

### Network Errors

**Detection:** The app monitors network connectivity via the operating system's network status API. A green/yellow/red dot in the Studio status bar indicates current state.

**Behavior when offline:**
1. Status bar dot turns red. Yellow banner appears below the chat header: "You are offline. AI responses are unavailable."
2. Tier 1 hotkeys: "Thinking…" overlay → error: "Network error. Check your connection." with [Retry].
3. Tier 2 Command Bar: inline error below input: "Network error. Check your connection." with [Retry].
4. Studio chat: Send button disabled (grayed out). Tooltip: "You are offline. AI responses are unavailable."
5. Local features continue working: browsing wiki files, viewing chat history, searching existing chats, viewing media library, Settings. All read-only local operations are unaffected.

**Behavior when connection is restored:**
1. Status bar dot turns green. Offline banner disappears.
2. Any failed in-progress operations can be retried.
3. No automatic retry — the user must explicitly click Retry or re-send.

**Slow/intermittent connections:**
1. AI calls may take longer. Streaming continues when data arrives.
2. If a streaming connection drops mid-response, the partial response is preserved. An error message appears: "Connection lost during generation. [N] tokens received." with [Retry].
3. No automatic timeout for AI calls (models can take 30+ seconds for long responses). The user can always click Stop.

**Applies to:** All AI API calls across all three tiers. Local operations (file I/O, search, wiki browsing) are unaffected.

---

### Permission Errors

**MySecondBrain is a single-user app.** There are no user roles, login screens, or role-based access control. Permission errors are limited to:

1. **File system permissions:** Wiki directory not readable/writable. Settings folder not accessible. Behavior: specific error message with path. "Cannot access [path]. Check folder permissions."
2. **API key authorization failures:** 401/403 from provider APIs. Behavior: error message on the AI response. "API key invalid or expired. Check Settings → Providers." [Retry].
3. **Windows global keyboard hook:** May require administrator privileges on first run to register system-wide hotkeys. Behavior: On first hotkey registration, Windows may prompt for elevation. If denied, Tier 1 hotkeys don't function. A one-time warning: "Global hotkeys require administrator permission. Without them, Alt+Q/W/E/R and Alt+Space will only work when MySecondBrain is the active window."
4. **Windows DPAPI encryption:** API keys are encrypted via DPAPI tied to the local Windows user account. If the user profile is corrupted or DPAPI fails, keys cannot be decrypted. Behavior: Settings → Security shows "⚠️ Encryption error. API keys cannot be accessed." User must re-enter keys.

**Applies to:** All tiers. The app never blocks the user from viewing their own data — permission errors only affect external operations (API calls, file access).

---

### Data Limits

**Hard limits enforced by the app:**

| Resource | Limit | Behavior When Exceeded |
|----------|-------|----------------------|
| Chat message text input | 128,000 characters | Textbox shows character counter. At limit, input is blocked. Tooltip: "Maximum message length reached." |
| System prompt length | 32,000 characters | Settings validation: "System prompt must be under 32,000 characters." |
| Chat title length | 200 characters | Truncated in display. Full title preserved in data. |
| Persona name length | 100 characters | Truncated in display. Full name preserved in data. |
| File attachment size (per file) | 100MB | Warning: "File '[name]' is too large ([size]). Maximum file size is 100MB." |
| File attachment count (per message) | 20 files | Warning: "Maximum 20 attachments per message." |
| Wiki snapshot count (per file) | 30 snapshots | Oldest snapshot auto-deleted when exceeded (N6). |
| Wiki snapshot total storage | 50MB | Oldest snapshots across all files deleted when exceeded (N6). |
| Chat import file size | 100MB | Warning: "The selected file is too large ([N]MB). Maximum import size is 100MB." |
| Chat import messages (per chat) | 10,000 messages | Truncated: "⚠️ Chat '[title]' has [N] messages — only the first 10,000 were imported." |
| Model Comparison Personas | 4 maximum | Additional checkboxes grayed out. "Maximum 4 Personas for comparison." |
| Model Comparison Personas (minimum) | 2 minimum | Start button disabled. "Select at least 2 Personas to compare." |

**Soft limits (warnings only):**

| Resource | Threshold | Warning |
|----------|-----------|---------|
| Chat context window | 90% of model's max (Hard Stop strategy) | Send button grays out. Tooltip: "Context window nearly full (X/Y tokens). Reduce message size or clear conversation." |
| Chat context window | 80% of model's max (Auto-Summarize strategy) | AI silently summarizes oldest 50%. Summary block visible: "[Earlier conversation summarized]" |
| Monthly budget (S5) | 80% of set limit | Warning toast: "⚠️ You've reached 80% of your monthly budget ($[X] of $[Y])." |
| Monthly budget (S5) | 100% of set limit | If "Block API on Limit" enabled: API calls blocked. Message: "Monthly budget exceeded. Increase limit in Settings → Pricing or wait until next month." |
| Database size | No hard limit | Compaction available via Settings → Maintenance (A9). |
| Wiki directory files | No hard limit | Indexing handles large directories. Performance degrades with 10,000+ files. |

---

## Feature-Specific Edge Cases

For every feature group in [`feature-inventory.md`](feature-inventory.md), edge cases not covered by the global patterns above.

---

### A. Settings & Configuration

- **Onboarding Wizard skipped entirely:** User lands in Studio with General Assistant persona, no API keys, no wiki. All AI calls fail. Wiki features show empty states. The app is functional for browsing but not for AI interaction until keys are added.
- **All API keys deleted:** Existing chats referencing deleted keys will fail on next message. Error: "API key for [provider] was deleted. Assign a new key in Settings → Providers."
- **Default Persona deleted:** If the default Persona (A2) is deleted, the first available Persona becomes default. If no Personas exist, new chats use a built-in "General Assistant" fallback.
- **Wiki directory deleted externally:** Wiki Browser shows: "Wiki directory not found. The folder may have been moved or deleted. Select a new folder in Settings → Wiki."
- **Wiki directory on removable drive ejected:** Wiki Browser shows: "Wiki directory is not accessible. Check your connection to the drive."
- **Appearance settings produce unreadable text (e.g., font too small/large):** Font size range enforced (10-24px). Live preview shows sample rendering.
- **VACUUM fails (low disk space):** Error: "Database compaction failed. Ensure sufficient free disk space. [N]MB required." Database remains functional.
- **STT provider configured but microphone not available:** Microphone button grayed out. Tooltip: "No microphone detected."
- **Multiple rapid setting changes:** All take effect immediately. No partial state. If two changes conflict (e.g., rapid theme toggle), last change wins.

**Affected screens:** [`screens/settings.html`](screens/settings.html), [`screens/onboarding-wizard.html`](screens/onboarding-wizard.html), [`screens/studio-chat.html`](screens/studio-chat.html)

---

### B. Model Configurations & Personas

- **API key expires or is revoked by provider:** AI calls fail with 401/403. Error message on the response. User must update the key in Settings → Providers.
- **Model deprecated by provider:** The model identifier is no longer valid. AI calls fail. The Model Configuration shows a warning in Settings: "⚠️ Model '[identifier]' may no longer be available." User must update the model identifier.
- **Persona references a deleted Model Configuration:** The Persona shows "⚠️ Missing Model Configuration" in Settings. When selected for a chat, the user is prompted: "The Model Configuration for '[Persona]' was deleted. Select a new one."
- **All Personas deleted:** The sidebar falls back to a built-in "General Assistant" Persona. New chats use this fallback.
- **API key used by multiple Model Configurations is deleted:** All referencing Model Configurations show "⚠️ Missing API Key." AI calls using those configs fail.
- **Context overflow strategy "Auto-Summarize" triggers mid-conversation:** The summarization API call costs tokens (separate from the user's message). The summary block appears with "[Earlier conversation summarized]" label. User can expand to see what was summarized. If the summarization API call itself fails, the app falls back to Sliding Window silently.
- **Thinking toggle enabled for model that doesn't support it:** Toggle is grayed out in toolbar. Tooltip: "This model does not support extended thinking."
- **Local open-source model endpoint unreachable:** AI call fails with connection error. Error: "Could not connect to local model at [endpoint]. Ensure the server is running."
- **Pricing data missing (cost/1K tokens not configured):** Cost displays as "$—" or "Cost unknown." No cost tracking for that Model Configuration. Usage Dashboard shows "Unknown" for that model's cost.

**Affected screens:** [`screens/settings.html`](screens/settings.html), [`screens/studio-chat.html`](screens/studio-chat.html)

---

### C. Studio Chat Workspace

- **Multiple tabs with active generation simultaneously:** Each tab generates independently. Cross-tab completion alerts (C35) show green dots on inactive tabs. Close confirmation if user attempts to close app/Studio while any tab is generating (C20).
- **User sends message while previous response is still streaming:** The current stream is stopped. The new message is sent. The partial response is preserved as a system note.
- **Message text contains extremely long code blocks (10,000+ lines):** Rendered with syntax highlighting. Horizontal scroll for long lines. Performance may degrade — the chat view is not optimized for rendering massive single messages.
- **All messages cleared (C16):** Chat is empty. Persona info card shown. Undo via Ctrl+Z or toast: "Conversation cleared. Undo?"
- **Draft auto-save recovers after crash (C36):** On re-opening the chat, a dialog: "An unsent draft was recovered. Restore it?" Options: Restore / Discard.
- **Scroll-to-bottom during rapid streaming:** Smooth scroll. If user is scrolled up, auto-scroll pauses (C17). "Scroll to bottom" floating button appears.
- **Spell check language mismatch (e.g., typing Hebrew in English mode):** Spell check is language-aware per Q2. Hebrew text is not spell-checked with English dictionary. Mixed-language messages: each word checked against appropriate dictionary.
- **Incognito chat (C30) is elevated by accident (user sends a reply):** The chat flips to permanent. The 🕶️ indicator disappears. The user can re-toggle "Make Temporary" from the three-dot menu.
- **Locked chat (C31) password is lost:** Permanent lockout. The encryption is AES-256-GCM. No recovery mechanism. The chat is inaccessible forever. The app warns: "⚠️ If you lose this password, this chat will be permanently inaccessible. MySecondBrain cannot recover it."
- **User resizes panels to extreme minimums:** Minimum widths enforced: sidebar 150px, right panel 200px. Panels snap to minimum.
- **Pinned window (C23) on multi-monitor setup:** Per-monitor DPI awareness (P8). Pin state remembered per session. On session restore (A6), pin state is restored.
- **Message selection mode (C18) with hundreds of messages:** Checkboxes appear on hover. Bulk actions operate on selected subset. Performance impact minimal.
- **User pastes image (C9a) that exceeds file size limit:** Warning: "Image too large ([size]). Maximum file size is 100MB."
- **User attaches a file type not supported by the active model (C9c):** Yellow warning badge on attachment: "⚠️ [Model] does not support [file type]. Attached as metadata only." The file name, type, and size are included in the prompt. The AI cannot process the content.

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html)

---

### D. Message Manipulation & Branching

- **Edit a message that has subsequent branches:** If editing "Edit in Place," all branches from that message are preserved but may no longer make sense in context. If "Edit as Branch," a new branch is created. Existing branches are unaffected.
- **Cycle through branches (D3) rapidly:** Each branch renders independently. No flickering. Branch indicator updates.
- **Chat tree visualization (D4) with 50+ branches:** The tree renders all nodes. Active branch highlighted. Scrollable/zoomable if complex. Performance may degrade with 100+ nodes.
- **Delete a message that is a branch point:** All child branches are preserved but orphaned (no parent). They remain accessible via the chat tree as disconnected nodes.
- **Undo (D9) stack across multiple edits:** Per-chat undo stack. Persists until chat tab is closed. Stack depth: 50 operations. Oldest operations dropped.
- **Quote from chat (D5) with very long selected text:** Selected text is trimmed to 2,000 characters in the quote block. "[...]" indicates truncation.

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html)

---

### E. Chat Modes & Controls

- **Switch from Standard to Text Completion mode mid-conversation:** Existing messages preserved. Subsequent messages sent as raw text prompts with no conversation history. Model Configuration must support text completion mode.
- **Text Completion mode with a model that doesn't support it:** The mode toggle is disabled. Tooltip: "Text Completion mode is not supported by this model."
- **Thinking toggle left ON across many messages:** Each message incurs additional thinking tokens (cost). No automatic disable. User manages via toggle.
- **Mute notifications (E4) on all chats:** System-level mute. No sound plays for any completion. Overrideable per chat.

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html)

---

### F. Artifacts & Side Panel

- **Artifact with 100+ versions:** Version dropdown becomes scrollable. Version history is preserved. Diff between any two versions works. Storage: each version is the full artifact content (not incremental diffs). Large artifacts with many versions may consume significant storage.
- **Artifact viewer for unsupported language:** Falls back to plain text rendering with no syntax highlighting.
- **Save artifact to Wiki (F6), but wiki not configured:** Error: "No wiki directory configured. Set up in Settings → Wiki." with link.
- **Global Artifacts Browser with artifacts from deleted chats:** Artifacts exclusively linked to deleted chats are garbage-collected (O5). Artifacts from existing chats appear.
- **Artifact name collision:** Version numbers differentiate (e.g., "script.py" v1, v2...). No renaming collision within the same chat.

**Affected screens:** [`screens/global-artifacts-browser.html`](screens/global-artifacts-browser.html), [`screens/studio-chat.html`](screens/studio-chat.html)

---

### G. Media Library

- **Media file from a deleted chat:** Per O5, exclusively linked media is garbage-collected. Media saved to disk or shared across chats is preserved.
- **Media file format not supported for inline preview:** Shows generic file icon with filename and "Open in System App" button.
- **Media Library with thousands of files:** Grid is virtualized (only renders visible thumbnails). Filtering and search remain responsive.
- **Generated image (G4) fails due to model not supporting image generation:** Error: "This model does not support image generation. Use a model with image generation capability."

**Affected screens:** [`screens/media-library.html`](screens/media-library.html), [`screens/studio-chat.html`](screens/studio-chat.html)

---

### H. Tool Use (Agent Capabilities)

- **Terminal command execution (H2) with dangerous command (e.g., `rm -rf`):** ALWAYS shown in confirmation dialog with risk assessment. User must explicitly approve. Cannot be auto-approved (H5 override). User approves at their own risk.
- **File generation (H3) targeting a path that already exists:** Confirmation: "A file already exists at [path]. Overwrite?" Options: Overwrite / Choose Different Name / Cancel.
- **File editing (H4) on a file that changed externally since last read:** Warning: "This file has been modified since the AI last read it. The AI's suggested edit may be based on outdated content." User can proceed or cancel.
- **Deep Research (H6) exceeds 30 minutes:** Pauses with: "Research paused after 30 minutes (limit reached). [N] of [M] sources processed. Increase limit in Settings → Tools or continue."
- **Deep Research (H6) with no web search tool available (tool disabled):** AI responds: "Deep Research requires web search to be enabled. Enable Browser Search in Settings → Tools."
- **Wiki Search Tool (H7) when wiki not configured:** AI responds: "Wiki search is not available — no wiki directory configured. Set one up in Settings → Wiki."
- **All tools disabled globally:** The Tools toggle in the textbox toolbar is grayed out. Tooltip: "All tools are disabled in Settings → Tools."

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html), [`screens/settings.html`](screens/settings.html)

---

### I. Import & Export

- **Export very long chat (10,000+ messages):** Progress bar shown. Export may take several seconds. PDF export may be slower than Markdown/JSON.
- **Export chat with images/media:** Markdown and JSON exports include media file references (filenames). Actual media files are not included in the export — only references. The user must manually copy media files.
- **Import ChatGPT export with custom GPTs/system messages:** Custom GPT instructions are imported as system messages. The chat is assigned a placeholder Persona. User can create a matching Persona later.
- **Import file with mixed formats (some valid, some corrupted chats):** Valid chats are imported. Corrupted chats are skipped with a summary: "⚠️ [N] chats could not be imported due to parsing errors."
- **Import during onboarding (no Studio open yet):** Imported chats appear after Studio launches. No conflicts.

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html), [`screens/settings.html`](screens/settings.html), [`screens/onboarding-wizard.html`](screens/onboarding-wizard.html)

**Full flow:** See [`flows/import-chatgpt.md`](flows/import-chatgpt.md).

---

### J. Prompt Library

- **Prompt with variable {{selected_text}} used when no text selected:** Variable resolves to empty string. The prompt still works but may be incomplete.
- **Prompt with variable {{current_wiki_file}} when wiki not configured:** Variable resolves to "[No wiki configured]".
- **Prompt Library with 500+ prompts:** Organized with folders/tags. Search/filter available. Performance impact minimal.
- **Delete a prompt that is used as a hotkey assignment:** The hotkey assignment is cleared. Settings → Hotkeys shows "⚠️ Prompt deleted — reassign hotkey."

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html)

---

### K. Text Actions & Three-Tier Interaction

- **Tier 1 hotkey pressed while Command Bar (Tier 2) is open:** Tier 1 hotkey is ignored. Command Bar takes priority.
- **Tier 1 hotkey pressed while another Tier 1 is processing:** Second hotkey is ignored. Only one Tier 1 action at a time.
- **Tier 1 "Open in Studio" clicked, then user closes Studio before Accept:** Studio chat is already permanent. Closing Studio does not undo elevation. Source text can still be applied via Accept (independent actions).
- **Tier 1 Apply to source app that was closed:** Clipboard fallback. Toast: "Text copied to clipboard — [action]. Source application was closed."
- **Tier 2 Command Bar open when Windows locks (Win+L):** Hidden behind lock screen. On unlock, Command Bar is still open with conversation intact.
- **Tier 2 Command Bar resized to <350×250px (minimum):** Window snaps to minimum dimensions.
- **Tier 2 Command Bar dismissed with Escape when input has text:** First Escape clears input. Second Escape dismisses.
- **Text Action deleted but still assigned to a hotkey:** Hotkey assignment is cleared. Settings → Hotkeys shows "⚠️ Text Action deleted — reassign hotkey."
- **Custom Text Action with system prompt that exceeds model's context when combined with highlighted text:** The highlighted text is truncated. Popup shows: "⚠️ Text was truncated — the original was [N] characters. Open in Studio for full context."

**Affected screens:** Tier 1/2 are overlays (no dedicated screen). Settings: [`screens/settings.html`](screens/settings.html).

**Full flows:** See [`flows/tier1-hotkey-rewrite.md`](flows/tier1-hotkey-rewrite.md), [`flows/tier2-command-bar-query.md`](flows/tier2-command-bar-query.md), [`flows/elevate-transient-to-permanent.md`](flows/elevate-transient-to-permanent.md).

---

### L. Chat Organization & Search

- **Full-text search (L3) with query matching 10,000+ messages:** Results paginated or limited to top 200. Performance depends on SQLite FTS implementation.
- **Search across both permanent and transient chats:** Transient chats in the 7-day window are included in search results. Older transient chats are already cleaned.
- **Delete chat that is currently open in a tab:** Tab closes. If the chat is soft-deleted, it moves to Trash. The tab shows: "This chat has been deleted. [Restore] [Close Tab]"
- **Bulk operations (L11) on 100+ chats:** Progress indicator. Operations are transactional — all succeed or all fail.
- **Chat folder (L9) deleted:** Chats in the folder return to the root chat list. Chats are not deleted.
- **Pin (L8) chat, then archive (L10) chat:** Archived takes precedence — chat is hidden. Unarchiving restores pinned position.
- **Color label (L14) on chat that's later deleted:** Label data is deleted with the chat.
- **Timeline tab (L5) with thousands of transient entries:** Scrollable, newest first. Old entries scroll off. Auto-cleanup removes entries older than 7 days.

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html)

---

### M. Model Comparison

- **All 4 panels streaming simultaneously over slow connection:** Each stream is independent. Some may finish while others are still streaming. No panel blocks another.
- **One panel's API key invalid, other 3 valid:** Only the invalid panel shows error. Others continue normally. User can still Accept from valid panels.
- **Accept a panel whose conversation is 50+ messages:** Entire conversation appended to main chat. Branch data created for other panels. Performance: no degradation from message count.
- **Close comparison during active generation:** Confirmation: "Responses are still being generated. Stop and discard?"
- **Broadcast mode (send to all) with one panel in error state:** Message sent to all panels. Error-state panel attempts Retry automatically.
- **User types in per-panel input while broadcast mode is ON:** Per-panel inputs are hidden during broadcast. User must toggle broadcast OFF to use per-panel inputs.

**Affected screens:** [`screens/model-comparison.html`](screens/model-comparison.html), [`screens/studio-chat.html`](screens/studio-chat.html)

**Full flow:** See [`flows/model-comparison-flow.md`](flows/model-comparison-flow.md).

---

### N. Personal Wiki / Second Brain

- **Existing wiki folder selected during onboarding has no index.md:** The app immediately runs wiki indexing (N2) and auto-generates `index.md` (N11) at the wiki root. Display shows ".md files found: [N]" + "index.md auto-generated." If the folder contains zero `.md` files, a starter `index.md` is created with a welcome message.
- **Wiki directory contains 10,000+ .md files:** Indexing takes longer on startup. Index stored in SQLite for fast search. Related Sections and Backlinks may be slower to compute.
- **Wiki .md file with broken internal links:** Wiki Browser renders the link as plain text (not clickable). Broken link detection: TBD (could be a future feature).
- **Wiki .md file with circular cross-links (A → B → C → A):** Rendered as clickable links. No infinite loop — user navigates manually. No performance issue.
- **Write to Wiki (N5) triggered from a chat with no conversation (empty chat):** AI generates a generic template. Preview shows minimal content. User can cancel.
- **Write to Wiki (N5) update on a file that changed externally during the AI generation:** On save, the Diff Viewer shows changes against the CURRENT file content (including external edits). The AI-generated update is applied on top of the latest version.
- **@ Mention (N7) a wiki file that is deleted mid-conversation:** The injected content remains in the chat history (it was injected when @ was typed). Future @ mentions won't find the file.
- **AI Memory (_memory.md) grows very large (>10,000 tokens):** Token cap setting (N12) limits injection. AI is instructed to condense during updates. If still over limit: "[Memory truncated to [N] tokens]" notice.
- **Find & Replace (N13) with regex that matches thousands of occurrences:** Preview shows all matches. "Replace All" may take time for many files. Undo via N6 snapshots.
- **index.md (N11) becomes very large (100KB+):** Generated from local index — pure computation, no AI. AI reads it for cross-linking (N10). Large index.md increases token cost of cross-linking step. Architect should consider summarizing index.md for AI consumption.
- **Git version control (onboarding Step 3) auto-commit fails (e.g., network down for push):** Local commit succeeds. Push fails with error logged. Next auto-commit will attempt push again.
- **GitHub token expires or is revoked:** Push fails. Settings → Wiki shows "⚠️ GitHub connection failed. Token may have expired." User must update token.

**Affected screens:** [`screens/wiki-browser.html`](screens/wiki-browser.html), [`screens/studio-chat.html`](screens/studio-chat.html), [`screens/settings.html`](screens/settings.html), [`screens/onboarding-wizard.html`](screens/onboarding-wizard.html)

**Full flow:** See [`flows/write-to-wiki.md`](flows/write-to-wiki.md).

---

### O. Data Model & Lifecycle

- **7-day auto-cleanup (O4) runs while user is viewing a transient chat:** Chat currently open in a tab is skipped for this cleanup cycle. Will be cleaned on next cycle if still transient and not elevated.
- **Auto-cleanup blocked by database lock:** Retry on next cycle. Error logged.
- **Garbage collection (O5) on chat with media shared across multiple chats:** Media preserved. Only exclusively linked media is deleted.
- **Database grows to many GB:** Compaction via VACUUM (O6) reclaims space. No auto-compaction — user must trigger manually or it could be added as a scheduled task (future).
- **Chat elevation (O3) triggered by sending reply, but reply fails (API error):** Elevation still occurs (flag flipped on send action, not API response). Chat moves to Chats list.
- **Transient chat with artifacts (F1) — auto-elevation exception applies:** Chat is auto-elevated (IsTransient flips to false) during cleanup check if it contains artifacts. User's work is preserved.

**Affected screens:** All screens (background process). Settings: [`screens/settings.html`](screens/settings.html).

**Full flow:** See [`flows/elevate-transient-to-permanent.md`](flows/elevate-transient-to-permanent.md).

---

### P. Windows OS Integration

- **Global keyboard hook triggers antivirus false positive:** Some security software may flag global keyboard hooks as keyloggers. Documented concern — see Flagged Concerns.
- **HWND injection (P3) fails on UWP/modern Windows apps:** Fallback to clipboard paste. Toast indicates clipboard fallback.
- **Clipboard format preservation (P4) on app that uses custom clipboard formats:** App preserves standard formats (text, HTML, RTF). Custom formats are lost.
- **Local WebSocket server (P5) port conflict:** Configurable port in Settings. Default: some high port (e.g., 19876). If port is in use, error on startup: "WebSocket server could not start — port [N] is in use. Change port in Settings → Tools."
- **System tray (P6) unavailable (rare Windows configuration):** App minimizes to taskbar instead. No tray icon shown.
- **Session restore (P7) with 50+ tabs open:** All tabs restored. May take a moment on launch. Active tab is the last active tab from the previous session.
- **Per-monitor DPI awareness (P8) when moving window between monitors with different scaling:** Instant resizing. No blur. Crisp rendering at both DPIs.

**Affected screens:** System-wide (all screens). Settings: [`screens/settings.html`](screens/settings.html).

---

### Q. Language & RTL Support

- **Hebrew text auto-detected but user wants English RTL rendering:** Toggle in Settings → Language: "Auto-detect RTL" can be turned OFF. All text renders LTR by default.
- **Mixed LTR/RTL in a single message (e.g., English + Hebrew):** Each segment renders in correct direction. Bidirectional text algorithm handles the mixing.
- **Code blocks with Hebrew variable names:** Code blocks are always LTR (monospace). Hebrew characters render right-to-left within the LTR block — may look odd. This is standard behavior for mixed-direction code.
- **UI language set to Hebrew but system prompts are in English:** UI chrome (buttons, labels) in Hebrew. AI responses in whatever language the model outputs (typically matches the prompt language).

**Affected screens:** All screens.

---

### R. Backup & Recovery

- **Backup (R3) triggered while a chat has active generation:** Backup proceeds. Active generation continues independently. Backup captures current database state — the in-progress message may or may not be included depending on timing.
- **Backup to Google Cloud Storage fails (network error):** Error logged. "Backup failed: Network error. Next scheduled backup will retry." Manual backup available.
- **Restore (R4) from backup that contains chats since deleted:** Restored chats reappear. This is intentional — restore rolls back to the backup state.
- **Backup file corrupted:** Restore fails with error: "Backup file is corrupted or incomplete. Select a different backup."
- **Restore while chats are open in tabs:** Confirmation: "Restoring a backup will close all open chats. Continue?" On confirm, all tabs close, restore proceeds, Studio reloads with restored data.

**Affected screens:** [`screens/settings.html`](screens/settings.html)

---

### S. Usage & Pricing Dashboard

- **Usage charts (S3) with zero data (new user):** Chart areas show "No data for the selected time range." Empty chart with axes.
- **Budget alerts (S5) when budget limit is set to $0:** Invalid — minimum $1 if budget alerts are enabled.
- **AI Feedback Summary (S6) when only thumbs-down given:** Approval % = 0%. Rankings still show.
- **Per-chat breakdown (S4) with hundreds of chats:** Table is paginated or virtualized. Sortable columns.

**Affected screens:** [`screens/usage-dashboard.html`](screens/usage-dashboard.html)

---

### U. Soft-Delete Trash

- **Restore chat (U4) that references deleted Persona or Model Config:** Chat restored. Persona field shows "⚠️ Unknown Persona." User must assign a valid Persona to continue the conversation.
- **30-day auto-purge (U3) runs while user is viewing Trash:** Chat currently being viewed is skipped for this cycle. Purged on next cycle.
- **Empty Trash (U6) with 1,000+ items:** Bulk delete operation. Progress bar if operation takes >1 second. All items permanently deleted.
- **Permanent delete (U5) then immediately regret:** No undo. The chat and all exclusively linked data are gone per O5 garbage collection.

**Affected screens:** [`screens/studio-chat.html`](screens/studio-chat.html) (sidebar Trash view)

---

### T. Nice-to-Have Features (Future)

Edge cases for deferred features (T1-T6) will be documented when these features are designed. These features are out of scope for the initial release. See [`features/nice-to-have-future.md`](features/nice-to-have-future.md).
