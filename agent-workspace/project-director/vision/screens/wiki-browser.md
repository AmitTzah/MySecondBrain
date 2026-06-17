# Wiki Browser — Screen Specification

## Purpose

The Wiki Browser is the primary interface for browsing and navigating the user's personal wiki — a directory of `.md` files on disk. It provides a three-region split layout: a collapsible file tree on the left, a rendered Markdown viewer in the center, and an info panel with related sections, backlinks, and file metadata on the right.

## Layout

Three-region resizable split layout within the Studio frame:
- **Left:** Studio app navigation sidebar (same as studio-chat)
- **Center-Left:** File tree (collapsible directory tree of wiki folder)
- **Center:** Markdown viewer (rendered .md content)
- **Right:** Info panel with three tabs: Related Sections, Backlinks, File Info

The Studio sidebar (Chats, Wiki, Media, Artifacts, Usage, Settings) is visible. The Wiki nav item is active. The right panel (Artifacts + Chat Nav) from studio-chat is hidden — replaced by the wiki's own info panel.

## Regions

### Region 1: Studio Sidebar (Leftmost)
Same as [`studio-chat.md`](studio-chat.md) Region 1. 📝 Wiki nav item active.

### Region 2: File Tree (Center-Left)
**Width:** Resizable, default ~240px. Collapsible via toggle.

- Directory tree of the wiki folder.
- Folders expandable/collapsible with triangle indicators.
- `.md` files shown with 📄 icon. Non-.md files hidden by default (toggleable: "Show all files").
- Active file highlighted.
- Right-click context menu: Open in External Editor, Version History (N6), Delete, Rename.
- Files with unsaved external changes show a dot indicator (●).
- Search bar at top: "Filter files..." — filters the tree to matching filenames.
- Empty state: "No .md files found in wiki directory."

### Region 3: Markdown Viewer (Center)
- Renders the selected `.md` file with full Markdown formatting: headings (H1-H6), bold, italic, code blocks with syntax highlighting, lists (ordered/unordered), links, tables, blockquotes, images.
- **Internal wiki links** (e.g., `[other file](other-file.md)`) are clickable and navigate within the Wiki Browser to that file.
- **External links** open in the default system browser.
- **"Open in External Editor" button** in header — launches the file in the system default `.md` editor.
- **"💬 Discuss with AI" button** in header — creates a new chat (or opens existing) with the current file's full content pre-loaded as context. The file is @ mentioned so the AI has full awareness. This enables a "wiki → chat → Write to Wiki" loop: browse wiki, discuss a file with AI, refine it, then save back via Write to Wiki (N5).
- **File name** displayed as the title at the top.
- Empty state (no file selected): "Select a file from the tree to view its content."

### Region 4: Info Panel (Right)
**Width:** Resizable, default ~280px. Three tabs:

**Tab 1: Related Sections**
- Uses local wiki index (N2) to find sections in other files sharing keywords with the current file.
- Zero API cost — purely local computation.
- Each entry: filename, heading, relevance snippet.
- Click to open that file in the viewer, scrolled to the matching heading.
- Empty state: "No related sections found."

**Tab 2: Backlinks**
- Shows which other wiki files contain links TO the current file.
- Detected by scanning for `[text](current-file.md#...)` patterns across the local index.
- Each entry: source filename, context snippet showing the link.
- Click to open the source file.
- Empty state: "No backlinks to this file."

**Tab 3: File Info**
- Word count, character count, creation date, last modified date, estimated reading time, number of headings, number of cross-links in/out.
- Word count and reading time update in real-time.
- Version history link: "View [N] snapshots" → opens version history view (N6).

## Data Displayed

- Wiki files from disk (`.md` files in configured wiki directory)
- Index data from [`data/wiki-file.md`](../data/wiki-file.md)
- Version snapshots from [`data/wiki-version-snapshot.md`](../data/wiki-version-snapshot.md)

## Actions

| Action | Trigger | Behavior |
|--------|---------|----------|
| Open file | Click file in tree | Renders file in Markdown viewer. Updates info panel. |
| Filter files | Type in filter bar | Filters tree to matching filenames. |
| Collapse/Expand tree | Toggle button | Hides or shows file tree region. |
| Open in External Editor | Button in viewer header | Launches file in system default .md editor. |
| Navigate internal link | Click wiki link in rendered content | Opens linked file in Wiki Browser. |
| Navigate external link | Click external link | Opens in default browser. |
| Copy link to file | Right-click → Copy Link | Copies `[filename](filename.md)` to clipboard. |
| View version history | Right-click → Version History, or File Info tab link | Opens version history/snapshot view (N6). |
| Delete file | Right-click → Delete | Confirmation dialog. Moves file to system recycle bin. |
| Rename file | Right-click → Rename | Inline rename. Updates cross-links if possible. |
| Switch info tab | Click tab | Switches between Related Sections, Backlinks, File Info. |

## Empty States

| Context | State |
|---------|-------|
| No files in wiki | "No .md files found in wiki directory. Create one via Write to Wiki or add .md files to your wiki folder." |
| No file selected | "Select a file from the tree to view its content." |
| File has no content | Renders empty document. |
| No related sections | "No related sections found for this file." |
| No backlinks | "No backlinks to this file. Other files can link to it using [text](filename.md)." |
| Filter no results | "No files match '[filter]'." |

## Loading States

| Context | State |
|---------|-------|
| Loading file | Skeleton placeholder in viewer area. |
| Indexing wiki | "Indexing wiki..." progress bar in file tree header (startup or after re-index). |

## Error States

| Context | State |
|---------|-------|
| File not found | "This file has been moved or deleted." with "Remove from tree" option. |
| File read error | "Could not read file. Check permissions." |
| Wiki directory not configured | "No wiki directory configured. Set up in Settings → Wiki." with link to settings. |

## Navigation

**Entry Points:**
- Studio sidebar → 📝 Wiki
- Clicking wiki links from chat messages or artifacts

**Exit Points:**
- Studio sidebar → any nav item (💬 Chats, 🖼️ Media, etc.)

## Cross-References

- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N4
- Wiki indexing: N2
- Wiki search: N3
- Wiki versioning: N6
- Studio chat: [`screens/studio-chat.md`](studio-chat.md)
