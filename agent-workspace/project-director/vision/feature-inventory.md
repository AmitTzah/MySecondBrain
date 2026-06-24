# Feature Inventory

## Core Features

Features without which the app has no reason to exist. These MUST be present in the initial release. Detailed behavioral specifications are in `features/[feature-group].md`.

### A. Settings & Configuration
**Spec:** [`features/settings-configuration.md`](features/settings-configuration.md)

- **A1. Global Settings Screen:** A dedicated settings screen with all global configuration options organized into 18 categories (Providers, Profiles, Appearance, Wiki, Backup, Text Actions, Hotkeys, Tools, Skills, Memory, Language, Notifications, Startup, Updates, Diagnostics, Pricing, Security, Maintenance).
- **A2. Default Profile Selection:** User selects which model profile is auto-assigned when creating a new chat.
- **A3. Appearance Settings:** At least three visual themes for chat view (Classic, Compact, Bubble). Customizable font family, size, and weight for messages.
- **A4. Notification Settings:** Toggle sound on assistant completion. Option to disable streaming entirely. Per-chat mute toggle.
- **A5. Dark Mode / Light Mode:** Toggle for entire application UI. Independent of chat themes (A3). Dark mode is default.
- **A6. Startup Behavior:** Option to launch on Windows startup. Option to restore last session (reopen all chats/tabs).
- **A7. Auto-Update:** Check on startup, periodically, or manual. Notify and install when available.
- **A8. Onboarding Wizard:** First-launch guided setup: add API key, create profile, select wiki directory, configure hotkeys. Each step skippable. Re-launchable from Settings.
- **A9. Database Maintenance:** "Compact Database" button runs SQLite VACUUM. Displays size before/after.
- **A10. Speech-to-Text (STT) Provider:** Configure dedicated STT provider and model for voice dictation (separate from text-generation configs). Supports OpenAI Whisper API, local Whisper, OpenAI-compatible STT endpoints.
- **A11. Diagnostics & Debug Logging:** Structured diagnostic logging via Serilog to rolling JSON files. Log level selector (Information/Debug/Verbose). Eight per-category toggle checkboxes: LLM API Calls, Tier 1 Hotkey Pipeline, Tier 2 Command Bar, Database, Wiki & File System, WebSocket, Startup & Shutdown, System Integration. "Open Logs Folder" and "Clear Logs" buttons. API keys MUST be redacted in all log output. Full spec: [`features/diagnostics-debug-logging.md`](features/diagnostics-debug-logging.md).
- **A12. Skills Defaults:** Global defaults for which skills are enabled in new chats. Built-in skills list with individual toggles. Community skills discovered at `%LOCALAPPDATA%/MySecondBrain/skills/` also listed. "Enable All" / "Disable All" quick actions. Full spec: [`features/agent-skills.md`](features/agent-skills.md).
- **A13. Memory Management:** View all stored memories (AI-extracted facts). Edit or delete individual memory entries. "Clear All Memories" with confirmation. Memory storage size displayed. Full spec: [`features/agent-skills.md`](features/agent-skills.md) §Memory Tool.

### B. Model Configurations & Personas
**Spec:** [`features/model-configurations-personas.md`](features/model-configurations-personas.md)

Two-layer model: **Model Configurations** (engine — what model runs) and **Personas** (behavior — how the model behaves).

- **B1. API Key Management:** Add, view, edit, delete API keys. Encrypted at rest via Windows DPAPI. "Test Key" validates against provider API. Built-in types: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAI-Compatible.
- **B2. Model Configurations (The Engine):** Named configs defining: display name, provider, API key, model identifier, temperature, max output tokens, max context window, thinking on/off, pricing (cost/1K tokens), Context Overflow Strategy (B8).
- **B3. Personas (The Behavior):** Named personas defining: display name, system prompt, default Model Configuration, default chat mode (Standard or Text Completion).
- **B4. Persona Selection per Chat:** New chat (Ctrl+N) picks Persona from list. Pre-configures system prompt, model, and mode. Switchable anytime from textbox toolbar. Recently used at top.
- **B5. Local Open-Source Model Support:** Connect via OpenAI-Compatible provider pointing to local endpoint (e.g., localhost:1234).
- **B6. "OpenAI-Compatible" Provider Type:** Generic provider with display name, endpoint URL, optional API key. Enables any OpenAI-API-compatible service.
- **B7. Auto-Fetch Available Models:** Fetches model list from provider API when key added. Cached, refreshable. Manual entry also supported.
- **B8. Context Window Overflow Strategy:** Per-config: Sliding Window (drop oldest), Hard Stop (block + warn), or Auto-Summarize (AI summarizes oldest 50% into summary block). Changeable mid-chat.

### C. Studio Chat Workspace
**Spec:** [`features/studio-chat-workspace.md`](features/studio-chat-workspace.md)

- **C1. Conversation View:** Scrolling message history. Messages visually distinguished per theme. Each shows model/profile and timestamp (hover for full, always-visible relative time).
- **C2. Message Content Rendering:** Markdown (headings, bold, italic, code blocks with syntax highlighting, lists, links, tables, blockquotes), images (inline, clickable), audio (mini player), video (embedded player).
- **C3. Code Block Rendering:** Syntax highlighting by declared language. Copy button on hover at top-right.
- **C4. Streaming Response Display:** Token-by-token rendering. Markdown/code render progressively. Generation time shown on completion.
- **C5. Message Actions:** Send (Enter/button). During generation, Send transforms into spinner + red "Stop." Clicking Stop preserves partial response.
- **C5a. Conditional [Apply] Button (Tier 1 Elevation):** When chat originated from Tier 1 text transformation: source indicator banner + [Apply Latest] in header. Per-message [Apply] on direct transformations. Grayed out if source app closed.
- **C6. Copy:** Two explicit per-message buttons: "Copy MD" (raw Markdown) and "Copy Rich" (HTML/RTF). Copy entire conversation via menu option.
- **C7. Chat Titling:** Auto-generated from first user message via AI. Manually editable.
- **C8. Continue Generation:** "Continue" button when last message is from assistant. Sends continuation request.
- **C9. Drag & Drop Files/Media:** Drag files/images/video/audio into input. Images for vision models. Text files read into prompt. Other files: metadata included.
- **C9a. Paste Image from Clipboard (Ctrl+V):** Paste image into textbox → appears as thumbnail below input. Click thumbnail opens image in new tab. Send with next message for vision models.
- **C10. Multiple Chat Tabs:** Open multiple chats in tabs. Reorder by drag-drop. Close, reopen from sidebar.
- **C11. Token Usage & Context Display:** Per-message token count + cost. Chat header shows context size vs max window and cumulative cost. Local tokenizer for real-time feedback.
- **C12. Keyboard Shortcuts (Studio):** Ctrl+N (new), Ctrl+W (close tab), Ctrl+Shift+T (reopen), Ctrl+Tab/Shift+Tab (next/prev tab), Ctrl+F (search in chat), Ctrl+Shift+F (global search), Ctrl+S (export). Ctrl+/ for shortcut reference.
- **C13. Resizable Panels:** Sidebar, artifact panel (WebView2), chat nav bar all resizable. Min/max enforced. Sizes remembered.
- **C14. Error Handling & Retry:** Specific error message + Retry button on API failure. Escalating message on consecutive failures.
- **C15. Scroll-to-Bottom Button:** Floating button when scrolled up during streaming. Smooth scroll to latest.
- **C16. Clear Conversation:** Clear all messages, preserve chat. Accessible from chat header three-dot (⋯) menu. Confirmation dialog. Undo via toast or Ctrl+Z.
- **C16a. Chat Header Three-Dot Menu (⋯):** Clear Conversation, Export Chat (I1), Duplicate Chat (D7), Chat Tree (D4), Edit System Message (E5).
- **C17. Auto-Scroll Behavior:** Auto-pauses when user scrolls up during generation. Handles media height changes smoothly.
- **C18. Message Selection Mode:** Checkboxes appear on message hover. Bulk actions bar: Copy Selected, Delete Selected, Quote Selected.
- **C19. Offline/Network Status Indicator:** Green/Yellow/Red dot in status bar. Offline banner below header.
- **C20. Close Confirmation with Active Generation:** Confirmation dialog when closing tab/window during generation.
- **C21. Audio Input (Microphone):** Voice dictation via configured STT Provider. Editable transcribed text.
- **C22. Camera Capture:** Webcam photo capture, immediately attached to message for vision models.
- **C23. Pin Window / Always on Top:** Toggle in header. Remembered across sessions.
- **C24. Dark/Light Mode Quick Toggle:** Sun/Moon icon in chat header. Instant toggle between dark and light mode (A5).
- **C25. Font Size Quick Adjust:** A⁻/A⁺ buttons in chat header. Adjust chat message font size 10–24px. Current size displayed.
- **C26. Model Comparison Button:** "Compare" button (⚖) in textbox toolbar. Opens side-by-side Persona comparison (M).
- **C27. Dynamic System Message Editing Access:** Click Persona name in header → popover with editable system message. Also in three-dot menu and Chat Nav bar.
- **C28. Duplicate / Fork Chat Access:** Right-click message → "Fork from here". Also in three-dot menu → "Duplicate Chat".
- **C29. Chat Header Layout:** Complete header bar layout: Persona name | context bar | cost | [Source banner] | font size | dark mode | pin | ⋯ menu.
- **C30. Incognito / Temporary Studio Chat:** Toggle chat as temporary (IsTransient=true). Auto-cleans after 7 days. 🕶️ indicator on tab.
- **C31. Locked Chats:** Password-protected encryption (AES-256-GCM). Global default + per-chat override. "Hide locked from sidebar" option. Permanent lockout if password lost.
- **C32. Chat Summarization:** "Summarize Chat" in three-dot menu → AI generates summary → save as artifact or export.
- **C33. Message Favoriting:** Star individual messages (★). Filter favorited in Chat Nav. Global search filter for favorited.
- **C34. Spell Check in Textbox:** Red squiggly underline for misspelled words. Right-click suggestions. Toggle in Settings.
- **C35. Cross-Tab Completion Alert:** Pulsing green dot on inactive tab when generation completes. Sound + brief "✓" in tab title. Configurable in Settings.
- **C36. Auto-Save Message Drafts:** Textbox content auto-saves every 5 seconds. Recover unsent drafts after crash or accidental tab close. "Restore draft?" dialog.
- **C37. Right Panel Layout:** Two vertically stacked resizable sections: Artifacts (top, WebView2-powered) + Chat Nav (bottom). Divider for resizing. Both collapsible. No tabs — both visible simultaneously.

### D. Message Manipulation & Branching
**Spec:** [`features/message-manipulation-branching.md`](features/message-manipulation-branching.md)

- **D1. Edit Any Past Message:** Edit user or assistant messages. "Edit in Place" (overwrite) or "Edit as Branch" (new version). AI sees updated history.
- **D2. Delete Any Past Message:** Removes from current history. Branch data preserved.
- **D3. Branch Navigation:** Branch indicator (e.g., "2/3"). Cycle through branches with arrows. Subsequent messages re-render.
- **D4. Chat Tree Visualization:** Visual tree/graph of all branches. Active branch highlighted. Click to navigate.
- **D5. Quote from Chat:** Select text, click Quote to insert as quoted block in input.
- **D6. Chat Navigation Bar:** Collapsible panel with scrollable message list. Click to jump to message.
- **D7. Duplicate / Fork Chat:** Duplicate from any message point. New ChatThread with messages up to that point.
- **D8. Message Feedback:** Thumbs-up/down on assistant messages. Stored with message.
- **D9. Undo/Redo Message Edits:** Ctrl+Z/Ctrl+Y. Per-chat stack, persists until chat closed.

### E. Chat Modes & Controls
**Spec:** [`features/chat-modes-controls.md`](features/chat-modes-controls.md)

- **E1. Standard Chat Mode:** Default. User/assistant conversational structure.
- **E2. Text Completion Mode:** Raw text prompt → raw completion. No conversation history. For text completion APIs.
- **E3. Thinking Toggle:** Enable/disable AI extended reasoning. Shows thinking process when enabled.
- **E4. Mute Notifications Toggle:** Per-chat mute for sound notifications.
- **E5. Dynamic System Message Editing:** View/edit system message anytime from chat header or nav bar. Takes effect for subsequent messages.

### F. Artifacts & Side Panel
**Spec:** [`features/artifacts-side-panel.md`](features/artifacts-side-panel.md)

- **F1. Workspace-to-Artifact Pipeline:** Model creates files in workspace (`%LOCALAPPDATA%/MySecondBrain/workspace/`) using `bash` or `text_editor`. Model calls `present_files` tool with file paths to surface them as artifacts. App copies files to artifacts directory and displays them in the side panel. Same filename within chat = new version (app auto-tracks). Different filename = new artifact.
- **F2. Side Panel (WebView2):** Resizable panel right of chat, powered by embedded Edge WebView2 control. Lists all presented artifacts by name. Click to view content with browser-native syntax highlighting (200+ languages), Markdown rendering, and diff views. Interactive React/Tailwind artifacts from web-artifacts-builder skill render natively.
- **F3. Version History:** Each artifact maintains versions (v1, v2, v3...). Entirely app-side — the app tracks every file write within a chat by filename. Version selector dropdown in the side panel. AI's text_editor `str_replace` commands on the same filename automatically create new versions.
- **F4. Diff View:** Select any two versions → side-by-side or unified diff. Red = removed, Green = added. Diff computation is app-side (C#), rendering is WebView2-native (using browser diff libraries). Navigation: "Previous Change" / "Next Change."
- **F5. Version Switching:** Switch which version is "active" (displayed in viewer). Active version is what new AI changes are based on. Reverting to older version + requesting changes = new branch from that version.
- **F6. Artifact Viewer (WebView2):** Browser-native syntax highlighting for code, rendered Markdown, interactive HTML/React for web-artifacts-builder output. "Save to Disk" and "Save to Wiki" buttons. Save to Wiki launches N5 pipeline.
- **F7. Global Artifacts Browser:** Lists all artifacts from all chats. Search, sort, filter. Opens in side panel.

### G. Media Library & Multi-Modal Generation
**Spec:** [`features/media-library.md`](features/media-library.md)

- **G1. Media Library Overview:** Browsable gallery of all media across chats. AI-generated and user-uploaded images, audio, video, webcam captures, screenshots.
- **G2. Media Library Filtering & Search:** Filter by type, source chat, date range. Search by filename. Grid layout.
- **G3. Media Actions:** View/play, download, copy to clipboard, open in system app, delete, navigate to source chat.
- **G4. Image Generation:** AI generates images inline in chat. Auto-saved to Media Library.
- **G5. Audio Generation:** AI generates audio with inline player. Auto-saved to Media Library.
- **G6. Inline Media in Chat:** All media renders inline. "Save to Disk" and "View in Library" buttons.

### H. Tool Use (Agent Capabilities)
**Spec:** [`features/tool-use-agents.md`](features/tool-use-agents.md)

The model uses 9 tools matching Anthropic's trained-in schemas where possible. All tools are additively assembled per chat — disabled tools are completely removed from the API call.

- **H1. bash:** Anthropic `bash_20250124` schema. Executes commands in workspace-isolated `%LOCALAPPDATA%/MySecondBrain/workspace/`. cmd.exe on Windows with Git Bash/WSL fallback for `.sh` scripts. Absolute paths outside workspace blocked pre-execution. Writes outside workspace require explicit user confirmation. Wiki directory is read-only from bash.
- **H2. text_editor:** Anthropic `text_editor_20250728` schema. Commands: `view` (read file), `create` (new file — FAILS if path exists, forcing `str_replace` for updates), `str_replace` (patch file — exact match required), `insert` (append to file). Replaces the original separate `file_generate` and `file_edit` tools.
- **H3. web_search:** Google Custom Search or Bing API. Anthropic server schema reimplemented as client tool with identical interface. Model autonomously searches web for information.
- **H4. web_fetch:** HttpClient GET fetches URL content. Read-only. Used by Deep Research and general web page reading.
- **H5. memory:** Anthropic `memory_20250818` schema wrapping SQLite memory store. Model stores/retrieves discrete facts about the user. Separate from the wiki. Per-chat toggle in toolbar. User can view/edit/delete memories in Settings → Memory. Full spec: [`features/agent-skills.md`](features/agent-skills.md) §Memory Tool.
- **H6. wiki_search:** Queries local SQLite FTS5 wiki index. Returns matching filenames, headings, and content snippets. Read-only. Zero API cost. Model uses to incorporate the user's personal knowledge base into responses.
- **H7. skill_load:** Activates an Agent Skill by loading its full `SKILL.md` instructions into context. Structured XML wrapping for context management. Deduplicated — if already activated in session, re-injection skipped. Full spec: [`features/agent-skills.md`](features/agent-skills.md).
- **H8. ask_user_input:** Structured WPF confirmation dialogs instead of prose-based confirmations. Pattern from claude.ai. Used for dangerous operations (bash writes outside workspace, file deletions).
- **H9. present_files:** Model signals "these workspace files are done — surface them as artifacts." App copies files from workspace to artifacts directory, renders in WebView2 side panel. First path in array shown first. Auto-copies from non-artifacts paths.

**Tool Auto-Approval (H10):** Global defaults + per-chat overrides for which tools auto-execute. `bash` writes outside workspace and `text_editor` deletes ALWAYS require confirmation. Other tools configurable: Auto-Approve / Ask / Disabled.

**Deep Research:** Now a skill rather than a custom state machine. Model follows research protocol using `web_search` + `web_fetch` + `bash` tools. Progress visible naturally as tool calls stream in chat. Full spec: [`features/agent-skills.md`](features/agent-skills.md) §Deep Research.

### I. Import & Export
**Spec:** [`features/import-export.md`](features/import-export.md)

- **I1. Export Chat:** Export as Markdown, PDF, or JSON. Includes current branch messages with metadata.
- **I2. Import Chats:** Import from ChatGPT export (JSON) and Claude export (JSON). Created as new ChatThreads.

### J. Prompt Library
**Spec:** [`features/prompt-library.md`](features/prompt-library.md)

- **J1. Prompt Library:** Saved reusable prompts with dynamic variables: {{clipboard}}, {{selected_text}}, {{date}}, {{current_wiki_file}}. Organized with tags. Select to insert.
- **J2. Prompt Management:** Edit, delete, organize into folders/categories. Accessible from textbox toolbar.

### K. Text Actions & Three-Tier Interaction
**Spec:** [`features/text-actions-three-tier.md`](features/text-actions-three-tier.md)

- **K1. Text Actions (Unified):** Named text transformation actions with three independent dimensions: capture scope (what to grab — selection, focused element, surrounding context, full document, screenshot), transform (system prompt + Model Configuration), apply mode (where to put result — replace, insert, append, prepend, clipboard, show only). Built-in defaults: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt, Continue Writing, Improve Flow, Summarize Page, Explain Screen. Custom actions with any combination supported. Available as hotkeys (Tier 1) and toolbar dropdown (Studio).
- **K2. Textbox Toolbar:** Controls above chat input: Persona selector, thinking toggle, mute toggle, tools dropdown, skills dropdown, memory toggle, prompt library, Text Actions dropdown.
- **K3. Tier 1 — Global Hotkey Text Actions:** Three phases: Capture (graduated UIA pipeline per capture scope flags + HWND + "Thinking..." overlay), Result Popup (editable AI output + Accept/Discard/Open in Studio/Save to Wiki/Retry + Additional Instructions field), Apply (per apply mode: HWND injection, UIA insertion, clipboard-only, or show-only + fallbacks + confirmation toast + Undo).
- **K4. Tier 2 — Command Bar (Alt+Space):** Spotlight-style overlay. Inline state: input field, Q&A display, Pop-out/Close/Copy controls. Popped-out state: floating resizable mini-window with Open in Studio, Pin, Minimize, Close. Elevation to Studio. Dismissal saves as transient thread.
- **K5. Tier 3 — Studio Chat:** Full chat workspace (Section C). Opened via main window, tray icon, or elevation.

### L. Chat Organization & Search
**Spec:** [`features/chat-organization-search.md`](features/chat-organization-search.md)

- **L1. Sidebar Chat List:** All permanent chats sorted by selected order. Pinned at top. Grouped by date. Shows title, preview, timestamp, star, tags.
- **L2. Chat Favoriting:** Star/unstar. Filter toggle for favorites only.
- **L3. Full-Text Search:** Searches all chat messages (permanent + transient in window). Results with snippets, chat name, timestamp, highlights. Click to open and scroll.
- **L4. Delete Chat (Soft-Delete):** Moves chat to Trash (30-day soft-delete). Restore available. Permanent delete from Trash. Trash view in sidebar. See [`features/soft-delete-trash.md`](features/soft-delete-trash.md).
- **L5. Timeline Tab:** Chronological feed of all transient actions (Tier 1 + Tier 2). Shows action type, preview, timestamp, source app.
- **L6. Sidebar Filtering:** Default: permanent chats. Timeline tab: transient actions. Toggle between views.
- **L7. Chat Tags/Labels:** User-defined tags (e.g., "coding", "writing"). Filterable.
- **L8. Pin Chats:** Pin to top of sidebar. Above all other chats.
- **L9. Chat Folders/Collections:** Create folders. One chat per folder. Sidebar grouping by folder.
- **L10. Chat Archiving:** Hide without deleting. Accessible via "Archived" filter. Excluded from auto-cleanup.
- **L11. Bulk Operations:** Select multiple chats for delete, archive, export, tag, or move.
- **L12. Right-Click Context Menu:** Rename, Delete, Archive, Duplicate, Export, Pin, Tags, Move to Folder.
- **L13. Chat Sorting Options:** Most Recent (default), Name, Date Created, Last Activity. Remembered.
- **L14. Chat Color Labels:** Assign colored dot from preset palette. Visual identification.

### M. Model Comparison
**Spec:** [`features/model-comparison.md`](features/model-comparison.md)

- **M1. Model Comparison Mode:** Send same prompt to multiple Personas. Side-by-side transient view.
- **M2. Comparison Setup:** Select 2+ Personas. Single input. Responses in separate panels (horizontal/vertical).
- **M3. Comparison Results:** Each panel: Persona name, response time, token count, cost. Simultaneous streaming.
- **M4. Accepting a Comparison Result:** "Accept" appends to permanent ChatThread. Others discarded (or saved as branches).

### N. Personal Wiki / Second Brain
**Spec:** [`features/personal-wiki.md`](features/personal-wiki.md)

- **N1. Wiki Directory Configuration:** User selects directory of .md files. File system watcher monitors changes.
- **N2. Wiki Indexing:** All .md files indexed for full-text search. Stores filenames, heading hierarchy, content. Auto-updates on file change. Powers wiki search, related sections, @ mentions, AI cross-linking.
- **N3. Wiki Search:** Dedicated search scope for wiki entries. Results with filenames, headings, snippets. Click opens in Wiki Browser.
- **N4. Wiki Browser:** Three-region split: File Tree (collapsible directory tree), Markdown Viewer (rendered content + "Open in External Editor"), Info Panel (Related Sections tab + Backlinks tab + File Info tab with word count, reading time, heading count).
- **N5. Write to Wiki — Core Workflow:** "Discuss then confirm" model. Trigger: toolbar button or context menu. Pipeline: target selection → AI generates polished .md with cross-links → Preview Panel (editable) → Save/Refine in Chat/Append Only/Cancel. For updates: mandatory Diff Viewer.
- **N6. Automatic Wiki Versioning:** Snapshots before modification. Max 30 per file. Total cap 50MB. Recoverable from Wiki Browser.
- **N7. @ Mentions for Wiki Files:** Type @ in textbox → quick-search dropdown of wiki files. Injects full content (or summarized excerpt if >8K tokens).
- **N8. AI Wiki Access Restrictions:** No deletions, no renaming, write-to-wiki only via N5 pipeline.
- **N9. Append-Only Mode:** Toggle in Preview Panel. AI appends under dated heading. Diff shows append only.
- **N10. AI Cross-Linking (Forward + Backlinks):** Tiered pipeline: AI reads index.md → selects candidates → requests full content → generates draft with suggested links → user reviews/accepts. Backlinks suggested after save.
- **N11. Auto-Generated index.md:** Maintained at wiki root. Directory tree, all headings with links, cross-links, recently modified, orphan pages. Generated from local index. AI reads for cross-linking.
- **N12. Find & Replace Across Wiki:** Search and replace across all wiki files with preview of changes. Wiki snapshots (N6) provide undo. Regex support.
- **N13. Git Wiki Version Control:** Initialize git repository in wiki directory. Auto-commit on file change (debounced). Optional GitHub remote push with Personal Access Token (DPAPI-encrypted). Configured in Onboarding Wizard Step 3 and Settings → Wiki.

### O. Data Model & Lifecycle
**Spec:** [`features/data-model-lifecycle.md`](features/data-model-lifecycle.md)

- **O1. Unified ChatThread Model:** Every interaction — Tier 1, 2, or 3 — creates a ChatThread with Messages. Identical model regardless of origin.
- **O2. IsTransient Flagging:** Tier 1/2 = IsTransient=true. Tier 3/elevated = IsTransient=false.
- **O3. Chat Elevation:** Sending reply in transient thread in Studio flips IsTransient to false.
- **O4. 7-Day Auto-Cleanup:** Background task deletes IsTransient=true threads older than 7 days. Exceptions: favorited, tagged, pinned, archived, or containing user replies/branches (auto-elevated).
- **O5. Garbage Collection Policy:** Hard-deleted threads → exclusively linked media/artifacts also deleted. Shared/saved items preserved.
- **O6. Database Compaction:** VACUUM to reclaim disk space after cleanup.

### P. Windows OS Integration
**Spec:** [`features/windows-os-integration.md`](features/windows-os-integration.md)

- **P1. Global Keyboard Hooks:** System-wide hotkey detection regardless of focused application.
- **P2. HWND Capture:** Capture active window handle before drawing overlay UI.
- **P3. Spatial Anchoring:** Save HWND + source app + document title + original text. Enables [Apply] back to source. Fallback to clipboard paste. Missing window = grayed out.
- **P4. Clipboard Format Preservation:** Check DataFormats during capture. Preserve HTML/RTF when returning results.
- **P5. Local WebSocket Server:** localhost WebSocket for direct integrations (e.g., Word Add-in).
- **P6. System Tray:** Minimize to tray. Left-click restores. Right-click menu: New Chat, Open Studio, Command Bar, Recent Chats, Settings, Exit.
- **P7. Session Restore:** Restore previous session's chats and tabs on launch (if A6 enabled).
- **P8. Per-Monitor DPI Awareness:** Full per-monitor DPI awareness. Crisp rendering at any scaling.
- **P9. bash Tool — Windows Adaptation:** bash tool named to match Anthropic schema but executes via `cmd.exe`. `.sh` scripts use Git Bash or WSL fallback. Heredocs redirected to `text_editor`. Cross-platform commands (python, pip, npm) work without translation. bash availability detected at startup and communicated to model via system prompt.
- **P10. Workspace Isolation:** All bash commands execute in `%LOCALAPPDATA%/MySecondBrain/workspace/`. Working directory locked to workspace. Absolute paths outside workspace blocked pre-execution. Wiki directory is read-only from bash. Workspace cleaned up periodically (files older than 24h). The `text_editor` tool bridges workspace to artifacts directory. The `present_files` tool signals finished deliverables.

### Q. Language & RTL Support
**Spec:** [`features/language-rtl.md`](features/language-rtl.md)

- **Q1. English (LTR):** Default. All UI labels in English. Left-to-right text.
- **Q2. Hebrew (RTL):** Auto-detected from Unicode character ranges. Right-to-left rendering.
- **Q3. Mixed LTR/RTL Messages:** Each segment renders in correct direction.

### R. Backup & Recovery
**Spec:** [`features/backup-recovery.md`](features/backup-recovery.md)

- **R1. Google Cloud Storage Backup:** Full backup of SQLite DB, wiki .md files, and artifacts.
- **R2. Backup Schedule:** Daily, weekly, manual. Default: daily.
- **R3. Manual Backup:** Trigger immediate backup from Settings.
- **R4. Restore:** Replace current data with backup. Confirmation dialog.

### S. Usage & Pricing Dashboard
**Spec:** [`features/usage-pricing-dashboard.md`](features/usage-pricing-dashboard.md)

- **S1. Usage Overview Screen:** Comprehensive usage stats — tokens and cost across all providers. Filterable.
- **S2. Time Range Filters:** Today, This Week, This Month, Custom Range, All Time. Default: This Month.
- **S3. Usage Charts:** Line chart (tokens over time), bar chart (cost over time), pie charts (by provider, by model).
- **S4. Per-Chat Breakdown:** Table of chats with token count, cost, models. Click to open chat.
- **S5. Budget Alerts:** Monthly spending limit. Warning at threshold (e.g., 80%). Option to block API calls when exceeded.
- **S6. AI Feedback Summary:** Aggregated 👍/👎 per Persona and Model. Approval percentages, trend charts, rankings. Filterable by time range.

### U. Soft-Delete Trash
**Spec:** [`features/soft-delete-trash.md`](features/soft-delete-trash.md)

- **U1. Soft-Delete on Chat Deletion:** Deleting a chat moves it to Trash (IsDeleted=true, DeletedAt timestamp). 30-day recovery window.
- **U2. Trash View:** Sidebar "🗑️ Trash" showing all soft-deleted chats with Restore and Delete Permanently buttons.
- **U3. 30-Day Auto-Purge:** Background task permanently deletes chats older than 30 days in Trash.
- **U4. Restore from Trash:** Restores chat to original location (folder, tags, pinned status preserved).
- **U5. Permanent Delete from Trash:** Explicit permanent deletion with confirmation. Follows O5 garbage collection.
- **U6. Empty Trash:** Bulk permanent delete of all items in Trash with confirmation.

### V. Diagnostics & Debug Logging
**Spec:** [`features/diagnostics-debug-logging.md`](features/diagnostics-debug-logging.md)

- **V1-V4:** (A11a-A11d) — Log level selector, 8 per-category toggles, Open Logs Folder, Clear Logs. Full spec in [`features/diagnostics-debug-logging.md`](features/diagnostics-debug-logging.md).

### W. Agent Skills
**Spec:** [`features/agent-skills.md`](features/agent-skills.md)

Skills are Markdown instruction files (`SKILL.md`) that encode domain-specific procedural knowledge. The model reads the instructions and uses existing tools (`bash`, `text_editor`, `web_search`, `web_fetch`) to produce output. Skills are NOT executable code.

- **W1. Built-in Skill Set (11 Anthropic Skills):** xlsx, docx, pdf, pptx (document creation); algorithmic-art, canvas-design, frontend-design, theme-factory (creative work); web-artifacts-builder, webapp-testing (development); skill-creator (meta). Shipped as embedded resources, updated with app updates.
- **W2. Progressive Disclosure:** Three-tier loading: (1) Skill catalog (name + description, ~80 tokens each) in system prompt. (2) Full SKILL.md loaded on demand via `skill_load` tool. (3) Bundled scripts/resources accessed via `bash` when instructions reference them. 11 skills = ~880 token catalog upfront, not 11 full instruction sets.
- **W3. skill_load Tool:** Model calls `skill_load("xlsx")` to activate a skill. App returns structured XML-wrapped SKILL.md body with resource listing. Deduplicated — re-activation in same session skipped.
- **W4. Skill Discovery:** Four locations scanned at startup: (a) Embedded `Skills/anthropic/` — 11 built-in skills. (b) `%LOCALAPPDATA%/MySecondBrain/skills/` — user-added community skills. (c) `%USERPROFILE%/.agents/skills/` — cross-client from Claude Code, Cursor, etc. (d) `%USERPROFILE%/.claude/skills/` — pragmatic Claude Code compatibility. User skills override built-in. Cross-client overrides user.
- **W5. Community Skills:** Users add skills from community repos by copying to `%LOCALAPPDATA%/MySecondBrain/skills/`. Discovered alongside built-in skills at startup. Listed in catalog with `source: community` annotation. Never overwritten by app updates. `skill-creator` meta-skill enables users to create their own.
- **W6. Per-Chat Skills Toggle:** Textbox toolbar "📚 Skills ▼" dropdown with individual skill checkboxes + "All on/off." Disabled skills removed from catalog and `skill_load` tool's enum. New chats inherit global defaults from Settings → Skills (A12).
- **W7. System Prompt Construction:** Additive assembly. Skill catalog appears only if ≥1 skill enabled. `skill_load` tool appears only if ≥1 skill enabled. Empty persona + everything disabled = no system prompt, empty tools array.
- **W8. Memory Tool:** SQLite-backed memory store with Anthropic `memory_20250818` schema. Discrete fact entries: key, value, source chat, timestamp. Relevance-based retrieval. Separate from wiki (wiki = user-authored knowledge, memory = AI-extracted facts). Per-chat toggle in toolbar. User can view/edit/delete memories in Settings → Memory (A13).
- **W9. Deep Research as Skill:** Deep Research is a skill rather than a custom state machine. Model follows research protocol using `web_search` + `web_fetch` + `bash` tools. Progress visible naturally as tool calls stream in chat. No custom progress UI needed.
- **W10. Skill Context Protection:** Skill content tagged with `<skill_content>` wrappers exempt from context compaction pruning. Losing skill instructions mid-conversation silently degrades behavior.
- **W11. Dependency Detection:** Skills declare dependencies (Python packages, system tools, Node.js). Model checks availability at runtime and guides user to install missing dependencies. None bundled with the app.

### T. Nice-to-Have Features
**Spec:** [`features/nice-to-have-future.md`](features/nice-to-have-future.md)

Features that are part of the long-term vision but can wait indefinitely. Architecture should accommodate without rework.

- **T1. Macro Genesis:** Compile successful chat sequences into permanent Tier 1 hotkeys.
- **T2. Context-Aware Grouping:** Auto-tag threads by window context (e.g., group all VS Code threads).
- **T3. Passive Autonomous Threads:** Local vision watchdogs proactively spawn threads.
- **T4. Screenshot/Screen Awareness:** Include screenshot of active window in Tier 1/Tier 2 actions.
- **T5. Video Generation:** AI generates video clips when future multi-modal models support it.
- **T6. Wiki Graph View:** Interactive visual graph of wiki file cross-links. Nodes = files, edges = links. Click node to open in Wiki Browser. Uses existing cross-link data (N2/N10/N11).

## Secondary Features

Not applicable — all features in Sections A through W (excluding T, Nice-to-Have) are considered core and required for the complete initial vision. The Nice-to-Have section (T) captures future features.

## Explicitly Out of Scope

Features we are consciously NOT building. These will not be part of the app — now or in the future — unless the vision is explicitly revised.

- **Multi-User Support:** The app is strictly single-user. There will never be user accounts, login screens, role-based access control, or collaboration features.
- **Non-Windows Platforms:** The app is Windows-only. It will never support macOS, Linux, iOS, Android, or web-based access.
- **Web Interface / Browser Access:** There is no web frontend. All interaction is through the native Windows application.
- **Built-in API Proxy Service:** The app does not proxy API calls through a third-party service. API keys are used directly by the app to call provider APIs from the user's machine.
- **Cloud Sync (Beyond Backup):** The app does not synchronize data across multiple devices. Backup to Google Cloud Storage is for disaster recovery only.
- **Built-in Provider Accounts:** The app does not create or manage accounts with AI providers. The user brings their own API keys.
- **Inline Widgets (read_me / show_widget):** Claude.ai's inline widget system for ephemeral SVG/HTML in chat messages is not replicated. MySecondBrain uses the file-based artifact pipeline (F1) for all non-trivial visual output.
