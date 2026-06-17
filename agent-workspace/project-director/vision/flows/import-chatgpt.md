# Import from ChatGPT/Claude — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — is migrating from ChatGPT and/or Claude to MySecondBrain. Has existing conversation history on those platforms that they want to bring into their new unified AI hub. The imported chats should be preserved with their original structure, timestamps, and message roles.

## Goal
Import chat history exported from ChatGPT or Claude into MySecondBrain as permanent ChatThreads, so all past AI conversations are searchable and accessible from a single interface.

## Starting Point
The user has already exported their chat history from ChatGPT or Claude:
- **ChatGPT:** Settings → Data Controls → Export Data → generates a ZIP containing `conversations.json`
- **Claude:** Settings → Export Data → generates a JSON file with conversation history

The user has extracted the JSON file(s) and knows their location on disk.

---

## Happy Path (Import from ChatGPT)

### Step 1: Trigger Import
**Trigger options:**
- **During onboarding:** On the Finish screen ([`screens/onboarding-wizard.html`](../screens/onboarding-wizard.html) Screen 5), user clicks **"Import from ChatGPT or Claude"**
- **From Studio:** File menu → Import, OR Settings → Import
- **From Settings:** [`screens/settings.html`](../screens/settings.html) → Import section

### Step 2: File Selection
A file picker dialog opens, filtered for JSON files. The user navigates to their exported file and selects it. Multiple files can be selected (for importing multiple exports at once).

### Step 3: Parse and Validate
The app reads the selected file and attempts to parse it:

**For ChatGPT exports:**
- Validates the top-level structure matches ChatGPT's export format
- Extracts each conversation: title, messages (with roles: user/assistant), timestamps, model used (if available in export)

**For Claude exports:**
- Validates the structure matches Claude's export format
- Extracts each conversation: title, messages (with roles: user/assistant), timestamps

**If the file cannot be parsed:** Error: "Could not parse file. Unsupported format or corrupted file." Return to file selection.

**If the file contains no recognizable chats:** Error: "The selected file contains no recognizable chat data." Return to file selection.

### Step 4: Preview
A preview dialog displays for each importable chat found in the file:

**Preview content (scrollable list):**
- Chat title (e.g., "Python async/await explanation")
- Message count (e.g., "12 messages")
- Date range (e.g., "Mar 15, 2025 — Mar 17, 2025")
- First message preview (first ~100 characters)
- Source platform icon (ChatGPT or Claude)

**Summary at top:**
- "[N] chats found in [filename]"
- Total messages across all chats

**Actions:**
- Checkboxes to select/deselect individual chats (all selected by default)
- "Select All" / "Deselect All" links
- **[Import [N] Chats]** button
- **[Cancel]** button

### Step 5: Duplicate Detection
Before importing, the app checks for duplicates:

For each selected chat, the app checks if a ChatThread already exists with the **same title AND same first message content** (first 200 characters).

If duplicates are found, a dialog appears:
- "⚠️ [N] chats appear to already exist in MySecondBrain:"
- List of duplicate chat titles
- Options: "Skip duplicates (import [M] new chats)" / "Import anyway (create duplicates)" / "Cancel"

The user selects "Skip duplicates" to import only new chats.

### Step 6: Import Execution
The app creates ChatThreads for each imported chat:

1. ChatThread created with IsTransient=false (permanent)
2. ChatThread title = original conversation title (or auto-generated from first message if no title)
3. All messages imported with original roles (user/assistant), content, and timestamps
4. Messages preserve their original order
5. Imported chats are NOT assigned a Persona — the Persona field is blank/null (these predate MySecondBrain's Persona system). User can assign a Persona later via right-click → Edit.
6. Imported chats use a default Model Configuration placeholder (e.g., "Imported") — they cannot be used to generate new AI responses until the user assigns a valid Persona/Model Configuration.

**Progress:** For large imports (50+ chats), a progress bar shows: "Importing [X] of [N] chats..."

### Step 7: Completion
On completion:
- Toast: "✓ Imported [N] chats: '[title1]', '[title2]', and [M] others."
- If during onboarding: navigates to Studio with imported chats visible in the sidebar
- If from Studio/Settings: the sidebar refreshes to show imported chats
- Imported chats appear in the chat list grouped by their original dates
- The user can now: search imported chats (L3), star them (L2), tag them (L7), organize into folders (L9), view message history, copy messages — everything EXCEPT generate new AI responses (until Persona assigned)

---

## Alternative Paths

### Path B: Import with Persona Assignment
After import, the user can assign a Persona to any imported chat:

1. Right-click the chat in sidebar → "Edit" → select Persona from dropdown
2. OR: open the chat → click the Persona area in the header → select from dropdown
3. Once a Persona is assigned, the chat becomes fully functional — the user can send new messages and get AI responses
4. The imported messages retain their original labels; new messages use the assigned Persona

### Path C: Import During Onboarding
From the onboarding Finish screen, the user clicks "Import from ChatGPT or Claude":

1. Same import flow as above (Steps 2-6)
2. After import completes, user sees the imported chats count on the Finish screen summary
3. User clicks "Launch Studio" → Studio opens with imported chats in the sidebar AND a new chat tab ready
4. The onboarding is complete

### Path D: Import from Both Platforms
The user has exports from both ChatGPT and Claude:

1. User runs import flow twice (once per file)
2. OR selects both files in Step 2 (if file picker supports multi-select)
3. Each file is processed sequentially
4. Preview shows all chats from all files, with source platform icons
5. All imported chats appear in the sidebar

### Path E: Re-Import (Same File Twice)
The user accidentally imports the same export file twice:

1. Duplicate detection (Step 5) catches all chats as duplicates
2. Dialog shows: "All [N] chats already exist in MySecondBrain. No new chats to import."
3. User can click "Import anyway" to create duplicates, or "Cancel"

---

## Failure Points

| Failure | Handling |
|---------|----------|
| File cannot be parsed | "Could not parse file. Unsupported format or corrupted file." Return to file selection. |
| File is not JSON | "The selected file is not a valid JSON file. Please select a ChatGPT or Claude export file." |
| File contains no recognizable chats | "The selected file contains no recognizable chat data." Return to file selection. |
| File is too large (>100MB) | "The selected file is too large ([N]MB). Maximum import size is 100MB." |
| Individual chat has too many messages (>10,000) | Chat is truncated: "⚠️ Chat '[title]' has [N] messages — only the first 10,000 were imported." |
| Database error during import | "Import failed: [error]. No chats were imported." (transactional — partial imports are rolled back) |
| Disk space runs out during import | "Import failed: insufficient disk space. [N] of [M] chats were imported. Free up space and retry." |

---

## Edge Cases

1. **ChatGPT export contains system messages:** ChatGPT's export may include hidden "system" messages (e.g., custom GPT instructions). These are imported as system messages with a "System" label, visible in the conversation but distinct from user/assistant messages.

2. **ChatGPT export contains images/files:** ChatGPT conversations may reference uploaded images or generated images. These are noted as placeholders in the imported messages: "[Image: filename.png — not available for import]". The actual image files are not included in ChatGPT's JSON export.

3. **Claude export contains artifacts:** Claude's export may include artifact references. These are imported as message content with an artifact marker: "[📄 Artifact: name — content imported below]" with the artifact content appended.

4. **Chat with extremely long title:** Titles are truncated to 200 characters in the sidebar display. Full title preserved in ChatThread data.

5. **Timestamps in non-standard formats:** The app parses ISO 8601 and common variants. If a timestamp cannot be parsed, the chat's creation date defaults to the import date with a note: "Original date unknown."

6. **Importing while other chats have active generation:** Import runs independently. Active generations in other tabs are unaffected.

7. **User imports, then immediately deletes the imported chat:** Soft-delete to Trash (U1). Standard 30-day recovery window.

8. **Import during first-launch onboarding with no API keys:** Import works fine — no API calls needed. The imported chats are viewable. New AI responses require API keys to be added later.

---

## Completion
The user's ChatGPT and/or Claude conversation history is now in MySecondBrain. All past conversations are searchable via full-text search (L3), organizable with tags/folders/pins, and viewable with full Markdown rendering. To continue any imported conversation with AI, the user assigns a Persona to the chat.

**The user's primary benefit:** Complete migration from other AI platforms to a single, unified interface. No conversation history is left behind. All past AI interactions are now in one place — searchable, organized, and under the user's control.

---

## Cross-References
- Feature spec: [`features/import-export.md`](../features/import-export.md) I2 — Import Chats
- Feature spec: [`features/chat-organization-search.md`](../features/chat-organization-search.md) L3 — Full-Text Search
- Feature spec: [`features/soft-delete-trash.md`](../features/soft-delete-trash.md) U1 — Soft-Delete
- Feature spec: [`features/studio-chat-workspace.md`](../features/studio-chat-workspace.md) C7 — Chat Titling
- Feature spec: [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O1 — ChatThread Model
- Screen: [`screens/onboarding-wizard.md`](../screens/onboarding-wizard.md) — Finish screen import link
- Screen: [`screens/settings.md`](../screens/settings.md) — Import section
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Destination for imported chats
