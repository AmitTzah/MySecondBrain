# Reference Implementation: Obsidian — Personal Wiki System

## Source
**Product:** Obsidian (v1.7+, Electron-based, proprietary but extensively documented)  
**Repository:** https://obsidian.md (closed source; plugin API is open)  
**Component studied:** File tree, Markdown rendering, backlinks, cross-linking, graph view, indexing, file-system-based wiki  

## What It Does
Obsidian is a personal knowledge base built on a directory of plain Markdown files. It provides: file tree navigation, rendered Markdown with wiki-style `[[internal links]]`, backlinks panel (files linking to current file), graph view (visual cross-link map), full-text search, and a rich plugin ecosystem.

## Architecture (Relevant to MySecondBrain)

### File Tree & Wiki Browser
- Collapsible directory tree of the vault (wiki folder).
- Files displayed with .md extension hidden (configurable).
- Click to open; right-click for context menu (rename, delete, open in external editor).
- **MySecondBrain insight:** MSB's Wiki Browser (N4) three-region layout closely follows Obsidian's pattern but adds: Related Sections tab (keyword-based, zero API cost), File Info tab (word count, reading time), and a "Discuss with AI" button that bridges wiki → chat → Write to Wiki. Obsidian's file tree is the gold standard for UX.

### Backlinks
- Backlinks panel shows files that contain `[[link to current file]]` references.
- Updated in real-time as files are edited.
- Shows context snippet around each link.
- **MySecondBrain insight:** Obsidian uses `[[wikilinks]]` syntax by default. MSB uses standard Markdown `[text](file.md#heading)` links (more compatible with external editors). The backlink detection is the same concept: scan all files for references to the current file. MSB's approach is simpler (standard Markdown links) but loses Obsidian's unlinked-mentions feature.

### Cross-Linking & Graph View
- Obsidian's graph view shows nodes (files) and edges (links between them). Interactive: click to navigate, zoom/pan.
- Auto-suggests links as you type `[[`.
- **MySecondBrain insight:** Obsidian's graph view is a deferred feature in MSB (T6 — Wiki Graph View). MSB's auto-generated `index.md` (N11) serves a similar purpose but in Markdown form rather than visual graph. The AI cross-linking pipeline (N10) is MSB's unique differentiator: AI reads `index.md`, selects candidate files, generates suggested links, user reviews. Obsidian has no AI-driven cross-linking.

### Full-Text Search
- Obsidian indexes all vault files for fast full-text search.
- Search operators: path:, tag:, line:, section:, etc.
- Results show filename + matching snippet with highlights.
- **MySecondBrain insight:** MSB uses SQLite FTS5 for both chat message search and wiki content search. Obsidian's search is more feature-rich (operators, regex). MSB's search scope is broader (chat messages + wiki files + artifacts). MSB can add search operators incrementally.

### Indexing Architecture
- Obsidian maintains a local cache/index of all vault files.
- Index updates on file change (debounced).
- Powers: search, backlinks, graph view, outgoing links, tag pane.
- **MySecondBrain insight:** MSB's indexing architecture (N2) mirrors Obsidian's: full scan on startup, incremental updates on file system watcher events, store in SQLite. The key difference: MSB also indexes heading hierarchy and content for AI tool use (H7 Wiki Search Tool) and auto-generated `index.md` (N11).

### Version History
- Obsidian provides a "File Recovery" plugin (snapshots of edits).
- Core plugin; manual snapshots.
- **MySecondBrain insight:** MSB's N6 (automatic snapshots before modification, max 30 per file, 50MB cap) is more structured. Git integration (onboarding Step 3, Flag #13) provides more robust version history than Obsidian's simple snapshots. The combination of git + N6 snapshots may be redundant — Architect should evaluate consolidation (Flag #13).

## Key Takeaways for MySecondBrain

| Concept | Obsidian Approach | MySecondBrain Adaptation |
|---------|------------------|-------------------------|
| Wiki format | Plain .md files on disk | Same — plain .md, no proprietary format |
| Internal links | `[[wikilinks]]` syntax | Standard Markdown `[text](file.md#heading)` |
| Backlinks | Real-time panel, context snippets | Same, powered by SQLite cross-link index |
| Graph view | Interactive visual graph | Deferred (T6). `index.md` serves as text-based catalog |
| Indexing | Cache on startup, incremental on change | Same pattern, SQLite FTS5 |
| Cross-linking | Manual `[[links]]` | AI-driven pipeline: AI suggests, human reviews (N10) |
| Version history | Plugin-based snapshots | Structured snapshots (N6) + git integration |
| Related sections | Not built-in (community plugins) | Built-in: keyword-based from local index, zero API cost |
| AI features | Community plugins (not core) | Core feature: Write to Wiki, AI cross-linking, AI memory, @ mentions |

## Licensing
Proprietary (closed source). Studied for UX patterns and architectural concepts only. Obsidian's approach to plain-.md-file wikis validates MySecondBrain's core wiki design.

## Risk Notes
- Obsidian's `[[wikilinks]]` syntax is proprietary (though widely adopted). MSB's decision to use standard Markdown links avoids lock-in and ensures external editor compatibility.
- Obsidian faces performance issues with 10,000+ file vaults during startup indexing. MSB faces the same challenge (edge case: "Wiki directory contains 10,000+ .md files"). Mitigated by incremental indexing and SQLite.
- Obsidian's graph view rendering (T6 deferred feature) uses Canvas/web technologies — MSB will need a WPF-native graph layout library (e.g., GraphShape, Microsoft Automatic Graph Layout) when implementing T6.
