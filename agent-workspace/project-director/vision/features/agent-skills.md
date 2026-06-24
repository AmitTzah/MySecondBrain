# Agent Skills — Feature Spec

## What the User Accomplishes

The user gets access to 11 built-in domain-specific AI capabilities (document creation, creative work, web development, testing) plus community skills — all without the model needing custom tool implementations. Skills are Markdown instruction files that tell the model HOW to use its existing tools to produce professional output. The user can add community skills, create their own, and control which skills are active per chat.

## Trigger

- Skill catalog injected into system prompt at chat start (if ≥1 skill enabled)
- Model autonomously calls `skill_load("skill-name")` when a task matches a skill's description
- User explicitly asks for skill-specific output ("Create an Excel budget...", "Build me a React dashboard...")
- Settings → Skills: user enables/disables skills globally
- Per-chat toolbar: user toggles individual skills for current chat

## Detailed Behavior

### W1. Built-in Skill Set (11 Anthropic Skills)

All 11 skills are included verbatim from [`github.com/anthropics/skills`](https://github.com/anthropics/skills), shipped as embedded resources in `MySecondBrain.UI.dll`. Updated with app updates.

**Document Skills:**

| Skill | What It Does | Tools Used | Key Dependencies |
|-------|-------------|------------|-----------------|
| **xlsx** | Create/edit Excel spreadsheets with formulas, formatting, charts | bash, text_editor | Python + openpyxl + LibreOffice |
| **docx** | Create/edit Word documents with TOC, headers, tracked changes | bash, text_editor | Python + pandoc + LibreOffice |
| **pdf** | Extract, split, merge, fill forms, OCR, create PDFs | bash, text_editor | Python (pypdf) + qpdf + LibreOffice |
| **pptx** | Create/edit PowerPoint decks with layouts, charts, speaker notes | bash, text_editor | Python (python-pptx) + LibreOffice |

**Creative Skills:**

| Skill | What It Does | Tools Used | Key Dependencies |
|-------|-------------|------------|-----------------|
| **algorithmic-art** | p5.js generative art (flow fields, particles, seeded randomness) | bash, text_editor | Node.js + p5.js |
| **canvas-design** | Create posters/designs as PNG/PDF with bundled fonts | bash | Python |
| **frontend-design** | Design guidance: typography, color, avoiding "template look" | text_editor | None (knowledge only) |
| **theme-factory** | 10 pre-set color/font themes to apply to any artifact | text_editor | None (knowledge only) |

**Development & Meta:**

| Skill | What It Does | Tools Used | Key Dependencies |
|-------|-------------|------------|-----------------|
| **web-artifacts-builder** | Create React/Tailwind/shadcn/ui HTML artifacts, bundle to single file | bash, text_editor | Node.js + React + Vite + Parcel |
| **webapp-testing** | Test web applications using Playwright (screenshots, DOM inspection) | bash | Python + Playwright + Chromium |
| **skill-creator** | Create, evaluate, and improve skills with benchmarking | bash, text_editor | Python + Claude CLI |

### W2. Progressive Disclosure

Skills use three-tier loading to keep the base context small:

| Tier | What's Loaded | When | Token Cost |
|------|--------------|------|------------|
| **1. Catalog** | `name` + `description` | Session start (system prompt) | ~80 tokens per skill |
| **2. Instructions** | Full `SKILL.md` body (minus YAML frontmatter) | When model calls `skill_load("name")` | <5000 tokens (recommended) |
| **3. Resources** | `scripts/`, `references/`, `assets/` | When instructions reference them | Varies |

11 installed skills = ~880 tokens for the catalog upfront, not 11 full instruction sets. This is critical for context efficiency.

**Skill catalog format in system prompt:**

```xml
<available_skills>
  <skill>
    <name>xlsx</name>
    <description>Create/edit Excel spreadsheets with formulas, formatting, and charts. Use when user mentions .xlsx, Excel, spreadsheet, or wants tabular data as a file.</description>
  </skill>
  <!-- ...one <skill> block per enabled skill -->
</available_skills>

When a task matches a skill's description, call the skill_load tool with the skill's name
to load its full instructions. The skill's instructions override general guidance — follow them.
```

### W3. skill_load Tool

The model activates a skill by calling the `skill_load` tool. The tool schema constrains the model to only valid skill names via an `enum` — this prevents hallucination of non-existent skills.

**Tool schema:**

```json
{
  "name": "skill_load",
  "description": "Load a skill's full instructions when a task needs specialized domain knowledge. Skills are listed in the <available_skills> section of the system prompt.",
  "parameters": {
    "type": "object",
    "properties": {
      "skill": {
        "type": "string",
        "enum": ["xlsx", "docx", "pdf", "pptx", "algorithmic-art", "canvas-design", "frontend-design", "theme-factory", "web-artifacts-builder", "webapp-testing", "skill-creator"],
        "description": "Name of the skill to load"
      }
    },
    "required": ["skill"]
  }
}
```

The `enum` is dynamically generated from enabled skills. If no skills are enabled, the `skill_load` tool is not registered in the API tools array.

**Structured response wrapping:**

When the model calls `skill_load("xlsx")`, the app returns:

```xml
<skill_content name="xlsx">
# XLSX creation, editing, and analysis

## Overview
A user may ask you to create, edit, or analyze the contents of an .xlsx file...

[full SKILL.md body, YAML frontmatter stripped]

<skill_resources>
  <file>scripts/recalc.py</file>
  <file>scripts/office/unpack.py</file>
  <file>scripts/office/pack.py</file>
  <file>scripts/office/soffice.py</file>
  <file>scripts/office/validate.py</file>
</skill_resources>
</skill_content>
```

Benefits of structured wrapping:
- Model can clearly distinguish skill instructions from conversation content
- App can identify skill content during context compaction (protect from pruning — W10)
- Bundled resources are surfaced without being eagerly loaded
- Model resolves relative paths against the skill's base directory

**Deduplication:** Track which skills have been activated in the current session. If the model attempts to load a skill already in context, skip re-injection to avoid duplicate instructions. Activations reset on new chat creation.

### W4. Skill Discovery

The skill loader scans four locations at startup:

| Location | Path | Scope | Purpose |
|----------|------|-------|---------|
| Embedded | `Skills/anthropic/` in `MySecondBrain.UI.dll` | Built-in | 11 Anthropic skills, updated with app |
| User | `%LOCALAPPDATA%/MySecondBrain/skills/` | User | User-created or downloaded community skills. Survives updates. |
| Cross-client (agents) | `%USERPROFILE%/.agents/skills/` | Cross-client | From other compliant tools (Claude Code, Cursor) |
| Cross-client (claude) | `%USERPROFILE%/.claude/skills/` | Cross-client | Pragmatic Claude Code compatibility |

**Scanning rules:** Within each directory, look for subdirectories containing a file named exactly `SKILL.md`. Skip `.git/`, `node_modules/`. Max depth 6 levels, max 2000 directories.

**Name collisions:** User-level skills override built-in. Cross-client overrides user. Within the same scope, first-found wins. Log a warning when a collision occurs.

**Parsing:** Extract `name` and `description` from YAML frontmatter. Body is the Markdown after the closing `---`. Lenient validation:
- `name` doesn't match directory → warn, load anyway
- `description` missing → skip skill (essential for disclosure)
- YAML unparseable → skip skill, log error

### W5. Community Skills

Users add skills from community repositories (e.g., [`alirezarezvani/claude-skills`](https://github.com/alirezarezvani/claude-skills)) by copying to `%LOCALAPPDATA%/MySecondBrain/skills/`.

Community skills are:
- Discovered alongside built-in skills at startup
- Listed in the catalog with a `source: community` annotation
- Never overwritten by app updates
- Take priority over built-in skills with the same name

The `skill-creator` meta-skill enables users to create and benchmark their own skills directly within the app.

### W6. Per-Chat Skills Toggle

The textbox toolbar includes a "📚 Skills ▼" dropdown:

- Checkbox for each discovered skill (built-in + community)
- "All on" / "All off" quick actions at top
- Disabled skills are completely removed from:
  - The `<available_skills>` catalog in the system prompt
  - The `skill_load` tool's `enum` parameter
- If no skills enabled: no catalog block, no `skill_load` tool
- New chats inherit global defaults from Settings → Skills (A12)
- Per-chat overrides are temporary for that chat session

### W7. System Prompt Construction

The system prompt is assembled additively per chat. Disabled items are removed entirely — not hidden or flagged.

```
System prompt =
    [persona.system_message]           ← only if non-empty
    [behavioral_instructions]          ← always
    [date_time_context]                ← always
    [platform_context]                 ← always (Windows, cmd.exe, workspace path)
    [<available_skills> block]         ← only if ≥1 skill enabled
    [skill_usage_instructions]         ← only if ≥1 skill enabled
```

**Tools array** (separate from system prompt):
```
    [bash schema]                      ← only if enabled
    [text_editor schema]               ← only if enabled
    [web_search schema]                ← only if enabled
    [web_fetch schema]                 ← only if enabled
    [wiki_search schema]               ← only if enabled
    [memory schema]                    ← only if enabled
    [ask_user_input schema]            ← always (needed for confirmations)
    [present_files schema]             ← only if enabled
    [skill_load schema]                ← only if ≥1 skill enabled
```

**Edge case — empty persona + everything disabled:** System prompt not sent. Tools array empty. Model operates as plain chat with no capabilities.

### W8. Memory Tool

The `memory` tool wraps a SQLite-backed memory store using Anthropic's `memory_20250818` schema. It is separate from the wiki (wiki = user-authored knowledge, memory = AI-extracted facts).

**Storage:**
- Discrete fact entries in SQLite (not a flat `_memory.md` file)
- Each entry: key (fact identifier), value (fact content), source chat ID, timestamp
- Retrieval is relevance-based — the model requests memories related to the current topic
- User can view, edit, and delete individual memories in Settings → Memory (A13)
- "Clear All Memories" with confirmation dialog
- Memory storage size displayed in Settings

**Per-Chat Toggle:**
- Textbox toolbar: "🧠 Mem" toggle. Default: Off (inherits from global default in Settings).
- When ON: `memory` tool is in the tools array. Model can store and retrieve facts.
- When OFF: `memory` tool removed from tools array entirely.
- Global default in Settings → Memory configures the inherited state for new chats.

**Behavior:**
- Model autonomously decides when to store facts (no "Update Memory" button)
- Model calls `memory` tool to store: "User prefers TypeScript over JavaScript", "User's project is called MySecondBrain"
- Model calls `memory` tool to retrieve: "What do I know about this user's preferences?"
- Tool results are fed back into the conversation context like any other tool result
- Memories persist across all chats (not per-chat)

### W9. Deep Research as Skill

Deep Research is now a skill rather than a custom state machine. The model follows a research protocol encoded in a skill's `SKILL.md` instructions, using the standard tool surface.

**How it works:**
1. User says "Do deep research on fusion energy progress in 2025"
2. Model loads the Deep Research skill: `skill_load("deep-research")` (if available as built-in or community skill)
3. Skill instructions guide the model through: formulate research questions → search for sources (`web_search`) → read promising sources (`web_fetch`) → synthesize findings → produce structured report with citations
4. Progress is visible naturally as tool calls stream in chat — no custom progress UI needed
5. "Searching for: fusion energy breakthroughs 2025" (web_search tool call appears)
6. "Reading: iter.org — Fusion Energy Outlook 2025" (web_fetch tool call appears)
7. Final report appears as an artifact (model writes report to workspace → calls `present_files`)

**No custom state machine:** The skill-based approach eliminates the need for custom progress displays ("Searching...", "Reading 3 of 8 sources...", "Synthesizing..."). The model's natural tool-call streaming provides equivalent visibility with zero custom implementation.

**Cancellation:** User can cancel at any point by clicking Stop. Partial findings preserved in conversation. If the report was partially written via `text_editor`, the partial file remains in workspace.

### W10. Skill Context Protection

Skill content is tagged with `<skill_content>` wrappers. If the app performs context compaction (summarizing older messages when the context window fills), skill content blocks are exempt from pruning. Losing skill instructions mid-conversation silently degrades the model's behavior — it forgets the domain rules.

### W11. Dependency Detection

Skills declare dependencies (Python packages, system tools, Node.js). The model checks availability at runtime:

1. Model reads SKILL.md → sees dependency requirements
2. Model attempts to use the required tool: `bash("python -c 'import openpyxl'")` 
3. If dependency missing: model guides user to install it: "The xlsx skill needs openpyxl. Run: `pip install openpyxl`"
4. None of these dependencies are bundled with the app — they are the user's responsibility

**System-level tools required by document skills:**

| Tool | Used By | Install |
|------|---------|---------|
| Python 3 + openpyxl + pandas | xlsx | `pip install openpyxl pandas` |
| Python 3 + python-docx | docx | `pip install python-docx` |
| Python 3 + pypdf, pdfplumber | pdf | `pip install pypdf pdfplumber` |
| Python 3 + python-pptx | pptx | `pip install python-pptx` |
| Node.js + React/Vite/Parcel | web-artifacts-builder | `npm install` (per project) |
| Node.js + p5.js | algorithmic-art | `npm install p5` |
| Python + Playwright + Chromium | webapp-testing | `pip install playwright && playwright install chromium` |
| LibreOffice | xlsx, docx, pdf, pptx | System install (for recalculation, conversion) |
| pandoc | docx | System install |
| qpdf | pdf | System install |

## Data

- Skill metadata (name, description, source, path) stored in memory at startup — not persisted to SQLite (re-discovered each launch)
- Memory entries stored in SQLite: [`data/memory-entry.md`](../data/memory-entry.md)
- Skill activation state tracked per session (in-memory only, reset on new chat)

## Success/Failure States

- **Skill Loaded:** Skill instructions injected into context. Model follows domain rules.
- **Skill Already Active:** Deduplication — re-injection skipped. Model proceeds with existing context.
- **Skill Not Found:** "Skill 'xyz' is not available. Check Settings → Skills for enabled skills."
- **Missing Dependency:** Model detects and guides: "This skill needs openpyxl. Run: pip install openpyxl"
- **No Skills Enabled:** Catalog not in system prompt. `skill_load` tool not in tools array.
- **Community Skill Invalid:** Skipped at startup with log warning. "⚠️ Skill 'xyz' at [path] has invalid SKILL.md: [reason]"

## Permissions

- Single-user app. All skills available to the sole user.
- Skill execution uses existing tools (`bash`, `text_editor`) — permissions governed by those tools' auto-approval settings (H11).
- Skills cannot escalate privileges — they're instructions, not code.

## Interactions

- W1-W5 interact with H7 (skill_load tool)
- W6 interacts with K2 (textbox toolbar), A12 (Settings → Skills defaults)
- W7 interacts with E1 (standard chat mode — system prompt assembly)
- W8 interacts with H5 (memory tool) — memory is listed in both W (skill spec) and H (tool spec)
- W9 interacts with H3 (web_search), H4 (web_fetch), H1 (bash), F1 (present_files → artifacts)
- W10 interacts with B8 (context overflow strategy — skill blocks protected from pruning)
- W11 depends on user-installed system tools (Python, Node.js, LibreOffice — none bundled)
