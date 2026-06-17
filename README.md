<div align="center">
  <h1>🧠 MySecondBrain</h1>
  <p><strong>One app. Every AI model. Your knowledge, permanent.</strong></p>
</div>

---

**MySecondBrain** is a native Windows desktop application that replaces ChatGPT.com, Claude.ai, and every other LLM chat platform with a single, provider-agnostic hub. Use any model with your own API keys — no subscriptions, no platform lock-in. Beyond chat, it's a personal wiki that turns AI conversations into polished, searchable Markdown notes stored on your own disk.

---

## The Problem

AI chat is scattered across a dozen websites and apps, each locked to one provider. Conversations are trapped inside platforms — you can't search across them, you can't mix models, and when a chat ends, the knowledge evaporates. Copy-pasting AI output into your notes is manual, tedious, and rarely happens.

## The Solution

MySecondBrain consolidates **every AI interaction** into a single Windows app, across three tiers of engagement — from a one-second hotkey rewrite to a two-hour deep research session. Every interaction is captured, searchable, and can be elevated into permanent knowledge in your personal wiki.

---

## Three-Tier Interaction Model

Unlike every other AI chat tool (which offers exactly one way to interact — a chat window), MySecondBrain provides three tiers:

| Tier | Trigger | What It Is |
|------|---------|------------|
| **Tier 1** | Global hotkey from any app | Highlight text anywhere (VS Code, Word, browser), press your hotkey, and AI transforms it. A minimal overlay appears at your cursor, processes the text, and pushes the result back into the original window. Built-in actions: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt — plus unlimited custom actions. |
| **Tier 2** | `Alt+Space` | A Spotlight-style command bar drops down from the top of your screen. Ask a quick question, get an answer inline, keep asking — all without leaving your current app. Pop it out into a floating mini-window, or elevate the conversation into a permanent Studio chat when it becomes valuable. |
| **Tier 3** | Studio window | The full chat workspace: multi-tab conversations, streaming responses, Markdown/code rendering, artifacts panel, branching conversation trees, model comparison, tool-use agents, media library, and deep integration with your personal wiki. |

Transient Tier 1 and Tier 2 interactions auto-clean after 7 days. Elevate any interaction to a permanent Tier 3 chat with one click — nothing valuable is ever lost.

---

## Personal Wiki / Second Brain

Chat is the input. The wiki is the output.

MySecondBrain indexes a directory of your own Markdown (`.md`) files on disk. When you finish a conversation, **Write to Wiki** uses AI to produce a polished, permanent summary note — with cross-links to related notes, backlinks, and a table of contents. The wiki is:

- **Yours:** Plain `.md` files on your own disk. Edit them anywhere — Obsidian, VS Code, Notepad.
- **Indexed:** Full-text search across all files, headings, and links.
- **Versioned:** Automatic snapshots before every AI modification, with diff view and recovery.
- **AI-Aware:** AI can reference your wiki files in conversations (via `@` mentions), search your wiki, and suggest cross-links between related notes.

---

## Key Features

### AI Chat & Models
- **Provider-agnostic:** OpenAI, Anthropic, Google, DeepSeek, Mistral, and any OpenAI-compatible endpoint (including local models via Ollama/LM Studio)
- **BYO API keys:** Encrypted at rest with Windows DPAPI. No third-party proxy, no account required.
- **Personas:** Reusable AI behavior presets (system prompt + model configuration). Switch on the fly.
- **Streaming responses** with Markdown rendering, syntax-highlighted code blocks, and inline media
- **Thinking/reasoning toggle** per chat
- **Model Comparison:** Send one prompt to 2–4 Personas side-by-side, compare responses, accept the best

### Conversation Management
- **Multi-tab workspace** with drag-and-drop tab reordering
- **Message branching:** Edit any past message as a branch — explore alternate conversation paths
- **Chat tree visualization:** See and navigate all branches in a visual graph
- **Import** from ChatGPT and Claude exports
- **Export** chats as Markdown, PDF, or JSON
- **Locked chats:** AES-256-GCM password-protected encryption for sensitive conversations

### Artifacts & Media
- **AI-generated artifacts** (code files, documents, configs) with version history and diff view
- **Media Library:** Gallery of all images, audio, video, and webcam captures across all chats
- **Image generation and audio generation** via AI models
- **Voice dictation** via speech-to-text (Whisper or compatible)
- **Global Artifacts Browser:** Search and browse all artifacts across all chats

### Tool Use / Agents
- **Web search** — AI can search the web and incorporate results
- **Terminal/script execution** — with mandatory user approval
- **File generation and editing** — AI creates and modifies files on your disk
- **Deep Research:** Autonomous multi-step research with cited, structured reports
- **Auto-approval rules** per tool and per chat

### Personal Wiki
- **Wiki Browser:** Three-panel view — file tree, Markdown viewer, info panel (related sections, backlinks, file info)
- **Write to Wiki:** AI generates polished `.md` notes from conversations
- **@ mentions:** Reference any wiki file in chat by typing `@`
- **AI cross-linking:** Automatic forward links and backlink suggestions
- **Auto-generated `index.md`** with directory tree, headings, links, recently modified, and orphan pages
- **AI Memory:** A `_memory.md` file the AI can maintain and reference across conversations

### Windows Integration
- **Global keyboard hooks** for system-wide hotkeys
- **HWND-aware spatial anchoring** — push transformed text back into the source window
- **Clipboard format preservation** (HTML, RTF)
- **System tray** with quick-access menu
- **Session restore** on launch
- **Per-monitor DPI awareness**
- **Local WebSocket server** for direct integrations (e.g., Word add-in)

### Organization & Search
- **Full-text search** across all chats (permanent + transient)
- **Tags, color labels, folders, archiving, pinning, favoriting**
- **Timeline view** of all transient Tier 1 and Tier 2 interactions
- **Bulk operations:** select multiple chats to delete, archive, export, tag, or move
- **Soft-delete trash** with 30-day recovery

### Usage & Settings
- **Usage Dashboard:** Token/cost analytics with charts, per-chat breakdown, and budget alerts
- **Dark/Light mode** with independent chat themes (Classic, Compact, Bubble)
- **Customizable fonts** and message size
- **Auto-update** with configurable schedule
- **Backup & Recovery** to Google Cloud Storage

**174 feature items across 22 feature groups.** [Full specification →](agent-workspace/project-director/vision/vision-summary.md)

---

## Technology

| Aspect | Choice |
|--------|--------|
| **Platform** | Windows 10 / 11 (native desktop) |
| **UI Framework** | WPF (.NET) — planned |
| **Database** | SQLite (local, zero-configuration) |
| **API Keys** | Encrypted at rest via Windows DPAPI |
| **Wiki Storage** | Plain `.md` files on disk |

MySecondBrain is **Windows-only by design**. Being native enables capabilities no web app can offer: global keyboard hooks, HWND capture for spatial anchoring, clipboard format preservation, and a local WebSocket server. It will never be a web app, a mobile app, or cross-platform.

---

## Project Status

> **Phase:** Vision discovery complete — execution planning in progress.

| Milestone | Status |
|-----------|--------|
| Vision & Product Specification | ✅ Complete — 8 screens, 22 feature groups, 13 data entities, 8 user flows |
| Interactive Wireframe Mocks | ✅ Complete — open `screens/*.html` in browser |
| Architecture & Technical Planning | 🔜 In progress |
| Implementation | ⬜ Not started |

**22 feature groups** are fully specified with detailed behavior, edge cases, and data models. 14 architectural concerns are flagged for review before technical planning begins.

---

## Quick Links

| Document | Description |
|----------|-------------|
| [Vision Summary](agent-workspace/project-director/vision/vision-summary.md) | Complete product index — screens, features, data entities, flows |
| [App Overview](agent-workspace/project-director/vision/app-overview.md) | North Star — what this app is and why it exists |
| [Interactive Wireframe Mocks](agent-workspace/project-director/vision/screens/) | Open any `*.html` in a browser (after cloning) for a clickable simulation of each screen |

---

## Project Structure

```
MySecondBrain/
├── agent-workspace/                    # AI agent workspace (planning & design artifacts)
│   └── project-director/
│       ├── vision/                     # Complete product vision & specification
│       │   ├── vision-summary.md       # Master index of all vision artifacts
│       │   ├── app-overview.md         # Core purpose & North Star
│       │   ├── feature-inventory.md    # All 22 feature groups cataloged
│       │   ├── personas.md             # Target user personas
│       │   ├── edge-cases.md           # Global + per-feature edge cases
│       │   ├── screens/                # Screen specs (*.md) + interactive mocks (*.html)
│       │   ├── features/               # Detailed feature specifications (22 files)
│       │   ├── data/                   # Data entity definitions (13 entities)
│       │   └── flows/                  # End-to-end user flow documents (8 flows)
│       ├── backlog.md                  # Development backlog
│       └── current-state.md            # Current project state tracker
└── README.md                           # This file
```

---

## Getting Started (for Contributors)

The project is in the planning phase — no code yet. Here's how to get oriented:

1. **Read the North Star:** [`agent-workspace/project-director/vision/app-overview.md`](agent-workspace/project-director/vision/app-overview.md) — what we're building and why
2. **Explore the vision:** [`agent-workspace/project-director/vision/vision-summary.md`](agent-workspace/project-director/vision/vision-summary.md) — complete index of all specs
3. **Click through the mocks:** Open `agent-workspace/project-director/vision/screens/*.html` in a browser — interactive wireframes for every screen
4. **Review the backlog:** [`agent-workspace/project-director/backlog.md`](agent-workspace/project-director/backlog.md) — planned work and priorities

---

## License

[MIT](LICENSE) © 2026
