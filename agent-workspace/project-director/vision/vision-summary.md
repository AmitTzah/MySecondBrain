# MySecondBrain — Complete Vision Summary

## App at a Glance

MySecondBrain is a native Windows desktop application that serves as the user's sole interface for all AI language model interactions — a unified, provider-agnostic chat hub that replaces ChatGPT.com, Claude.ai, and all other LLM chat platforms. It operates across three interaction tiers: ephemeral hotkey-triggered text transformations (Tier 1), a Spotlight-style command bar with mini-chat (Tier 2), and a full Studio chat workspace (Tier 3). Beyond chat, it functions as a personal wiki / second-brain: an indexed, searchable knowledge base of user-owned `.md` files on disk, where an AI agent can read a conversation and produce polished, permanent summary notes — turning every AI discussion into lasting knowledge.

The app includes 11 built-in Agent Skills plus community skill support. The AI uses 14 provider-agnostic tools following the Roo Code pattern — including file operations (read_file, list_files, search_files, apply_diff, write_to_file), a workspace-isolated bash executor (per-chat sandbox), web search, image search, web fetch, SQLite-backed persistent memory, wiki search, skill loader, structured confirmations, and file presentation for artifacts. Tools execute in parallel when the model batches independent calls. A generic file viewer renders arbitrary files with syntax highlighting alongside chat tabs. A comprehensive API History viewer provides raw JSON debugging of every API call. Artifacts render in a WebView2-powered side panel with browser-native syntax highlighting, diff views, and interactive React app support.

---

## Project Type

**GUI** (native Windows WPF desktop application). Deliverable format: per-screen `.md` canonical specs + interactive `.html` wireframe mocks with tooltips and navigation. Open `screens/*.html` in a browser for the interactive simulation.

---

## Simulation Reading Guide

This vision document set describes the complete end-state of MySecondBrain in exhaustive, specific, unambiguous detail. To mentally simulate the finished application:

1. **Read this file (vision-summary.md)** for the map — understand what exists and where to find it
2. **Read [`app-overview.md`](app-overview.md)** for the North Star — what this app is and why it exists
3. **Read [`personas.md`](personas.md)** for who it's for — the single primary persona
4. **Browse the index tables below** — locate any screen, feature, entity, or flow
5. **Open `screens/*.html` in a browser** for the interactive mock — click through screens, read tooltips, navigate
6. **Drill into any `screens/[name].md`** for the canonical screen specification
7. **Drill into any `features/[name].md`** for detailed feature behavior
8. **Drill into any `flows/[name].md`** for end-to-end user journeys
9. **Drill into any `data/[name].md`** for entity attributes and lifecycle

---

## Screen Index

| Screen | Purpose | Mock HTML | Canonical Spec | Key Features |
|--------|---------|-----------|----------------|--------------|
| **Studio Chat** | Tier 3 primary workspace — multi-tab AI conversations + file viewer tabs, Markdown rendering (WPF), streaming, artifacts (WebView2), branching, 14-tool agent surface, parallel execution, API History, skills, memory | [`screens/studio-chat.html`](screens/studio-chat.html) | [`screens/studio-chat.md`](screens/studio-chat.md) | C (Studio Chat), D (Branching), E (Chat Modes), F (Artifacts/WebView2), H (14 Tools), J (Prompts), K (Text Actions), L (Organization), M (Comparison), N (Wiki), W (Skills), X (API History), Y (File Viewer) |
| **Onboarding Wizard** | First-launch guided setup — API keys, Persona, wiki directory, hotkeys | [`screens/onboarding-wizard.html`](screens/onboarding-wizard.html) | [`screens/onboarding-wizard.md`](screens/onboarding-wizard.md) | A8 (Onboarding), B1 (API Keys), B3 (Personas), N1 (Wiki Dir), P1 (Hotkeys), I2 (Import) |
| **Model Comparison** | Side-by-side multi-Persona comparison with independent mini-chats, broadcast mode, auto-branching | [`screens/model-comparison.html`](screens/model-comparison.html) | [`screens/model-comparison.md`](screens/model-comparison.md) | M1-M4 (Model Comparison), B3 (Personas), D3 (Branching), E3 (Thinking) |
| **Settings** | Global configuration — 19 categories including new System Info (App Data Locations). 14-tool surface with out-of-workspace read approval. | [`screens/settings.html`](screens/settings.html) | [`screens/settings.md`](screens/settings.md) | A1-A14 (Settings), B (Model Configs), N1 (Wiki Dir), R (Backup), H (14 Tools + out-of-workspace approval), W (Skills), W8 (Memory), Z (App Data Locations) |
| **Wiki Browser** | Three-region browser for personal wiki — file tree, Markdown viewer, info panel (related sections, backlinks, file info) | [`screens/wiki-browser.html`](screens/wiki-browser.html) | [`screens/wiki-browser.md`](screens/wiki-browser.md) | N4 (Wiki Browser), N2 (Indexing), N6 (Snapshots), N10 (Cross-Linking) |
| **Usage Dashboard** | Complete analytics — tokens, cache breakdown, latency distribution, per-provider/model filtering, per-chat breakdown, budget alerts, AI feedback. Every API call across all 3 tiers logged. | [`screens/usage-dashboard.html`](screens/usage-dashboard.html) | [`screens/usage-dashboard.md`](screens/usage-dashboard.md) | S1-S8 (Usage Dashboard, enriched) |
| **Media Library** | Browsable gallery of all media across chats — images, audio, video, webcam captures | [`screens/media-library.html`](screens/media-library.html) | [`screens/media-library.md`](screens/media-library.md) | G1-G6 (Media Library) |
| **Global Artifacts Browser** | Cross-chat artifact listing — code, docs, config files with search, filter, version history | [`screens/global-artifacts-browser.html`](screens/global-artifacts-browser.html) | [`screens/global-artifacts-browser.md`](screens/global-artifacts-browser.md) | F7 (Global Artifacts Browser) |

---

## Feature Index

| Feature Group | Tier | Screens Used | Key Data Entities |
|---------------|------|-------------|-------------------|
| **A. Settings & Configuration** (A1-A13) | Core | Settings, Onboarding Wizard, Studio Chat | api-key, persona, model-configuration, memory-entry |
| **B. Model Configurations & Personas** (B1-B8) | Core | Settings, Studio Chat, Model Comparison, Onboarding Wizard | api-key, model-configuration, persona |
| **C. Studio Chat Workspace** (C1-C37) | Core | Studio Chat | chat-thread, message, persona, usage-record |
| **D. Message Manipulation & Branching** (D1-D9) | Core | Studio Chat | message, chat-thread |
| **E. Chat Modes & Controls** (E1-E5) | Core | Studio Chat | persona, model-configuration |
| **F. Artifacts & Side Panel** (F1-F7) | Core | Studio Chat, Global Artifacts Browser | artifact |
| **G. Media Library & Multi-Modal** (G1-G6) | Core | Media Library, Studio Chat | media-item |
| **H. Tool Use / Agent Capabilities** (H1-H15) | Core | Studio Chat | chat-thread, message, memory-entry |
| **I. Import & Export** (I1-I2) | Core | Studio Chat, Settings, Onboarding Wizard | chat-thread, message |
| **J. Prompt Library** (J1-J2) | Core | Studio Chat | prompt-template |
| **K. Text Actions & Three-Tier** (K1-K5) | Core | Tier 1/2 overlays, Studio Chat, Settings | text-action (now with chatMode), chat-thread, message |
| **L. Chat Organization & Search** (L1-L14) | Core | Studio Chat | chat-thread, message |
| **M. Model Comparison** (M1-M4) | Core | Model Comparison, Studio Chat | persona, model-configuration, chat-thread |
| **N. Personal Wiki / Second Brain** (N1-N13) | Core | Wiki Browser, Studio Chat, Settings, Onboarding Wizard | wiki-file, wiki-version-snapshot |
| **O. Data Model & Lifecycle** (O1-O6) | Core | All screens (background) | chat-thread, message, artifact, media-item |
| **P. Windows OS Integration** (P1-P11) | Core | All screens (system-wide) | (system — no data entity) |
| **Q. Language & RTL** (Q1-Q3) | Core | All screens | (system — no data entity) |
| **R. Backup & Recovery** (R1-R4) | Core | Settings | backup-snapshot |
| **S. Usage & Pricing Dashboard** (S1-S8) | Core | Usage Dashboard | usage-record (enriched — cache tokens, latency, tier, error info) |
| **U. Soft-Delete Trash** (U1-U6) | Core | Studio Chat | chat-thread |
| **V. Diagnostics & Debug Logging** (A11a-A11d) | Core | Settings | (logging — no data entity) |
| **W. Agent Skills** (W1-W11) | Core | Studio Chat, Settings | memory-entry, skill (in-memory) |
| **X. API History Viewer** (X1-X3) | Core | Studio Chat | usage-record, per-chat raw JSON log |
| **Y. Generic File Viewer Tabs** (Y1-Y5) | Core | Studio Chat | (file system — no data entity) |
| **Z. App Data Locations** (Z1-Z3) | Core | Settings | (file system — no data entity) |
| **T. Nice-to-Have (Future)** (T1-T6) | Nice-to-Have | (deferred) | (deferred) |

---

## Data Entity Index

| Entity | Description | Appears On |
|--------|-------------|------------|
| [`api-key`](data/api-key.md) | Encrypted API key for an AI provider | Settings (Providers), Onboarding Wizard (Step 1) |
| [`artifact`](data/artifact.md) | File-based AI deliverable presented via `present_files` tool, versioned by filename within chat | Studio Chat (WebView2 artifacts panel), Global Artifacts Browser |
| [`backup-snapshot`](data/backup-snapshot.md) | Full backup archive (DB + wiki + artifacts) | Settings (Backup) |
| [`chat-thread`](data/chat-thread.md) | Unified conversation container — all tiers | Studio Chat, Timeline, Wiki Browser (via @ mentions) |
| [`media-item`](data/media-item.md) | Image, audio, video, webcam capture | Studio Chat, Media Library |
| [`memory-entry`](data/memory-entry.md) | AI-extracted fact about the user (SQLite, Anthropic memory_20250818 schema) | Settings (Memory), Studio Chat (memory tool calls) |
| [`message`](data/message.md) | Single message in a ChatThread | Studio Chat, Model Comparison |
| [`model-configuration`](data/model-configuration.md) | Provider/model/temperature/token settings | Settings (Profiles), Studio Chat (toolbar), Model Comparison |
| [`persona`](data/persona.md) | AI behavior preset (system prompt + default config) | Settings (Profiles), Studio Chat (toolbar/header), Model Comparison, Onboarding Wizard |
| [`prompt-template`](data/prompt-template.md) | Saved reusable prompt with variables | Studio Chat (toolbar) |
| [`skill`](data/skill.md) | Agent Skill metadata (in-memory, not persisted) — Markdown instruction file for domain-specific AI capabilities | Studio Chat (toolbar Skills dropdown), Settings (Skills), system prompt catalog |
| [`text-action`](data/text-action.md) | Named text transformation with hotkey | Tier 1 overlays, Settings (Hotkeys), Studio Chat (toolbar) |
| [`usage-record`](data/usage-record.md) | Complete API call audit log — token counts (incl. cache), latency, cost, tier, error info, raw JSON path | Studio Chat (header), Usage Dashboard, API History |
| [`wiki-file`](data/wiki-file.md) | Indexed .md file in personal wiki directory | Wiki Browser, Studio Chat (via @ mentions, Write to Wiki) |
| [`wiki-version-snapshot`](data/wiki-version-snapshot.md) | Pre-modification snapshot of a wiki file | Wiki Browser (Version History) |

---

## Flow Index

| Flow | Persona | Goal | Screens Involved |
|------|---------|------|-----------------|
| [`first-launch-onboarding`](flows/first-launch-onboarding.md) | Hybrid Developer/Knowledge Worker | Complete initial setup and arrive at Studio | Onboarding Wizard → Studio Chat |
| [`tier1-hotkey-rewrite`](flows/tier1-hotkey-rewrite.md) | Hybrid Developer/Knowledge Worker | Trigger AI text actions in any Windows app via global hotkey | Tier 1 overlay → (optional) Studio Chat |
| [`tier2-command-bar-query`](flows/tier2-command-bar-query.md) | Hybrid Developer/Knowledge Worker | Quick AI Q&A via Spotlight-style overlay | Tier 2 Command Bar → (optional) Studio Chat |
| [`write-to-wiki`](flows/write-to-wiki.md) | Hybrid Developer/Knowledge Worker | Transform chat conversation into permanent .md wiki file | Studio Chat → Wiki Browser |
| [`deep-research`](flows/deep-research.md) | Hybrid Developer/Knowledge Worker | Autonomous multi-source research via skill-based protocol | Studio Chat |
| [`elevate-transient-to-permanent`](flows/elevate-transient-to-permanent.md) | Hybrid Developer/Knowledge Worker | Promote a Tier 1/2 transient chat to permanent Studio chat | Timeline tab → Studio Chat |
| [`import-chatgpt`](flows/import-chatgpt.md) | Hybrid Developer/Knowledge Worker | Migrate ChatGPT/Claude history into MySecondBrain | Onboarding Wizard (Finish) or Settings → Studio Chat |
| [`model-comparison-flow`](flows/model-comparison-flow.md) | Hybrid Developer/Knowledge Worker | Compare 2-4 Personas side-by-side, accept best, auto-branch rest | Studio Chat → Model Comparison → Studio Chat |

---

## File Manifest

### Foundation (4 files)
| File | Description |
|------|-------------|
| [`app-overview.md`](app-overview.md) | Core purpose, elevator pitch, differentiators, platform, success metrics |
| [`personas.md`](personas.md) | Primary persona: Hybrid Developer/Knowledge Worker/Creative Writer |
| [`feature-inventory.md`](feature-inventory.md) | All 26 feature groups (A-Z) cataloged by tier |
| [`edge-cases.md`](edge-cases.md) | Global scenarios + per-feature edge cases for all 26 feature groups |

### Features (26 files)
| File | Feature Group |
|------|---------------|
| [`features/settings-configuration.md`](features/settings-configuration.md) | A. Settings & Configuration |
| [`features/model-configurations-personas.md`](features/model-configurations-personas.md) | B. Model Configurations & Personas |
| [`features/studio-chat-workspace.md`](features/studio-chat-workspace.md) | C. Studio Chat Workspace |
| [`features/message-manipulation-branching.md`](features/message-manipulation-branching.md) | D. Message Manipulation & Branching |
| [`features/chat-modes-controls.md`](features/chat-modes-controls.md) | E. Chat Modes & Controls |
| [`features/artifacts-side-panel.md`](features/artifacts-side-panel.md) | F. Artifacts & Side Panel (WebView2) |
| [`features/media-library.md`](features/media-library.md) | G. Media Library |
| [`features/tool-use-agents.md`](features/tool-use-agents.md) | H. Tool Use (14-tool provider-agnostic surface) |
| [`features/import-export.md`](features/import-export.md) | I. Import & Export |
| [`features/prompt-library.md`](features/prompt-library.md) | J. Prompt Library |
| [`features/text-actions-three-tier.md`](features/text-actions-three-tier.md) | K. Text Actions & Three-Tier |
| [`features/chat-organization-search.md`](features/chat-organization-search.md) | L. Chat Organization & Search |
| [`features/model-comparison.md`](features/model-comparison.md) | M. Model Comparison |
| [`features/personal-wiki.md`](features/personal-wiki.md) | N. Personal Wiki / Second Brain |
| [`features/data-model-lifecycle.md`](features/data-model-lifecycle.md) | O. Data Model & Lifecycle |
| [`features/windows-os-integration.md`](features/windows-os-integration.md) | P. Windows OS Integration |
| [`features/language-rtl.md`](features/language-rtl.md) | Q. Language & RTL |
| [`features/backup-recovery.md`](features/backup-recovery.md) | R. Backup & Recovery |
| [`features/usage-pricing-dashboard.md`](features/usage-pricing-dashboard.md) | S. Usage & Pricing Dashboard |
| [`features/soft-delete-trash.md`](features/soft-delete-trash.md) | U. Soft-Delete Trash |
| [`features/diagnostics-debug-logging.md`](features/diagnostics-debug-logging.md) | V. Diagnostics & Debug Logging |
| [`features/agent-skills.md`](features/agent-skills.md) | W. Agent Skills |
| [`features/api-history-viewer.md`](features/api-history-viewer.md) | X. API History Viewer (NEW) |
| [`features/file-viewer-tabs.md`](features/file-viewer-tabs.md) | Y. Generic File Viewer Tabs (NEW) |
| [`features/app-data-locations.md`](features/app-data-locations.md) | Z. App Data Locations (NEW) |
| [`features/nice-to-have-future.md`](features/nice-to-have-future.md) | T. Nice-to-Have Features (Future) |

### Data Entities (15 files)
| File | Entity |
|------|--------|
| [`data/api-key.md`](data/api-key.md) | API Key for AI providers |
| [`data/artifact.md`](data/artifact.md) | AI-presented file artifact with versions |
| [`data/backup-snapshot.md`](data/backup-snapshot.md) | Backup archive |
| [`data/chat-thread.md`](data/chat-thread.md) | Unified conversation container |
| [`data/media-item.md`](data/media-item.md) | Image, audio, video, webcam capture |
| [`data/memory-entry.md`](data/memory-entry.md) | AI-extracted fact (SQLite, Anthropic schema) |
| [`data/message.md`](data/message.md) | Single chat message |
| [`data/model-configuration.md`](data/model-configuration.md) | Model engine configuration |
| [`data/persona.md`](data/persona.md) | AI behavior preset |
| [`data/prompt-template.md`](data/prompt-template.md) | Reusable prompt with variables |
| [`data/skill.md`](data/skill.md) | Agent Skill metadata (in-memory) |
| [`data/text-action.md`](data/text-action.md) | Named text transformation |
| [`data/usage-record.md`](data/usage-record.md) | Complete API call audit log — tokens (incl. cache), latency, cost, tier, error info, raw JSON path |
| [`data/wiki-file.md`](data/wiki-file.md) | Indexed .md wiki file |
| [`data/wiki-version-snapshot.md`](data/wiki-version-snapshot.md) | Wiki file snapshot |

### Screens (16 files — 8 .md + 8 .html)
| File | Type |
|------|------|
| [`screens/studio-chat.md`](screens/studio-chat.md) + [`.html`](screens/studio-chat.html) | Canonical spec + Interactive mock |
| [`screens/onboarding-wizard.md`](screens/onboarding-wizard.md) + [`.html`](screens/onboarding-wizard.html) | Canonical spec + Interactive mock |
| [`screens/model-comparison.md`](screens/model-comparison.md) + [`.html`](screens/model-comparison.html) | Canonical spec + Interactive mock |
| [`screens/settings.md`](screens/settings.md) + [`.html`](screens/settings.html) | Canonical spec + Interactive mock |
| [`screens/wiki-browser.md`](screens/wiki-browser.md) + [`.html`](screens/wiki-browser.html) | Canonical spec + Interactive mock |
| [`screens/usage-dashboard.md`](screens/usage-dashboard.md) + [`.html`](screens/usage-dashboard.html) | Canonical spec + Interactive mock |
| [`screens/media-library.md`](screens/media-library.md) + [`.html`](screens/media-library.html) | Canonical spec + Interactive mock |
| [`screens/global-artifacts-browser.md`](screens/global-artifacts-browser.md) + [`.html`](screens/global-artifacts-browser.html) | Canonical spec + Interactive mock |

### Flows (8 files)
| File | Flow |
|------|------|
| [`flows/first-launch-onboarding.md`](flows/first-launch-onboarding.md) | First-launch onboarding |
| [`flows/tier1-hotkey-rewrite.md`](flows/tier1-hotkey-rewrite.md) | Tier 1 hotkey text action |
| [`flows/tier2-command-bar-query.md`](flows/tier2-command-bar-query.md) | Tier 2 Command Bar query |
| [`flows/write-to-wiki.md`](flows/write-to-wiki.md) | Write to Wiki |
| [`flows/deep-research.md`](flows/deep-research.md) | Deep Research (skill-based) |
| [`flows/elevate-transient-to-permanent.md`](flows/elevate-transient-to-permanent.md) | Elevate transient to permanent |
| [`flows/import-chatgpt.md`](flows/import-chatgpt.md) | Import from ChatGPT/Claude |
| [`flows/model-comparison-flow.md`](flows/model-comparison-flow.md) | Model Comparison |

### Meta (1 file)
| File | Description |
|------|-------------|
| [`state.json`](state.json) | Single source of truth — phase, progress, metrics, last_commit |

---

## Flagged Concerns

The following items are flagged for Architect review before technical planning. These are NOT blockers — they are documented concerns the Architect should evaluate and resolve.

1. ⚠️ **Auto-Summarize context overflow** requires a separate API call costing tokens. Must be transparent to the user. (B8)

2. ⚠️ **Text completion APIs being deprecated** by some providers. Feature E2 may need fallback strategy.

3. ⚠️ **WebView2 runtime dependency** — adds ~100MB to install size. Pre-installed on Windows 11, auto-installed on Windows 10. Fallback to WPF-only rendering if unavailable. (F2, F6)

4. ⚠️ **bash tool security model** — workspace isolation is app-enforced, not OS-level sandboxing. Path blocking covers common patterns but is not a security boundary. (H1, P10, P11)

5. ⚠️ **WebSocket server security** — localhost-only by default but security model needs review. (P5)

6. ⚠️ **Global keyboard hooks may trigger antivirus false positives** — some security software flags global hooks as keyloggers. (P1)

7. ⚠️ **VACUUM requires temporary free disk space** equal to database size. Low-disk scenarios need handling. (O6)

8. ⚠️ **Rich text bidirectional text rendering complexity** — mixing LTR and RTL in chat messages with code blocks requires careful implementation. (Q2-Q3)

9. ⚠️ **Google Cloud Storage dependency for backups** — single cloud provider dependency. Local folder backup alternative available. (R1)

10. ⚠️ **Locked chat encryption — permanent lockout risk** must be clearly communicated. No recovery mechanism. (C31)

11. ⚠️ **Git wiki version control — GitHub token storage** requires DPAPI encryption. Token scope should be minimal (repo-only).

12. ⚠️ **Model comparison — per-panel multi-turn conversations** = N independent API streams with cost/connection implications.

13. ⚠️ **Skill dependency management** — 11 skills require Python, Node.js, LibreOffice, pandoc, qpdf. None bundled with app. Model guides user to install missing dependencies at runtime. (W11)

14. ⚠️ **Workspace isolation is app-enforced** — path blocking covers common patterns (`C:\`, `%`, `~`) but is not a security sandbox. Malicious models could attempt bypass via PowerShell, WSL, or indirect path resolution. (P10, P11)

15. ⚠️ **present_files auto-copy from workspace** — large files may cause UI pauses during copy. Size limits and async copy recommended.

16. ⚠️ **Memory tool (SQLite) separate from wiki** — two knowledge persistence mechanisms. Risk of user confusion: "why does AI remember X but not Y?" Clear distinction: memory = AI-extracted facts, wiki = user-authored knowledge. (W8, N)

17. ⚠️ **14-tool provider-agnostic schemas** — tool descriptions are designed for instruction-following by any model, but model compliance varies. Some models may misuse tools or ignore constraints. Tool schemas must be tested per provider.

18. ⚠️ **Out-of-workspace file read approval** — configurable per read-tool (Auto-Approve/Ask/Disabled). Auto-Approve removes a safety gate. Ask mode may cause frequent confirmation dialogs. Default: Ask.

19. ⚠️ **Per-chat workspace isolation** — subdirectories under `workspace/{chat-id}/` prevent inter-chat conflicts, but a malicious model within one chat can still read/write anything within its own workspace. Workspace is app-scoped, not OS-sandboxed.

20. ⚠️ **Raw API JSON log per chat** — `_api_history.json` may grow very large for long conversations with many tool calls. File is cleaned up with workspace (24h after chat close). For persistent debugging, the UsageRecord entity provides structured queryable data.

21. ⚠️ **Parallel tool execution — model-dependent behavior** — some models may not batch independent tool calls, reducing the benefit of parallel execution. The app handles parallel execution when the model requests it, but cannot force the model to batch calls.

22. ⚠️ **Generic file viewer — Markdown rendering** — .md files render with formatting in the file viewer. This uses the same engine as the Wiki Browser viewer but WITHOUT backlinks/indexing. Risk: user may expect wiki features (backlinks, related sections) to work in the file viewer.

---

## Completion Status

### Metrics
| Metric | Count |
|--------|-------|
| Features documented | 26 feature groups (A-Z), 195+ individual feature items |
| Screens designed | 8 screens with canonical .md specs + interactive .html mocks |
| Data entities | 15 entities with attributes, lifecycle, relationships, UI visibility |
| User flows | 8 end-to-end journeys documented |
| Flagged concerns | 22 items for Architect review |

### Phase Completion
| Phase | Status | Artifacts |
|-------|--------|-----------|
| Phase 1: Foundation | ✅ Complete | app-overview.md, personas.md, feature-inventory.md, 26 feature specs, 15 data entities |
| Phase 2: Screens | ✅ Complete | 8 screen .md specs + 8 interactive .html mocks |
| Phase 3: Flows | ✅ Complete | 8 flow documents |
| Phase 4: Edge Cases | ✅ Complete | edge-cases.md — global + 26 feature-specific |
| Phase 5: Wire-Up | ✅ Complete | vision-summary.md generated; screen HTML consistency maintained |
| Phase 6: Report Back | ✅ Complete | Final report delivered |

### 2026-06-25 Update — 7-Item Vision Refresh
Major updates applied:
- **14-tool provider-agnostic surface**: text_editor replaced by read_file, list_files, search_files, apply_diff, write_to_file. Tool count 10 → 14. Roo Code pattern. Schemas designed for any model.
- **Parallel tool execution**: Independent tools run in parallel. Sequential display with indicator.
- **Per-chat workspace isolation**: Each chat gets own workspace subdirectory. 24h grace period cleanup.
- **Approval model**: Out-of-workspace read configurable per read-tool. Blocked paths always denied.
- **X. API History Viewer**: Per-chat raw JSON log. Opens in generic file viewer tab.
- **Y. Generic File Viewer Tabs**: Read-only tabs alongside chat tabs. Text/code with syntax highlighting, Markdown rendered, images with zoom/pan.
- **Z. App Data Locations**: System Info category in Settings. Comprehensive reference of all app file/folder paths.
- **Enriched UsageRecord**: cacheReadTokens, cacheCreationTokens, latencyMs, tier, errorInfo, rawJsonPath.
- **Usage Dashboard upgrade**: Provider/model filter dropdowns, cache breakdown, latency distribution (avg/p50/p95/p99).
- **Text Completion mode for Text Actions**: TextAction.chatMode field (Standard/TextCompletion). "Continue Writing" defaults to TextCompletion.
- **K2 toolbar**: Updated to 14 tools. Out-of-workspace read approval per read-tool.

---

*Vision last updated: 2026-06-25 (7-item vision update — 14-tool provider-agnostic surface, parallel execution, API History viewer, generic file viewer tabs, rich UsageRecord with cache/latency/tier, Text Completion mode for Text Actions, App Data Locations panel). Interactive mocks at `screens/*.html` — open in browser for clickable simulation.*
