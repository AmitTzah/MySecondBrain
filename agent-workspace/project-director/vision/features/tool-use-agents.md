# Tool Use (Agent Capabilities) — Feature Spec

## What the User Accomplishes

The AI can act as an agent using 10 tools matching Anthropic's trained-in schemas where possible. The model autonomously searches the web (text and images), fetches web pages, executes shell commands in a workspace-isolated environment, creates/edits files, manages persistent memory, loads skill instructions, queries the personal wiki, presents deliverable files as artifacts, and requests structured user confirmations. The user controls which tools are enabled globally and per-chat — disabled tools are completely removed from the API call, not hidden.

## Trigger

- AI requests tool use during conversation
- User explicitly asks AI to use a tool ("Search the web for...", "Create an Excel file...")
- User loads a skill that triggers tool usage (W)
- User enables/disables tools in Settings → Tools or per-chat toolbar

## Detailed Behavior

### H1. bash

Anthropic `bash_20250124` schema. Model executes shell commands in a workspace-isolated environment.

- **Execution:** `cmd.exe` on Windows. `.sh` scripts use Git Bash (`C:\Program Files\Git\bin\bash.exe`) or WSL (`wsl bash -c`) fallback. Heredocs redirected to `text_editor` tool instead.
- **Workspace:** All commands run in `%LOCALAPPDATA%/MySecondBrain/workspace/`. Working directory locked to workspace before execution. Absolute paths outside workspace detected and blocked pre-execution.
- **Safety:** Writes outside workspace require explicit user confirmation via `ask_user_input` (H8). Wiki directory is read-only from bash. Workspace cleaned up periodically — files older than 24h removed on app startup.
- **bash availability:** Detected at startup. Communicated to model via system prompt: "You are running on Windows. Shell commands use cmd.exe. Python, pip, npm work as expected. .sh scripts require Git Bash or WSL."
- **Skills integration:** Skills' bundled scripts (Python, Node.js) run via bash. Model follows skill instructions, writes code, executes it, verifies output.

### H2. text_editor

Anthropic `text_editor_20250728` schema. Replaces the original separate `file_generate` and `file_edit` tools.

**Commands:**

| Command | Purpose | Safety |
|---------|---------|--------|
| `view` | Read file or directory contents | Regular (read-only) |
| `create` | Create a new file with content. **FAILS if path already exists** — forces use of `str_replace` for updates, preventing accidental overwrites. | Low-Medium |
| `str_replace` | Replace text in an existing file. `old_str` must match exactly (byte-for-byte) and appear exactly once. Omit `new_str` to delete matched text. | Low-Medium (explicit for delete) |
| `insert` | Append text to end of file | Low |

**Critical rules:**
- `old_str` must be unique and match byte-for-byte including whitespace
- Always `view` the file immediately before `str_replace` — stale context causes byte-mismatch failures
- After any `str_replace`, all prior `view` output for that file is stale — MUST re-view before the next edit. The model cannot trust cached view output after modifying a file.
- `create` fails if path exists — prevents accidental overwrites, forces intentional updates
- The app tracks every file write within a chat by filename for automatic version history (F3)

**File paths:** `text_editor` can write to the workspace or directly to the artifacts directory. Files written to artifacts directory are auto-surfaced. Files written to workspace for intermediate work stay hidden until `present_files` (H9) is called.

### H3. web_search

Anthropic server schema reimplemented as client tool with identical interface. Model autonomously searches the web for information.

- **Backend:** Google Custom Search API or Bing Web Search API (configurable in Settings). User brings own API key.
- **Results:** Title, URL, snippet, display URL returned for each result. Max results configurable per call.
- **Cost:** API call cost billed to user's search API key (not AI provider). Zero AI token cost for the search itself.
- **Visibility:** Search query and results displayed as tool-call system messages in chat.
- **Usage:** Model autonomously decides when to search. Used by Deep Research skill (W9) for multi-source research.

### H4. web_fetch

Read-only HttpClient GET fetcher. Model fetches URL content for deeper reading.

- **Usage:** Model fetches promising sources discovered via `web_search`. Also used for general web page reading.
- **URL Constraint:** The model MUST have seen the URL in prior context — either from `web_search` results or previous `web_fetch` responses. The model cannot construct URLs from memory or guess URLs. This prevents hallucinated URLs and wasted fetch calls.
- **Output:** Page content (text extracted, HTML stripped). Limited to reasonable size (truncated if >100KB).
- **Cost:** Zero AI token cost for the fetch itself. Page content consumes context tokens when fed back to model.
- **Visibility:** URL and truncated content shown as tool-call system messages.

### H5. memory

Anthropic `memory_20250818` schema wrapping a SQLite-backed memory store. **Separate from the wiki** — wiki is user-authored knowledge, memory is AI-extracted discrete facts about the user.

- **Storage:** SQLite entries: key (fact identifier), value (fact content), source chat ID, timestamp.
- **Store:** Model calls memory tool to persist facts: "User prefers TypeScript over JavaScript."
- **Retrieve:** Model calls memory tool with relevance-based query: "What do I know about this user's preferences?"
- **Persistence:** Memories survive across all chats. Not per-chat.
- **Per-Chat Toggle:** "🧠 Mem" in textbox toolbar. OFF = memory tool removed from tools array.
- **User Management:** View, edit, delete memories in Settings → Memory (A13). "Clear All Memories" with confirmation.
- **No `_memory.md`:** The original `_memory.md` wiki file approach (former N12) has been replaced by the SQLite memory tool. This is a cleaner separation — the wiki is for user-authored knowledge, memory is for AI-extracted facts.

### H6. wiki_search

Queries the local SQLite FTS5 wiki index to find relevant `.md` files.

- **Query:** Model sends search query → app queries FTS5 wiki index → returns matching filenames, headings, and content snippets.
- **Cost:** Zero API cost — purely local FTS5 query.
- **Visibility:** Query and results shown as tool-call system messages.
- **Usage:** Model autonomously decides to search wiki when it needs the user's personal knowledge. Also triggered by user: "What did I write about X?"
- **Restrictions:** Read-only. AI cannot modify wiki files through this tool (N8).

### H7. skill_load

Activates an Agent Skill by loading its full `SKILL.md` instructions into context. Full spec: [`features/agent-skills.md`](agent-skills.md) §W3.

- **Deduplication:** If skill already activated in current session, re-injection skipped.
- **Wrapping:** Skill content returned in `<skill_content>` XML tags with resource listing.
- **Enum constraint:** Tool schema includes `enum` of valid skill names — prevents hallucination of non-existent skills.
- **Availability:** Tool only registered if ≥1 skill enabled.

### H8. ask_user_input

Structured WPF confirmation dialogs instead of prose-based confirmation prompts. Pattern from claude.ai consumer experience.

- **Usage:** Model calls this tool when it needs user confirmation for dangerous operations (bash writes outside workspace, text_editor deletes, file overwrites).
- **Dialog types:** Confirm/Cancel, Multiple choice selection, Text input.
- **Result:** User's choice returned to model as tool result.
- **Always available:** This tool is always in the tools array regardless of other tool toggles — confirmations are non-optional for dangerous operations.

### H9. present_files

Model signals "these workspace files are done — surface them as artifacts in the side panel."

- **Schema:**
```json
{
  "name": "present_files",
  "description": "Present files to the user as artifacts in the side panel. Use after writing deliverable files. The first file in the array is shown first.",
  "parameters": {
    "type": "object",
    "properties": {
      "filepaths": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Array of file paths to present. Files are copied from workspace to artifacts directory. First path shown first in side panel."
      }
    },
    "required": ["filepaths"]
  }
}
```

- **Auto-copy:** If a path is not already in the artifacts directory, the app copies it there automatically before surfacing.
- **Order:** First path in array is shown first in the side panel.
- **Version tracking:** If a file with the same name was previously presented in the same chat, the app creates a new version (F3) rather than a new artifact.
- **Multiple files:** `present_files` accepts an array — multiple files can be presented in one call.
- **Visibility:** Tool call shown as system message: "📄 Presented: budget.xlsx, chart.html"

### H10. image_search

Google Image Search or Bing Image Search API. Separate from `web_search` — dedicated to finding images rather than web pages. Model uses this when the user asks for images, photos, diagrams, or visual references.

- **Backend:** Google Custom Search API (with `searchType=image`) or Bing Image Search API. User brings own API key (same key as web_search).
- **Results:** Thumbnail URL, full image URL, dimensions, source page URL, title/alt text for each result.
- **Usage Criteria:** Model uses `image_search` when the user explicitly asks for images ("find me pictures of...", "show me what X looks like"), needs visual references, or wants to include images in a response. NOT used for general web information queries — those use `web_search`.
- **Safety:** Image results are filtered for safe search by default (configurable in Settings). Model must not use `image_search` for generating or finding inappropriate content.
- **Cost:** API call cost billed to user's search API key. Typically higher cost per query than `web_search` due to richer result data.
- **Visibility:** Query and thumbnail results displayed as tool-call system messages. Retrieved images can be displayed inline in chat or referenced by URL.
- **Relationship to web_search:** `image_search` is for finding images. `web_search` is for finding text/web pages. They are complementary — the model decides which to use based on the user's intent. If the user says "show me pictures of modern architecture," model uses `image_search`. If the user says "tell me about modern architecture," model uses `web_search`.

### H11. Tool Auto-Approval

- **Global Defaults:** Settings → Tools. Configure which tools auto-execute:
  - bash: Auto-Approve / Ask / Disabled
  - text_editor: Auto-Approve / Ask / Disabled
  - web_search: Auto-Approve / Ask / Disabled
  - web_fetch: Auto-Approve / Ask / Disabled
  - image_search: Auto-Approve / Ask / Disabled
  - memory: Auto-Approve / Ask / Disabled
  - wiki_search: Auto-Approve / Ask / Disabled
  - present_files: Auto-Approve / Ask / Disabled
  - skill_load: Auto-Approve / Ask / Disabled
- **Hard-Coded Overrides (cannot be auto-approved):**
  - bash writes outside workspace → ALWAYS ask
  - text_editor deletes (omit new_str in str_replace) → ALWAYS ask
- **Per-Chat Override:** Textbox toolbar "🔧 Tools ▼" dropdown. Overrides global defaults for current chat.
- **Auto-Approve Indicator:** When any tool is set to Auto-Approve, subtle indicator in chat: "Tools: Auto" vs "Tools: Ask"

## Data

- Tool call records stored with Messages (tool name, parameters, result)
- Memory entries stored in SQLite: [`data/memory-entry.md`](../data/memory-entry.md)
- Workspace files are temporary (24h cleanup) unless presented via `present_files`

## Success/Failure States

- **bash execution failed:** "Command failed with exit code [N]. Output: [stderr]"
- **bash blocked (outside workspace):** "Cannot access path outside workspace: [path]. Use text_editor to save files to user-chosen destinations."
- **text_editor create failed (file exists):** "File already exists. Use str_replace to modify or create with a different name."
- **text_editor str_replace failed (no match):** "old_str not found in file. The file may have changed — use view to re-read it."
- **web_search failed:** "Web search failed: [error message]. Check your search API key in Settings."
- **web_fetch failed:** "Could not fetch [URL]: [HTTP status / error]"
- **memory store failed:** "Could not store memory: [error]"
- **present_files failed:** "Could not present [path]: file not found in workspace."
- **skill_load failed:** "Skill '[name]' is not available."
- **Tool denied by user:** "Tool execution was denied by the user."

## Permissions

- bash writes outside workspace + text_editor deletes ALWAYS require confirmation — hard-coded rules (H10 override)
- All other tools configurable via global defaults + per-chat overrides (H10)
- Wiki directory is read-only from bash; file tools restricted by N8

## Interactions

- H1 (bash) uses P9 (bash on Windows adaptation), P10 (workspace isolation)
- H2 (text_editor) creates artifacts → F1 (workspace-to-artifact pipeline)
- H5 (memory) stores in SQLite → W8 (Memory Tool management in Settings)
- H6 (wiki_search) queries N2 (wiki index)
- H7 (skill_load) activates W3 (skill instructions)
- H9 (present_files) triggers F1 (artifact surfacing), F3 (version tracking)
- W9 (Deep Research skill) uses H3 + H4 + H1 + H9
- Tools restricted by N8 (wiki access restrictions)
