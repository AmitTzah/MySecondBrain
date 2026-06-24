# How MySecondBrain Artifacts & Skills Work — End to End

> **Based on:** Claude.ai's artifacts architecture (reverse-engineered) · adapted for native Windows WPF desktop app.
> This is a developer reference document — not a vision spec. See [`vision/`](../vision/) for product requirements.

---

## How Version Numbers Get Into Filenames

The filename is the entire artifact identity mechanism. There is no `artifact_id`, no version registry, no lineage tracking database. The filename chosen by the model determines everything:

| Filename vs previous (within same chat) | App behaviour |
|----------------------------------------|---------------|
| Same filename | New version of same artifact (v2, v3...) |
| Different filename | New, unrelated artifact (v1) |

The model has zero version awareness. If the user says "call it v4," the model writes to `report-v4.md` — a new artifact. If the user says "add item 4" and the model `str_replace`s `report.md`, the app detects the same filename → v2. Version numbers are purely client-side, tracked per filename within a chat.

---

## 1. What an Artifact Actually Is

An artifact is a file surfaced in the WebView2-powered side panel. The primitive sequence:

1. Model writes a file to the **workspace** using `text_editor` or `bash`
2. Model calls `present_files` pointing at that file
3. App copies file from workspace to **artifacts directory**, renders it in WebView2 side panel

There is no `create_artifact` or `update_artifact` tool. The word "artifact" does not appear in any tool name. It is entirely a client-side label for a presented file. The model just writes files and presents them.

### Side Panel Rendering (WebView2)

The artifacts side panel uses an embedded Microsoft Edge WebView2 control. Browser-native rendering for:
- **Code:** syntax highlighting via Prism.js/highlight.js (200+ languages), line numbers, copy button
- **Markdown:** rendered via marked.js with typography, table formatting, link handling
- **Interactive HTML/React:** full React/Tailwind/shadcn/ui rendering from `web-artifacts-builder` skill
- **Diff views:** side-by-side or unified via diff2html.js (diff computation is app-side in C#)
- **SVG/PDF:** browser-native rendering
- **Unknown types:** plain text with monospace font

Chat conversation rendering stays WPF-native (FlowDocument + ContentBlockRenderers). Two rendering surfaces: WPF for chat, WebView2 for artifacts.

### Side Panel Controls

| Control | Behaviour |
|---------|-----------|
| Preview / Code toggle | Rendered output ↔ raw source |
| Version selector | Dropdown of all versions (app-maintained per filename) |
| Copy | Raw content to clipboard |
| Save to Disk | Export file to user-chosen path |
| Save to Wiki | Launch Write-to-Wiki pipeline (N5) with content pre-filled |
| Inline section edit | Highlight text in rendered Markdown → "Edit with AI" → targeted `str_replace` |
| "Send error to chat" | On render error: copies error into chat for model to diagnose |

Built-in side-by-side and unified diff views between any two versions.

---

## 2. Filesystem Layout — Workspace vs Artifacts

MySecondBrain uses a two-zone model matching Claude.ai's pattern:

| Path | Access | Purpose | Cleanup | Equivalent |
|------|--------|---------|---------|------------|
| `%LOCALAPPDATA%/MySecondBrain/workspace/` | **Read/write** | Scratch/working space — bash execution, temp files, intermediate work. Invisible to user. | 24h auto-cleanup | Claude's `/home/claude/` |
| Artifacts directory (app-managed) | **Read/write** | Final deliverables — presented files land here. Watched by side panel. | Persists with chat | Claude's `/mnt/user-data/outputs/` |
| Wiki directory (user-chosen) | **Read-only** from bash | User's personal `.md` knowledge base. Writes only via `text_editor` + N5 pipeline. | N/A (user-managed) | No Claude equivalent |
| `%LOCALAPPDATA%/MySecondBrain/skills/` | **Read-only** | User-added community skills | Survives app updates | Claude's `/mnt/skills/private/` |

### The `present_files` Bridge

Model writes to workspace (scratch zone), calls `present_files(["file"])` → app copies to artifacts directory → surfaced in side panel. Auto-copy: if a path passed to `present_files` is outside the artifacts directory, it's copied there automatically.

### Workspace Isolation (bash)

All `bash` commands execute in the workspace directory. Absolute paths outside workspace blocked pre-execution. Wiki read-only from bash. Workspace cleaned up every 24h on app startup.

---

## 3. The Complete Tool Set (10 Tools)

Tools match Anthropic's trained-in schemas where possible. Models (Claude, GPT-4, Gemini) already know how to use these from training.

### File Tools (consolidated into `text_editor`)

MySecondBrain uses Anthropic's `text_editor_20250728` schema — a single tool with four commands, replacing Claude.ai's separate `view`/`create_file`/`str_replace` tools:

```javascript
// ── VIEW ──
text_editor({
  command: "view",
  path: "/path/to/file-or-directory"
})

// ── CREATE (fails if path exists) ──
text_editor({
  command: "create",
  path: "filename.ext",
  file_text: "...full content..."
})

// ── EDIT (exact match required) ──
text_editor({
  command: "str_replace",
  path: "filename.ext",
  old_str: "exact text — must appear exactly once",
  new_str: "replacement"   // omit to delete
})

// ── APPEND ──
text_editor({
  command: "insert",
  path: "filename.ext",
  new_str: "text to append"
})
```

**Critical rules:**
- `old_str` must match byte-for-byte including whitespace, and appear exactly once
- Always `view` before `str_replace`
- After any `str_replace`, all prior `view` output is stale — MUST re-view
- `create` fails if path exists → forces `str_replace` for updates
- Omitting `new_str` in `str_replace` deletes the matched text

### Shell Tool

```javascript
bash({
  command: "python script.py"
})
```

Runs in workspace-isolated `%LOCALAPPDATA%/MySecondBrain/workspace/`. cmd.exe on Windows, with Git Bash/WSL fallback for `.sh` scripts. Writes outside workspace require user confirmation.

### Web Search Tools

```javascript
// Text search — for information, facts, research
web_search({
  query: "fusion energy breakthroughs 2025"
})

// Image search — for pictures, photos, visual references
image_search({
  query: "modern architecture glass buildings"
})

// Fetch — reads a full page. URL MUST be from prior search results
web_fetch({
  url: "https://iter.org/fusion-outlook-2025"
})
```

**`web_search` vs `image_search`:** The model decides which to use based on user intent. "Tell me about modern architecture" → `web_search`. "Show me pictures of modern architecture" → `image_search`. They are complementary, not overlapping.

**`web_fetch` URL constraint:** The model must have seen the URL in prior `web_search` results or previous `web_fetch` responses. Cannot construct URLs from memory. This prevents hallucinated URLs and wasted fetch calls.

**Backend:** Google Custom Search API or Bing Web Search API (configurable in Settings). User brings own API key.

### Domain-Specific Tools

```javascript
// Wiki search — queries local FTS5 index, zero API cost
wiki_search({
  query: "kubernetes deployment strategies"
})

// Memory — SQLite-backed, Anthropic memory_20250818 schema
memory({
  action: "store",    // or "retrieve"
  key: "user_primary_language",
  value: "TypeScript"  // for store
})

// Skill loader — activates skill instructions
skill_load({
  skill: "xlsx"
})

// Structured confirmation — for dangerous operations
ask_user_input({
  question: "Allow bash to write outside workspace?",
  options: ["Approve", "Deny"]
})

// Present files — surface workspace files as artifacts
present_files({
  filepaths: ["budget.xlsx", "chart.html"]
})
```

---

## 4. Skills — How They Work

### What a Skill Is

A skill is a Markdown instruction document (`SKILL.md`) that encodes domain-specific procedural knowledge. It tells the model HOW to use existing tools — not a new tool, not executable code, not new capabilities.

**Analogy:** Tools are the model's hands; skills are the manual for a specific job.

### Skill Activation (`skill_load` vs Claude's `view`)

Claude.ai activates skills by calling `view("/mnt/skills/public/xlsx/SKILL.md")` — the same `view` tool used for any file read. MySecondBrain uses a dedicated `skill_load` tool with enum constraint. Why the difference:

| | Claude.ai | MySecondBrain |
|---|---|---|
| Activation | `view(SKILL.md path)` | `skill_load("xlsx")` |
| Why | Skills are on a filesystem the model can access | Skills are embedded resources + user directories — not on model's filesystem |
| Validation | Filename must exist | `enum` constraint prevents hallucinated skill names |
| Context protection | None | `<skill_content>` XML wrapping for pruning protection |
| Deduplication | None | App tracks activations, skips re-injection |

### Progressive Disclosure (Three Tiers)

| Tier | What's loaded | When | Token cost |
|------|--------------|------|------------|
| 1. Catalog | `name` + `description` | Session start (system prompt) | ~80 tokens each |
| 2. Instructions | Full `SKILL.md` body | When model calls `skill_load` | <5000 tokens |
| 3. Resources | `scripts/`, `references/`, `assets/` | When instructions reference them | Varies |

11 skills = ~880 token catalog upfront, not 11 full instruction sets.

### Built-in Skills (11 Anthropic Skills)

Shipped as embedded resources in `MySecondBrain.UI.dll`:

**Document:** xlsx, docx, pdf, pptx
**Creative:** algorithmic-art, canvas-design, frontend-design, theme-factory
**Dev & Meta:** web-artifacts-builder, webapp-testing, skill-creator

### Skill Discovery

Four locations scanned at startup:
1. **Embedded** `Skills/anthropic/` — 11 built-in, updated with app
2. **User** `%LOCALAPPDATA%/MySecondBrain/skills/` — community skills, survives updates
3. **Cross-client** `%USERPROFILE%/.agents/skills/` — from Claude Code, Cursor, etc.
4. **Cross-client** `%USERPROFILE%/.claude/skills/` — pragmatic Claude Code compatibility

Name collisions: user overrides built-in, cross-client overrides user.

---

## 5. Deep Research — Skill-Based Protocol

Deep Research is a skill, not a custom state machine. The model follows a research protocol encoded in `SKILL.md` instructions.

**Flow:**
1. User asks for research → Model matches Deep Research skill description → calls `skill_load("deep-research")`
2. Model follows protocol: plan → `web_search` (multiple queries) → `web_fetch` (promising sources) → synthesize
3. Model writes report via `text_editor.create` in workspace
4. Model calls `present_files(["report.md"])` → report appears in WebView2 side panel with citations

**Progress:** Visible naturally as tool calls stream in chat — no custom progress UI needed. "Searching for: fusion energy breakthroughs 2025" appears as a `web_search` tool call. "Reading: iter.org" appears as a `web_fetch` tool call.

**Citation format:** Inline `[1]`, `[2]` markers in Markdown, with a `## Sources` footnote section at the bottom. `CitationRenderer` (WPF) renders clickable superscript links.

---

## 6. Versioning — Model vs. App

**The model does:** write a file (new or patched). Nothing else.

**The app does:** stores every file write per turn, diffs content, builds the version selector, handles navigation.

| User says | Model does | App records |
|-----------|-----------|-------------|
| "Add item 4" | `text_editor.str_replace` same path | New version of same artifact |
| "Rewrite completely" | `bash` overwrite + `present_files` | New version of same artifact |
| "Call it v4" | `text_editor.create` new path + `present_files` | New independent artifact |
| "Go back to v1" | Nothing | Client UI navigation only |
| Edits a prior message | Nothing (sees different branch) | Separate artifact history |

The model has no version counter, no artifact ID, no cross-turn internal state.

---

## 7. Memory Tool (SQLite)

Separate from the wiki. Wiki = user-authored knowledge. Memory = AI-extracted discrete facts.

- **Storage:** SQLite with Anthropic `memory_20250818` schema — key/value pairs with source chat and timestamp
- **Per-chat toggle:** "🧠 Mem" in toolbar. OFF = memory tool removed from tools array
- **Persistence:** Memories survive across all chats, not per-chat
- **Management:** Settings → Memory → view, edit, delete individual entries. "Clear All Memories."

No `_memory.md` wiki file — the original N12 approach was replaced by SQLite memory tool for cleaner separation of concerns.

---

## 8. The Artifact Lifecycle (Within a Chat)

```
User: "Create a React dashboard for sales data"
  → Model: skill_load("web-artifacts-builder")
  → Model: bash("bash scripts/init-artifact.sh sales-dashboard")
  → Model: text_editor.str_replace (develops React components)
  → Model: bash("bash scripts/bundle-artifact.sh")
  → Model: present_files(["bundle.html"])
  → App: copies bundle.html to artifacts directory → renders in WebView2 panel
  → User sees: interactive React dashboard in side panel (v1)

User: "Add a revenue chart"
  → Model: text_editor.str_replace (adds chart component)
  → Model: bash("bash scripts/bundle-artifact.sh")
  → Model: present_files(["bundle.html"])
  → App: detects same filename → creates v2 → shows diff (v1→v2)

User: "Export as standalone file"
  → User clicks "Save to Disk" → system save dialog → file written
  → OR: "Save to Wiki" → launches N5 Write to Wiki pipeline
```

---

## 9. System Prompt Construction

Additively assembled per chat. Disabled items are removed entirely — not hidden.

```
System prompt =
    [persona.system_message]           ← only if non-empty
    [behavioral_instructions]          ← always
    [date_time_context]                ← always
    [platform_context]                 ← always (Windows, cmd.exe, workspace path)
    [<available_skills> block]         ← only if ≥1 skill enabled
    [skill_usage_instructions]         ← only if ≥1 skill enabled

Tools array =
    [bash]                             ← only if enabled
    [text_editor]                      ← only if enabled
    [web_search]                       ← only if enabled
    [web_fetch]                        ← only if enabled
    [image_search]                     ← only if enabled
    [wiki_search]                      ← only if enabled
    [memory]                           ← only if enabled
    [ask_user_input]                   ← always (confirmations)
    [present_files]                    ← only if enabled
    [skill_load]                       ← only if ≥1 skill enabled
```

**Edge case:** Empty persona + everything disabled = no system prompt, empty tools array. Model operates as plain chat.

---

## 10. What MySecondBrain Does NOT Have (vs Claude.ai)

| Claude.ai Feature | MySecondBrain Approach | Why |
|-------------------|----------------------|-----|
| Inline widgets (`read_me`/`show_widget`) | Not replicated | WPF ContentBlockRenderers handle inline code/images. Complex visuals → file artifact instead |
| MCP tools | Not in scope | Single-user desktop app. No remote MCP server integration planned for initial release |
| Publish/Share/Remix | Not applicable | Single-user desktop app. Save to Disk/Wiki instead |
| `window.storage` / sandbox restrictions | Not needed | WebView2 renders local files — no remote execution risk |
| AI-powered artifacts (embedded API calls) | Not supported | Artifacts are static files, not mini-apps with API access |

---

## 11. Key Design Decisions Summary

| Decision | Our Approach | Reason |
|----------|-------------|--------|
| Artifact identity | Filename within chat | Model naturally handles context by filename |
| Versioning | Client-side, per file-write event | Model has zero version state |
| File tools | `text_editor` (consolidated) | Anthropic's trained-in `text_editor_20250728` schema |
| Skill activation | `skill_load` tool with enum | Embedded resources not on model's filesystem; enum prevents hallucination |
| Workspace model | Two-zone (workspace + artifacts) | Clean separation of working vs deliverable |
| `present_files` | Explicit bridge | Model signals intent; temp files stay hidden |
| Rendering | WebView2 (artifacts) + WPF (chat) | Best of both: browser-native for artifacts, native WPF for conversation |
| Memory | SQLite, separate from wiki | Wiki = user knowledge, Memory = AI facts |
| Deep Research | Skill, not state machine | Zero custom code; model follows protocol naturally |
| Tools count | 10 (including image_search) | Image search is a distinct tool with different API, safety, and use criteria |
| Pre-edit reads | Mandatory re-view after str_replace | Prevents byte-mismatch failures from stale context |
| `create` fails if exists | Yes | Forces intentional updates via `str_replace` |

---

*Developer reference — created 2026-06-24. See [`vision/vision-summary.md`](../vision/vision-summary.md) for the product specification. See [`skills-integration.md`](skills-integration.md) for detailed skills subsystem design. See [`abstractions.md`](abstractions.md) for C# interface contracts.*
