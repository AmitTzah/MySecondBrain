# Personal Wiki / Second Brain — Feature Spec

## What the User Accomplishes
The user builds and maintains a personal knowledge base of .md files on their own disk. AI assists by generating polished summaries from chat conversations ("Write to Wiki"), suggesting cross-links between files, and searching the wiki. Every change follows a "discuss then confirm" model — AI proposes, user reviews and approves.

## Trigger
- "Write to Wiki" button in Studio chat textbox toolbar
- "Write to Wiki" option in sidebar chat right-click context menu (L12)
- "Save to Wiki" button on artifacts (F6)
- Wiki Browser navigation from Studio sidebar
- @ mentions in chat textbox (N7)

## Detailed Behavior

### N1. Wiki Directory Configuration
- User selects a directory on disk via folder picker dialog
- App verifies directory exists and is readable/writable
- File system watcher monitors for external changes (files added/edited/deleted outside the app)
- Wiki directory is plain .md files — no database, no proprietary format

### N2. Wiki Indexing
On startup and on file change (detected via file system watcher):
- Scans all .md files in the wiki directory
- Extracts: filename, H1/H2/H3 headings and their text, full file content
- Builds in-memory and/or SQLite full-text search index
- Index updates are incremental when possible (single file change), full re-index on app startup
- Zero API cost — purely local computation

### N3. Wiki Search
- **Access:** Dedicated search scope (separate from chat search L3). Accessed from Studio sidebar or Wiki Browser.
- **Results:** Matching .md filenames, matching headings (with level indicator), content snippets with highlighted terms
- **Click:** Opens file in Wiki Browser (N4), scrolled to matching section
- **Empty State:** "No wiki files found matching [query]"

### N4. Wiki Browser
Three-region split layout:

**Left — File Tree:**
- Collapsible directory tree of the wiki folder
- Clicking a file opens it in the center viewer
- Right-click context menu: Open in External Editor, Version History, Delete, Rename
- Files with unsaved external changes show a dot indicator

**Center — Markdown Viewer:**
- Renders selected .md file with full formatting (headings, bold, italic, code blocks, lists, links, tables)
- "Open in External Editor" button launches file in system default .md editor (e.g., VS Code)
- Rendered links to other wiki files are clickable (navigates within Wiki Browser)
- External links open in default browser

**Right — Info Panel (two tabs):**
1. **Related Sections tab:** Uses local wiki index to find sections in other files sharing keywords. Zero API cost. Shows filename + heading + relevance snippet.
2. **Backlinks tab:** Shows which other wiki files contain links TO the current file. Detected by scanning for `[text](current-file.md#...)` patterns across the local index.

### N5. Write to Wiki — Core Workflow
The primary mechanism for creating and updating wiki content.

**Step 1 — Trigger:**
- "Write to Wiki" button in toolbar OR right-click context menu on chat

**Step 2 — Target Selection Dialog:**
- "Create new wiki file" (default): Folder picker within wiki directory, filename input
- "Update existing": Directory tree to select target file
- OK/Cancel

**Step 3 — AI Generation:**
- AI reads the full chat conversation
- AI reads index.md (N11) for wiki structure awareness
- AI runs cross-linking pipeline (N10) to identify relevant existing sections
- AI generates polished .md summary

**Step 4 — Preview Panel:**
- Editable text area with AI-generated content
- Suggested cross-links highlighted (accepted links in green, pending in yellow)
- Action buttons:
  - **[Save to Wiki]:** For new files: saves immediately. For updates: opens Diff Viewer (mandatory).
  - **[Refine in Chat]:** Opens new Studio tab with draft as an artifact for iterative AI discussion
  - **[Append Only] toggle:** When active, AI content appended under `## AI Addition — [YYYY-MM-DD]` (N9)
  - **[Cancel]:** Discards everything

**Step 5 — Diff Viewer (Updates only):**
- Side-by-side diff: red = removed, green = added
- "Commit to Wiki" button only clickable after user has scrolled through full diff
- Silent backup snapshot saved before modification (N6)

**Step 6 — After Save:**
- New file: success toast with "Open in Wiki Browser" and "Open in External Editor" options
- Update: backlink suggestions panel appears (N10)
- Wiki index updates automatically (N2)
- index.md regenerates (N11)

### N6. Automatic Wiki Versioning
- Every modification via N5 saves previous state as snapshot in SQLite
- **Retention:** Max 30 snapshots per file (oldest auto-deleted when exceeded)
- **Storage Cap:** Total snapshot storage capped at 50MB (oldest across all files deleted when exceeded)
- **Recovery:** Wiki Browser → right-click file → "Version History" → preview any snapshot → "Restore"
- Restoring creates a new snapshot of current state first (restore is undoable)

### N7. @ Mentions for Wiki Files
- In chat textbox, typing `@` opens quick-search dropdown
- Real-time filtering as user types against wiki index (N2)
- Each result shows: filename and H1 title
- **Selecting a file:** Injects full content into chat context
- **Content too large (>~8K tokens):** Injects summarized excerpt (H1 + all H2 headings + first paragraph of each section) with note: "[Full content available in Wiki Browser]"
- AI can also autonomously query wiki via Wiki Search Tool (H7)

### N8. AI Wiki Access Restrictions
Hard-coded least-privilege rules enforced by the app (not by AI prompting):
- **No Deletions:** AI tool-calls to delete wiki files are rejected. Only human can delete.
- **No Renaming:** AI cannot rename wiki files (prevents breaking cross-links). AI can suggest renames.
- **Write-to-Wiki Only:** AI can only write to wiki through explicit N5 workflow. Generic tool use (H3, H4) cannot target wiki directory.

### N9. Append-Only Mode
- Toggle in N5 Preview Panel
- When active: AI cannot modify existing text. New content appended under `## AI Addition — [YYYY-MM-DD]`
- Diff Viewer shows only appended section (no red/removed text)
- Ideal for journals, logs, preserving user's own words
- Toggle state remembered per chat session

### N10. AI Cross-Linking (Forward + Backlinks)
Tiered, cost-efficient pipeline for suggesting section-level cross-links.

**Forward Links (New File → Existing Files):**
1. AI reads index.md (N11) — complete wiki catalog in one read, near-zero token cost
2. AI selects candidate files/headings relevant to new content
3. AI requests full content only of selected relevant sections (not entire files)
4. AI generates draft with suggested `[text](../path/file.md#heading)` links, highlighted in Preview Panel
5. User reviews, accepts, or rejects each link individually before saving
6. AI uses index.md to suggest which subfolder the new file belongs in

**Backlinks (Existing Files → New File):**
- After user saves new file, AI evaluates which existing files could benefit from linking TO it
- "Suggested Backlinks" panel: "{N} existing files could link to this page."
- Actions: [Apply All], [Apply Selected], [Dismiss]
- Each approved backlink opens its own Diff Viewer for that existing file
- If Append-Only Mode (N9) active for target file, backlinks appended under dated heading

### N11. Auto-Generated index.md
Automatically maintained at wiki directory root. Regenerated after every wiki change.

**Structure:**
- Collapsible directory tree showing full wiki folder hierarchy
- Per .md file: filename with link, H1 title, all H2/H3 headings with anchor links, creation date, last modified date, cross-links (links TO and FROM)
- "Recently Modified" section: 10 most recently changed files
- "Orphan Pages" section: files with zero inbound links

**Generation:** Pure local computation from wiki index (N2) + file system metadata. No AI/API calls.

**AI Usage:** During N10 step 1, AI reads index.md instead of querying database index. Gives complete wiki structure, all headings, all existing cross-links in one read. AI uses it to avoid suggesting duplicate cross-links.

## Data
- [`data/wiki-file.md`](data/wiki-file.md), [`data/wiki-version-snapshot.md`](data/wiki-version-snapshot.md)
- Wiki .md files: stored as plain files on disk (not in SQLite)
- Wiki index: stored in SQLite for fast search
- Version snapshots: stored in SQLite

## Success/Failure States
- **Success — New File Saved:** Toast: "Saved to Wiki: [filename].md" with Open/Open Externally buttons
- **Success — File Updated:** Toast: "Updated: [filename].md" with View Diff button
- **Failure — Directory Not Writable:** "Cannot write to wiki directory. Check folder permissions."
- **Failure — File Locked:** "Cannot save: [filename].md is open in another program. Close it and try again."
- **Empty State — No Wiki Dir:** Wiki Browser shows: "No wiki directory configured. Go to Settings to select one."
- **Empty State — Empty Wiki:** "Your wiki is empty. Start a chat and use 'Write to Wiki' to create your first note."

## Permissions
- Single-user app. Wiki directory ownership is the effective permission model.
- AI restrictions (N8) are hard-coded, not permission-based.

## Interactions
- N5 uses K5 (Studio Chat) as source material, F6 (artifact viewer) as alternative source
- N2 powers N3 (wiki search), N4 (related sections/backlinks), N7 (@ mentions), N10 (cross-linking), N11 (index.md)
- N4 uses N2 for file tree and rendering
- N6 stores snapshots; O6 (database compaction) may reclaim space
- N8 restricts H3, H4 (generic tool use) from targeting wiki directory
- N10 reads N11 (index.md); N11 is regenerated after N5 saves
- H7 (Wiki Search Tool) queries N2 index for AI agent use
