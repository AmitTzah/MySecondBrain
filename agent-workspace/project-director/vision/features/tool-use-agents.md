# Tool Use (Agent Capabilities) — Feature Spec

## What the User Accomplishes

The AI can act as an agent using 14 provider-agnostic tools. The tool schemas use Anthropic-flavored naming conventions (well-designed and broadly recognized) but are designed for instruction-following by ANY model — OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, and OpenAI-compatible providers. The user controls which tools are enabled globally and per-chat — disabled tools are completely removed from the API call, not hidden.

## Trigger

- AI requests tool use during conversation
- User explicitly asks AI to use a tool ("Search the web for...", "Read the config file...", "Create an Excel file...")
- User loads a skill that triggers tool usage (W)
- User enables/disables tools in Settings → Tools or per-chat toolbar
- Model sends multiple `tool_use` blocks in a single response → parallel execution (H15)

## Tool Overview — 14 Tools

| # | Tool | Category | Description |
|---|------|----------|-------------|
| 1 | `read_file` | File Operations | Read any file on the filesystem with offset/limit. Auto-approved in workspace/artifacts/wiki. |
| 2 | `list_files` | File Operations | List directory contents with recursive option. Structured JSON output. |
| 3 | `search_files` | File Operations | Regex search across files with file pattern filter. Returns matches with line/context. |
| 4 | `apply_diff` | File Operations | Surgical search/replace edits using SEARCH/REPLACE blocks. Workspace + artifacts only. |
| 5 | `write_to_file` | File Operations | Create or overwrite a file. Workspace + artifacts only. Fails if path exists without overwrite flag. |
| 6 | `bash` | System | Execute shell commands in workspace-isolated environment. Anthropic bash_20250124 schema. |
| 7 | `web_search` | Web | Google/Bing text search. Model autonomously searches the web. |
| 8 | `web_fetch` | Web | Read-only HTTP GET. URL must come from prior search results. |
| 9 | `image_search` | Web | Google/Bing image search. Separate from web_search — dedicated to finding images. |
| 10 | `wiki_search` | Knowledge | Local SQLite FTS5 wiki index query. Read-only. Zero API cost. |
| 11 | `memory` | Knowledge | SQLite-backed key-value memory store. Anthropic memory_20250818 schema. |
| 12 | `skill_load` | Agent | Load Agent Skill instructions. Enum-constrained schema. Deduplicated per session. |
| 13 | `present_files` | Agent | Surface workspace files as artifacts in WebView2 side panel. |
| 14 | `ask_user_input` | Safety | Structured WPF confirmation dialogs. Always available — cannot be disabled. |

## Key Architectural Changes from Previous Vision

- `text_editor` (Anthropic text_editor_20250728: view/create/str_replace/insert) is REPLACED by `read_file` + `apply_diff` + `write_to_file` + `list_files` + `search_files`
- Tool count: 10 → 14
- Schemas are provider-agnostic (Roo Code pattern), not Claude-specific
- `view` command → superseded by `read_file`
- `create` command → superseded by `write_to_file`
- `str_replace` and `insert` commands → superseded by `apply_diff`

## Detailed Behavior

### File Operations (5 Tools)

These five tools replace the single `text_editor` tool. Together they cover all file reading, searching, listing, editing, and creation operations.

---

### H1. read_file

Read any file on the filesystem. Parameters: `path` (required), `offset` (optional, 1-based line offset), `limit` (optional, max lines to return).

- **Workspace/artifacts/wiki paths:** Auto-approved — content returned immediately.
- **Outside workspace:** Triggers approval gate via `ask_user_input`: "Model is trying to read file outside workspace: [path]. Approve?" with Configure/Approve Once/Deny.
- **Binary files:** Detected and returned as: "[Binary file: filename (size)]" — content not readable.
- **Blocked paths:** `C:\Windows\`, `C:\Program Files\`, `C:\Program Files (x86)\`, `.env` files, registry hives. Returns: "Access denied: [path] is a protected system location." Cannot be overridden.
- **Out-of-workspace access:** Configurable per-tool in Settings → Tools: Auto-Approve / Ask (default) / Disabled.
- **Large files:** If file exceeds reasonable size for context, the response includes a warning: "⚠️ File '[filename]' is [size]. Showing first [N] lines. Use offset/limit to read specific sections."

### H2. list_files

List directory contents. Parameters: `path` (required), `recursive` (optional, default `false`).

- **Same approval model as read_file** for out-of-workspace paths.
- **Returns structured JSON:**
```json
{
  "files": [
    {"name": "report.md", "size": 1234, "modified": "2026-06-25T10:30:00Z"},
    {"name": "script.py", "size": 5678, "modified": "2026-06-24T15:00:00Z"}
  ],
  "directories": ["subdir1", "subdir2"]
}
```
- Workspace/artifacts/wiki: auto-approved.
- Outside workspace: same approval model as read_file. Blocked paths always denied.

### H3. search_files

Regex search across files. Parameters: `path` (required — directory to search recursively), `regex` (required — Rust regex syntax), `file_pattern` (optional — glob, e.g., `"*.ts"` for TypeScript files only).

- **Same approval model as read_file** for out-of-workspace paths.
- **Returns structured matches:**
```json
{
  "matches": [
    {"file": "src/app.ts", "line": 42, "content": "  const result = await fetchData();"},
    {"file": "src/utils.ts", "line": 15, "content": "  export async function fetchData() {"}
  ],
  "total_matches": 2,
  "files_searched": 5
}
```
- Workspace/artifacts/wiki: auto-approved.
- Outside workspace: same approval model. Blocked paths always denied.

### H4. apply_diff

Surgical search/replace edits on files. Parameters: `path` (required), `diff` (required — one or more SEARCH/REPLACE blocks).

- **Scope:** Workspace + artifacts (per-chat `artifacts/{chat-id}/`) only. Outside these is ALWAYS blocked.
- **Format:** Follows the SEARCH/REPLACE block pattern:
```
<<<<<<< SEARCH
:start_line:[line_number]
-------
[exact content to find]
=======
[new content to replace with]
>>>>>>> REPLACE
```
- `old_str` must match exactly (byte-for-byte) and appear exactly once. Multiple SEARCH/REPLACE blocks can be included for multi-location edits.
- **Failure — no match:** "old_str not found in file. The file may have changed — use read_file to re-read it."
- **Failure — multiple matches:** "old_str found [N] times in file. It must be unique. Use read_file to find a larger unique context string."
- **Safety:** Always re-read the file after applying a diff — stale context causes mismatches.

### H5. write_to_file

Create or overwrite a file. Parameters: `path` (required), `content` (required), `overwrite` (optional, default `false`).

- **Scope:** Workspace + artifacts (per-chat `artifacts/{chat-id}/`) only. Outside these is ALWAYS blocked.
- **If `overwrite=false` and path exists:** FAILS with "File already exists at [path]. Set overwrite=true to overwrite, or use a different filename."
- **If `overwrite=true` and path exists:** Overwrites the file.
- **Auto-creates parent directories** if they don't exist.
- **Safety:** The `overwrite` flag prevents accidental data loss. The model must explicitly opt into overwriting.

---

### H6. bash

Anthropic `bash_20250124` schema. Model executes shell commands in a workspace-isolated environment.

- **Execution:** `cmd.exe` on Windows. `.sh` scripts use Git Bash (`C:\Program Files\Git\bin\bash.exe`) or WSL (`wsl bash -c`) fallback.
- **Workspace:** `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/`. Each chat gets its own sandbox — no conflicts between chats. Working directory locked to the chat's subdirectory.
- **Safety:** Writes outside workspace require explicit user confirmation via `ask_user_input` (H14). This is a hard-coded override — cannot be auto-approved. Wiki directory is read-only from bash.
- **Blocked paths:** `C:\Windows\`, `C:\Program Files\`, `.env` files, registry hives — always denied, cannot be overridden.
- **Workspace cleanup:** When chat deleted/closed: workspace subdirectory queued for deletion (24h grace period). Startup cleanup: delete workspace directories older than 24h.
- **bash availability:** Detected at startup. Communicated to model via system prompt: "You are running on Windows. Shell commands use cmd.exe. Python, pip, npm work as expected. .sh scripts require Git Bash or WSL."
- **Skills integration:** Skills' bundled scripts (Python, Node.js) run via bash. Model follows skill instructions, writes code, executes it, verifies output.

---

### H7. web_search

Google Custom Search or Bing Web Search API. Anthropic server schema reimplemented as client tool with identical interface.

- **Backend:** Configurable in Settings. User brings own API key.
- **Results:** Title, URL, snippet, display URL. Max results configurable per call.
- **Cost:** API call cost billed to user's search API key (not AI provider). Zero AI token cost for the search itself.
- **Visibility:** Search query and results displayed as tool-call system messages in chat.

---

### H8. web_fetch

Read-only HttpClient GET fetcher. URL MUST have been seen in prior context — model cannot construct or guess URLs.

- **Output:** Page content (text extracted, HTML stripped). Truncated if >100KB.
- **Cost:** Zero AI token cost for the fetch itself. Page content consumes context tokens when fed back to model.
- **Visibility:** URL and truncated content shown as tool-call system messages.

---

### H9. image_search

Google Image Search or Bing Image Search API. Separate from `web_search` — dedicated to finding images rather than text.

- **Backend:** Same API key as web_search. Configurable in Settings.
- **Results:** Thumbnail URL, full image URL, dimensions, source page URL, title/alt text.
- **Usage Criteria:** Model uses when user explicitly asks for images ("find me pictures of...", "show me what X looks like"). NOT used for general web information queries — those use `web_search`.
- **Safety:** Safe search filtering by default (configurable in Settings).

---

### H10. wiki_search

Queries the local SQLite FTS5 wiki index to find relevant `.md` files.

- **Query:** Model sends search query → app queries FTS5 wiki index → returns matching filenames, headings, and content snippets.
- **Cost:** Zero API cost — purely local FTS5 query.
- **Restrictions:** Read-only. AI cannot modify wiki files through this tool (N8).

---

### H11. memory

Anthropic `memory_20250818` schema wrapping a SQLite-backed memory store. **Separate from the wiki** — wiki is user-authored knowledge, memory is AI-extracted discrete facts.

- **Storage:** SQLite entries: key (fact identifier), value (fact content), source chat ID, timestamp.
- **Store/Retrieve:** Model autonomously decides when to store or retrieve facts.
- **Persistence:** Memories survive across all chats. Not per-chat.
- **Per-Chat Toggle:** "🧠 Mem" in textbox toolbar. OFF = memory tool removed from tools array.
- **User Management:** View, edit, delete memories in Settings → Memory (A13).

Full spec: [`features/agent-skills.md`](agent-skills.md) §W8.

---

### H12. skill_load

Activates an Agent Skill by loading its full `SKILL.md` instructions into context.

- **Deduplication:** If skill already activated in current session, re-injection skipped.
- **Enum constraint:** Tool schema includes `enum` of valid skill names — prevents hallucination.
- **Availability:** Tool only registered if ≥1 skill enabled.

Full spec: [`features/agent-skills.md`](agent-skills.md) §W3.

---

### H13. present_files

Model signals "these workspace files are done — surface them as artifacts in the side panel."

- **Schema:** `filepaths` array (required). First path shown first.
- **Auto-copy:** Files copied from workspace to artifacts directory.
- **Version tracking:** Same filename within chat = new version (F3).
- **Visibility:** Tool call shown as system message: "📄 Presented: [filename(s)]"

---

### H14. ask_user_input

Structured WPF confirmation dialogs instead of prose-based confirmation prompts.

- **Dialog types:** Confirm/Cancel, Multiple choice selection, Text input.
- **Always available:** This tool is always in the tools array regardless of other tool toggles — confirmations are non-optional for dangerous operations.
- **Used for:** bash writes outside workspace, out-of-workspace file reads (when set to "Ask"), apply_diff/write_to_file outside workspace (blocked — uses ask_user_input to inform, not to approve).

---

## Parallel Tool Execution (H15)

When the model sends multiple `tool_use` blocks in a single assistant response, the app executes all requested tools and returns all results in a single user response — matching the standard Anthropic/OpenAI API pattern.

### Execution Model

- **Independent tools run in parallel:** Tools that don't depend on each other's output (`web_search` + `read_file` on different files, `list_files` + `wiki_search`, etc.) execute concurrently.
- **Dependent tools run sequentially:** The model should not batch calls that depend on each other (e.g., `bash` a command → `read_file` the output). If the model attempts this, tools execute in the order specified by the model. The model is expected to avoid batching dependent calls.
- **Tool orchestrator (IToolOrchestrator):** Handles dispatching tools to appropriate handlers, managing parallel execution, collecting results, and returning them to the model.

### UI Display

Parallel tool execution appears sequentially in the chat as each tool completes:
- When parallel execution begins, an indicator appears: "⚡ Running [N] tools in parallel…"
- As each tool completes, its result card streams in below
- When all tools complete, the indicator updates: "⚡ [N] tools completed in [T]ms"
- The user sees tool calls appear in completion order (which may differ from the model's request order)

### Constraints

- Maximum 10 concurrent tool executions. Additional tools are queued.
- Each tool execution is independent — one failure does not block others.
- The model MUST receive all tool results before generating the next assistant response (standard API contract).

---

## Workspace Isolation Model

- **Per-chat workspace:** `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/`
- Each chat gets its own sandbox subdirectory — no conflicts between chats
- bash, read_file, list_files, search_files, apply_diff, write_to_file are all scoped to the chat's subdirectory by default
- present_files copies from chat workspace → per-chat `artifacts/{chat-id}/` directory
- **Cleanup:** When chat deleted/closed: workspace subdirectory queued for deletion (24h grace period). Startup cleanup deletes workspace directories older than 24h.

---

## Approval Model (Consistent Across All File-Reading Tools)

| Path Scope | read_file / list_files / search_files | apply_diff / write_to_file | bash |
|-----------|--------------------------------------|---------------------------|------|
| **Workspace** | Auto-approved | Auto-approved | Auto-approved (within workspace) |
| **Artifacts directory** | Auto-approved | Auto-approved | Auto-approved (writes: ask) |
| **Wiki directory** | Auto-approved | Blocked (N8) | Read-only |
| **Outside workspace** | Configurable: Auto-Approve / Ask (default) / Disabled | ALWAYS blocked | Writes: ALWAYS ask (hard-coded override) |
| **Blocked paths** (C:\Windows\, C:\Program Files\, .env, registry) | ALWAYS denied | ALWAYS denied | ALWAYS denied |

- **Per-tool configuration:** Settings → Tools. Each read tool (`read_file`, `list_files`, `search_files`) has independent out-of-workspace approval settings.
- **Per-chat override:** Textbox toolbar "🔧 Tools ▼" dropdown with per-tool approval submenus.
- **Blocked paths:** `C:\Windows\`, `C:\Program Files\`, `C:\Program Files (x86)\`, `.env` files, registry hives. Always denied — cannot be overridden by any setting.

---

## System Prompt Construction

- Tools array assembled additively per enabled tools
- Disabled tools completely removed from the API call (not hidden — not sent)
- 14 tools in the full array when everything is enabled
- `ask_user_input` is always present regardless of other settings

---

## Data

- Tool call records stored with Messages (tool name, parameters, result)
- Memory entries stored in SQLite: [`data/memory-entry.md`](../data/memory-entry.md)
- Workspace files are temporary (24h cleanup) unless presented via `present_files`
- Per-chat raw API log: `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/_api_history.json`

---

## Success/Failure States

- **read_file — path outside workspace (Ask mode):** Triggers `ask_user_input` confirmation dialog. User approves → content returned. User denies → "Tool execution was denied by the user."
- **read_file — path outside workspace (Disabled):** "Access denied: reading files outside workspace is disabled for [tool]. Configure in Settings → Tools."
- **read_file — blocked path:** "Access denied: [path] is a protected system location."
- **read_file — binary file:** "[Binary file: filename (size)]" — content not readable.
- **apply_diff — no match:** "old_str not found in file. Use read_file to re-read the file."
- **apply_diff — multiple matches:** "old_str found [N] times. It must be unique."
- **write_to_file — path exists, overwrite=false:** "File already exists. Set overwrite=true or use a different filename."
- **write_to_file / apply_diff — outside workspace:** "Cannot write outside workspace. Files can only be created in the workspace or artifacts directories."
- **bash execution failed:** "Command failed with exit code [N]. Output: [stderr]"
- **bash blocked (outside workspace):** "Cannot access path outside workspace: [path]."
- **bash — .sh requires Git Bash/WSL but neither available:** "This command requires Git Bash or WSL. Install Git for Windows or enable WSL."
- **web_search failed:** "Web search failed: [error]. Check your search API key in Settings."
- **web_fetch failed:** "Could not fetch [URL]: [HTTP status / error]"
- **memory store failed:** "Could not store memory: [error]"
- **present_files failed:** "Could not present [path]: file not found in workspace."
- **skill_load failed:** "Skill '[name]' is not available."
- **Tool denied by user (ask_user_input):** "Tool execution was denied by the user."
- **Parallel execution — one tool fails:** Failed tool shows error. Other tools' results returned normally.

---

## Permissions

- bash writes outside workspace + apply_diff/write_to_file outside workspace + blocked paths: ALWAYS denied or require confirmation — hard-coded rules
- Out-of-workspace read access: configurable per read-tool (Auto-Approve / Ask / Disabled)
- Wiki directory: read-only for all tools (N8)
- Per-chat toolbar overrides available in the Tools dropdown (🔧)

---

## Interactions

- H1-H5 (file operations) replace the former H2 (text_editor)
- H6 (bash) uses P9 (bash on Windows adaptation), P10 (workspace isolation)
- H7-H9 (web tools) share the same search API key
- H10 (wiki_search) queries N2 (wiki index)
- H11 (memory) stores in SQLite → W8 (Memory Tool management in Settings)
- H12 (skill_load) activates W3 (skill instructions)
- H13 (present_files) triggers F1 (artifact surfacing), F3 (version tracking)
- H14 (ask_user_input) is always available — cannot be disabled
- H15 (parallel execution) — IToolOrchestrator dispatches, collects, returns all results
- W9 (Deep Research skill) uses H7 + H8 + H6 + H13
- Tools restricted by N8 (wiki access restrictions)
