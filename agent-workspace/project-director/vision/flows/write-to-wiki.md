# Write to Wiki — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — has just had a valuable AI discussion in the Studio and wants to capture the key insights as a permanent, polished Markdown note in their personal wiki, rather than letting the knowledge stay trapped in chat history.

## Goal
Transform a Studio chat conversation into a polished `.md` wiki file through AI-assisted generation with human review and approval. The user controls what is saved, where it goes, and how it links to existing knowledge.

## Starting Point
The user is in an active Studio chat ([`screens/studio-chat.html`](../screens/studio-chat.html)) with a conversation that contains valuable information worth preserving. The chat may be long (dozens of messages) or short (a focused Q&A). The wiki directory is configured.

---

## Happy Path (Create New Wiki File)

### Step 1: Trigger
**Trigger:** User clicks the **"Write to Wiki"** button in the Studio textbox toolbar, OR right-clicks the chat in the sidebar and selects "Write to Wiki" from the context menu (L12).

Alternatively, from the Artifacts panel: user clicks **"Save to Wiki"** on an artifact (F6) — this launches the same N5 pipeline but uses the artifact content as source material instead of the chat conversation.

### Step 2: Target Selection Dialog
A dialog appears with two options:

**Option A — "Create new wiki file" (default, pre-selected):**
- Folder picker within wiki directory — AI suggests a subfolder based on analysis of `index.md` (N11) directory tree
- Filename input: AI suggests a filename based on the chat topic (e.g., "oauth-vs-oidc.md"). User can edit.
- OK/Cancel

**Option B — "Update existing":**
- Directory tree to select an existing target file
- **Shortcut:** If any wiki files were @ mentioned (N7) in this chat, they appear at the top of the dialog as "Update [filename] (discussed above)" — pre-selected, no navigation needed. This is the most common update path.

The user selects "Create new wiki file," accepts the AI-suggested subfolder and filename, and clicks **OK**.

### Step 3: AI Generation
The AI begins processing:

1. AI reads the full chat conversation (all messages in current branch)
2. AI reads `index.md` (N11) for wiki structure awareness — understands what topics exist, how files are organized
3. AI runs the cross-linking pipeline (N10):
   - Reads `index.md` → identifies candidate files/headings relevant to the new content
   - Requests full content only of selected relevant sections (not entire files)
   - Generates draft with suggested `[text](../path/file.md#heading)` links
4. AI generates a polished `.md` summary of the conversation: well-structured headings, clear prose, code blocks where relevant, key takeaways

**Loading state:** A progress indicator shows: "Analyzing conversation…" → "Reading wiki structure…" → "Generating summary…" → "Finding related content…"

### Step 4: Preview Panel
A Preview Panel appears with the AI-generated content:

**Editable Text Area:** The full AI-generated `.md` content in a large text area. The user can edit, rephrase, add, or remove anything.

**Suggested cross-links** highlighted inline:
- Accepted links (user clicked ✓) appear in green
- Pending links (not yet reviewed) appear in yellow with ✓ (accept) / ✕ (reject) buttons
- Each suggested link shows: the linked file name and heading

**Action buttons:**
- **[Save to Wiki]:** For new files, saves immediately. For updates, opens the mandatory Diff Viewer first (Step 5).
- **[Refine in Chat]:** Opens a new Studio tab with the draft as an artifact for iterative AI discussion. The user can discuss the draft with AI, request changes, then return to save.
- **[Append Only] toggle:** When active, AI content is appended under `## AI Addition — [YYYY-MM-DD]` heading instead of replacing content. For new files, this toggle has no effect (the file IS the new content).
- **[Cancel]:** Discards everything. No file created. Chat unchanged.

The user reviews the content, accepts all suggested cross-links by clicking ✓ on each yellow link, makes minor edits to the text, and clicks **[Save to Wiki]**.

### Step 5: Save
**For new files:**
- File is written to the selected path on disk immediately
- Wiki index updates automatically (N2)
- `index.md` is regenerated (N11)
- Success toast: "✓ Saved to Wiki: oauth-vs-oidc.md" with **[Open in Wiki Browser]** and **[Open in External Editor]** buttons

**For updates (would trigger Diff Viewer):**
- Silent backup snapshot saved before modification (N6)
- Diff Viewer opens: side-by-side comparison — red = removed, green = added
- "Commit to Wiki" button only clickable after user has scrolled through the full diff
- On commit: file updated, index refreshed, `index.md` regenerated

### Step 6: Post-Save — Backlink Suggestions
After saving a new file, a **"Suggested Backlinks"** panel appears:
- "[N] existing files could link to this page."
- List of suggested files with proposed link text
- Actions: **[Apply All]**, **[Apply Selected]**, **[Dismiss]**
- Each approved backlink opens its own Diff Viewer for the existing file being modified
- If Append-Only Mode (N9) is active for a target file, backlinks are appended under a dated heading

The user reviews, selects relevant backlinks, clicks "Apply Selected." Each affected file opens its Diff Viewer for approval. Once all are approved, the flow is complete.

---

## Alternative Paths

### Path B: Update Existing Wiki File
From Step 2, user selects "Update existing" and picks a file (or uses the @ mention shortcut):

1. AI reads the chat conversation + the existing file content
2. AI generates an UPDATED version of the file (not a new summary) that incorporates insights from the chat
3. Preview Panel shows the updated content with changes highlighted
4. **[Save to Wiki]** opens the **mandatory Diff Viewer** (cannot bypass)
5. User scrolls through the full diff, approves changes
6. Backup snapshot saved before modification (N6)
7. After save: backlink suggestions for other files that might reference the updated content

### Path C: Append-Only Mode
From Step 4, user toggles "Append Only" ON:

1. AI content is appended under `## AI Addition — [YYYY-MM-DD]` in the Preview
2. Existing file content is shown above the append marker for context (grayed out, read-only)
3. On save for existing files: Diff Viewer shows only the appended section (no red/removed text)
4. Ideal for: journals, logs, preserving the user's own words while adding AI insights
5. Toggle state remembered per chat session

### Path D: Refine in Chat
From Step 4, user clicks "Refine in Chat":

1. A new Studio tab opens
2. The draft appears as an artifact (F1) in the Artifacts panel
3. A system message in the chat: "The following wiki draft has been loaded as an artifact. Discuss changes with me and I'll update it."
4. User discusses the draft with AI: "Add a section about token exchange," "Make the intro more concise"
5. AI updates the artifact with each response
6. User can click "Save to Wiki" on the artifact (F6) to return to the N5 pipeline
7. Or user can copy-paste final content manually

### Path E: Save Artifact to Wiki
From the Artifacts panel (F6), user clicks "Save to Wiki" on an artifact:

1. Launches the same N5 pipeline
2. Target Selection dialog pre-filled with the artifact name as suggested filename
3. AI generation step uses the artifact content as source (not the chat conversation)
4. AI can still cross-link by reading `index.md` (N11)
5. All other steps (Preview, Diff, Backlinks) remain the same

### Path F: Trigger from Sidebar Context Menu
From the Studio sidebar, user right-clicks a chat → "Write to Wiki" (L12):

1. Same N5 pipeline
2. Target selection dialog appears immediately
3. The entire chat is the source material
4. Identical to the toolbar button path

---

## Failure Points

| Failure | Handling |
|---------|----------|
| Wiki directory not configured | "No wiki directory configured. Set up in Settings → Wiki." with link to Settings. |
| Wiki directory not writable | "Cannot write to wiki directory. Check folder permissions." |
| Target file locked (open in another program) | "Cannot save: [filename].md is open in another program. Close it and try again." |
| AI call fails during generation | Error message: "AI generation failed: [details]." with **[Retry]** and **[Cancel]**. |
| Disk full | "Cannot save file. Disk is full. Free up space and try again." |
| Filename collision | "A file named [filename].md already exists. Overwrite or choose a different name?" |
| Filename invalid characters | "Filename contains invalid characters. Use letters, numbers, hyphens, and underscores." |

---

## Edge Cases

1. **Very long chat (100+ messages):** AI reads the full conversation. If token limits are exceeded, the context overflow strategy (B8) of the active Model Configuration applies. The Preview notes: "⚠️ Conversation truncated due to context limits. [N] oldest messages were summarized."

2. **Chat has multiple branches:** Only the current branch is used as source. A note in the Preview: "Based on current branch (v2 of 3). Switch branches to use different conversation history."

3. **Write to Wiki from an incognito/temporary chat (C30):** Works identically. The chat remains temporary; only the wiki file is permanent.

4. **User writes to wiki, then immediately deletes the chat:** The wiki file is independent — it persists on disk. Deleting the chat does not affect the wiki file.

5. **Append-Only on a new file:** Has no meaningful effect — the "append" IS the file content. Toggle is disabled for new files.

6. **Cross-linking suggests a file that was deleted mid-flow:** The suggestion is removed from the Preview. If the user had already accepted it, the link becomes a broken link — Wiki Browser will show it in the "Broken Links" section (if implemented) or the file simply won't navigate.

7. **User edits the Preview heavily, removing most AI content:** The edited version is what's saved. AI generation is a starting point, not the final word.

8. **Multiple rapid "Write to Wiki" actions on the same chat:** Each creates a separate file (or updates the same file multiple times). N6 snapshots are created for each update.

9. **Wiki file created via Write to Wiki, then edited externally, then updated again via Write to Wiki:** The N5 update reads the current file content (including external edits) and generates an update incorporating both the external edits and the new chat insights. The Diff Viewer shows all changes.

---

## Completion
**New file ending:** A new `.md` file exists in the user's wiki directory. The wiki index is updated. Backlinks have been applied to relevant existing files. The user can open the file in Wiki Browser or an external editor. The knowledge from the chat conversation is now a permanent, searchable, cross-linked part of the user's second brain.

**Update ending:** An existing `.md` file has been enhanced with insights from the chat. The previous version is preserved as a snapshot (N6). Backlinks to the updated file have been suggested and applied.

**The user's primary benefit:** The "discuss then confirm" model eliminates the friction of manually copying AI output into note files. AI does the heavy lifting of summarization and cross-linking; the human reviews and approves. Every meaningful conversation has a path to becoming permanent knowledge.

---

## Cross-References
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N5 — Write to Wiki core workflow
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N6 — Automatic Wiki Versioning
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N7 — @ Mentions
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N9 — Append-Only Mode
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N10 — AI Cross-Linking
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N11 — Auto-Generated index.md
- Feature spec: [`features/artifacts-side-panel.md`](../features/artifacts-side-panel.md) F6 — Save Artifact to Wiki
- Feature spec: [`features/chat-organization-search.md`](../features/chat-organization-search.md) L12 — Right-click context menu
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Source of Write to Wiki trigger
- Screen: [`screens/wiki-browser.md`](../screens/wiki-browser.md) — Destination for viewing saved files
