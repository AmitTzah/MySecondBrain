# Feature Inventory

## Core Features

Features without which the app has no reason to exist. These MUST be present in the initial release. Detailed behavioral specifications are in `features/[feature-group].md`.

### A. Settings & Configuration
**Spec:** [`features/settings-configuration.md`](features/settings-configuration.md)

- **A1. Global Settings Screen:** A dedicated settings screen with all global configuration options organized into sections (Providers, Profiles, Appearance, Wiki, Backup, Hotkeys, Tools, Language, Notifications, Startup, Updates, Security, Maintenance).
- **A2. Default Profile Selection:** User selects which model profile is auto-assigned when creating a new chat.
- **A3. Appearance Settings:** At least three visual themes for chat view (Classic, Compact, Bubble). Customizable font family, size, and weight for messages.
- **A4. Notification Settings:** Toggle sound on assistant completion. Option to disable streaming entirely. Per-chat mute toggle.
- **A5. Dark Mode / Light Mode:** Toggle for entire application UI. Independent of chat themes (A3). Dark mode is default.
- **A6. Startup Behavior:** Option to launch on Windows startup. Option to restore last session (reopen all chats/tabs).
- **A7. Auto-Update:** Check on startup, periodically, or manual. Notify and install when available.
- **A8. Onboarding Wizard:** First-launch guided setup: add API key, create profile, select wiki directory, configure hotkeys. Each step skippable. Re-launchable from Settings.
- **A9. Database Maintenance:** "Compact Database" button runs SQLite VACUUM. Displays size before/after.
- **A10. Speech-to-Text (STT) Provider:** Configure dedicated STT provider and model for voice dictation (separate from text-generation configs). Supports OpenAI Whisper API, local Whisper, OpenAI-compatible STT endpoints.

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
- **C5. Message Actions:** Send (Enter/button), Stop generation, Regenerate last response.
- **C5a. Conditional [Apply] Button (Tier 1 Elevation):** When chat originated from Tier 1 text transformation: source indicator banner + [Apply Latest] in header. Per-message [Apply] on direct transformations. Grayed out if source app closed.
- **C6. Copy:** Each message has Copy button placing raw Markdown AND Rich Text (HTML/RTF) on clipboard. Copy entire conversation option.
- **C7. Chat Titling:** Auto-generated from first user message via AI. Manually editable.
- **C8. Continue Generation:** "Continue" button when last message is from assistant. Sends continuation request.
- **C9. Drag & Drop Files/Media:** Drag files/images/video/audio into input. Images for vision models. Text files read into prompt. Other files: metadata included.
- **C10. Multiple Chat Tabs:** Open multiple chats in tabs. Reorder by drag-drop. Close, reopen from sidebar.
- **C11. Token Usage & Context Display:** Per-message token count + cost. Chat header shows context size vs max window and cumulative cost. Local tokenizer for real-time feedback.
- **C12. Keyboard Shortcuts (Studio):** Ctrl+N (new), Ctrl+W (close tab), Ctrl+Shift+T (reopen), Ctrl+Tab/Shift+Tab (next/prev tab), Ctrl+F (search in chat), Ctrl+Shift+F (global search), Ctrl+S (export). Ctrl+/ for shortcut reference.
- **C13. Resizable Panels:** Sidebar, artifact panel, chat nav bar all resizable. Min/max enforced. Sizes remembered.
- **C14. Error Handling & Retry:** Specific error message + Retry button on API failure. Escalating message on consecutive failures.
- **C15. Scroll-to-Bottom Button:** Floating button when scrolled up during streaming. Smooth scroll to latest.
- **C16. Clear Conversation:** Clear all messages, preserve chat with title/tags/settings. Confirmation dialog.
- **C17. Auto-Scroll Behavior:** Auto-pauses when user scrolls up during generation. Handles media height changes smoothly.
- **C18. Message Selection Mode:** Checkbox selection for bulk actions: Copy Selected, Delete Selected, Quote Selected.
- **C19. Offline/Network Status Indicator:** Green/Yellow/Red indicator. Offline banner when disconnected.
- **C20. Close Confirmation with Active Generation:** Confirmation dialog when closing tab/window during generation.
- **C21. Audio Input (Microphone):** Voice dictation via configured STT Provider. Editable transcribed text.
- **C22. Camera Capture:** Webcam photo capture, immediately attached to message for vision models.
- **C23. Pin Window / Always on Top:** Toggle in header. Remembered across sessions.

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

### F. Artifacts & Side Panel File Editing
**Spec:** [`features/artifacts-side-panel.md`](features/artifacts-side-panel.md)

- **F1. AI-Generated Artifacts:** Named artifacts (code, docs, config files) with type inferred from language/extension.
- **F2. Side Panel:** Resizable panel right of chat. Lists all artifacts by name. Click to view content.
- **F3. Version History:** Each artifact maintains versions (v1, v2, v3...). AI produces new versions on changes.
- **F4. Diff View:** Side-by-side or unified diff between any two versions.
- **F5. Version Switching:** Switch active version. Reverting + changes = new branch.
- **F6. Artifact Viewer:** Syntax highlighting for code, rendered view for Markdown. "Save to Disk" and "Save to Wiki" buttons (Save to Wiki launches N5 pipeline).
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

- **H1. Browser Search:** AI requests web search. App executes, feeds results back.
- **H2. Terminal/Script Execution:** AI requests shell command. ALWAYS requires explicit user confirmation. Command displayed before approval.
- **H3. File Generation:** AI creates new files on disk. User approves target path.
- **H4. File Editing:** AI modifies existing files. User approves and can review.
- **H5. Tool Auto-Approval:** Global defaults + per-chat overrides for which tools auto-execute.
- **H6. Deep Research:** Autonomous multi-step research: plan → multiple searches → read sources → synthesize → structured report with citations. Real-time progress display.
- **H7. Wiki Search Tool:** AI queries local wiki index to find relevant .md files. Incorporates into responses.

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

- **K1. Text Actions (Unified):** Named text transformation actions (name + system prompt + Model Configuration). Built-in defaults: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt. Custom actions supported. Available as hotkeys (Tier 1) and toolbar dropdown (Studio).
- **K2. Textbox Toolbar:** Controls above chat input: Persona selector, thinking toggle, mute toggle, tools toggle, auto-approval override, prompt library, Text Actions dropdown.
- **K3. Tier 1 — Global Hotkey Text Actions:** Three phases: Capture (highlighted text + HWND + "Thinking..." overlay), Result Popup (editable AI output + Accept/Discard/Open in Studio/Retry + Additional Instructions field), Apply (HWND injection or clipboard fallback + confirmation toast + Undo).
- **K4. Tier 2 — Command Bar (Alt+Space):** Spotlight-style overlay. Inline state: input field, Q&A display, Pop-out/Close/Copy controls. Popped-out state: floating resizable mini-window with Open in Studio, Pin, Minimize, Close. Elevation to Studio. Dismissal saves as transient thread.
- **K5. Tier 3 — Studio Chat:** Full chat workspace (Section C). Opened via main window, tray icon, or elevation.

### L. Chat Organization & Search
**Spec:** [`features/chat-organization-search.md`](features/chat-organization-search.md)

- **L1. Sidebar Chat List:** All permanent chats sorted by selected order. Pinned at top. Grouped by date. Shows title, preview, timestamp, star, tags.
- **L2. Chat Favoriting:** Star/unstar. Filter toggle for favorites only.
- **L3. Full-Text Search:** Searches all chat messages (permanent + transient in window). Results with snippets, chat name, timestamp, highlights. Click to open and scroll.
- **L4. Delete Chat:** Confirmation dialog. Cascading deletion of media/artifacts exclusively linked.
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
- **N4. Wiki Browser:** Three-region split: File Tree (collapsible directory tree), Markdown Viewer (rendered content + "Open in External Editor"), Info Panel (Related Sections tab + Backlinks tab).
- **N5. Write to Wiki — Core Workflow:** "Discuss then confirm" model. Trigger: toolbar button or context menu. Pipeline: target selection → AI generates polished .md with cross-links → Preview Panel (editable) → Save/Refine in Chat/Append Only/Cancel. For updates: mandatory Diff Viewer.
- **N6. Automatic Wiki Versioning:** Snapshots before modification. Max 30 per file. Total cap 50MB. Recoverable from Wiki Browser.
- **N7. @ Mentions for Wiki Files:** Type @ in textbox → quick-search dropdown of wiki files. Injects full content (or summarized excerpt if >8K tokens).
- **N8. AI Wiki Access Restrictions:** No deletions, no renaming, write-to-wiki only via N5 pipeline.
- **N9. Append-Only Mode:** Toggle in Preview Panel. AI appends under dated heading. Diff shows append only.
- **N10. AI Cross-Linking (Forward + Backlinks):** Tiered pipeline: AI reads index.md → selects candidates → requests full content → generates draft with suggested links → user reviews/accepts. Backlinks suggested after save.
- **N11. Auto-Generated index.md:** Maintained at wiki root. Directory tree, all headings with links, cross-links, recently modified, orphan pages. Generated from local index. AI reads for cross-linking.

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

### T. Nice-to-Have Features
**Spec:** [`features/nice-to-have-future.md`](features/nice-to-have-future.md)

Features that are part of the long-term vision but can wait indefinitely. Architecture should accommodate without rework.

- **T1. Macro Genesis:** Compile successful chat sequences into permanent Tier 1 hotkeys.
- **T2. Context-Aware Grouping:** Auto-tag threads by window context (e.g., group all VS Code threads).
- **T3. Passive Autonomous Threads:** Local vision watchdogs proactively spawn threads.
- **T4. Screenshot/Screen Awareness:** Include screenshot of active window in Tier 1/Tier 2 actions.
- **T5. Video Generation:** AI generates video clips when future multi-modal models support it.

## Secondary Features

Not applicable — all features in Sections A through S are considered core and required for the complete initial vision. The Nice-to-Have section (T) captures future features.

## Explicitly Out of Scope

Features we are consciously NOT building. These will not be part of the app — now or in the future — unless the vision is explicitly revised.

- **Multi-User Support:** The app is strictly single-user. There will never be user accounts, login screens, role-based access control, or collaboration features.
- **Non-Windows Platforms:** The app is Windows-only. It will never support macOS, Linux, iOS, Android, or web-based access.
- **Web Interface / Browser Access:** There is no web frontend. All interaction is through the native Windows application.
- **Built-in API Proxy Service:** The app does not proxy API calls through a third-party service. API keys are used directly by the app to call provider APIs from the user's machine.
- **Cloud Sync (Beyond Backup):** The app does not synchronize data across multiple devices. Backup to Google Cloud Storage is for disaster recovery only.
- **Built-in Provider Accounts:** The app does not create or manage accounts with AI providers. The user brings their own API keys.
