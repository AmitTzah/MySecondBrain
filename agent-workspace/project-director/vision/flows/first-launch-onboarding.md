# First-Launch Onboarding — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — a technically proficient professional installing MySecondBrain for the first time, seeking to replace all scattered AI chat platforms with a single Windows-native interface.

## Goal
Complete initial setup of MySecondBrain: connect at least one AI provider, select a starter Persona, configure the personal wiki, review hotkeys, and arrive at the Studio ready to start a conversation.

## Starting Point
The user launches MySecondBrain for the first time. No settings exist — the app detects this and opens the Onboarding Wizard automatically. The Studio window is not yet visible.

---

## Happy Path (Complete All Steps)

### Step 1: Welcome Screen
**Screen:** [`screens/onboarding-wizard.html`](../screens/onboarding-wizard.html) — Screen 0: Welcome

The user sees the app icon, "Welcome to MySecondBrain" heading, and three feature highlight cards (Three-Tier AI, Bring Your Own Keys, Personal Wiki). The user clicks **"Get Started"** → advances to Step 1 (API Keys).

If the user closes the window here (X button), no settings are saved. On next launch, the wizard re-opens at the Welcome screen.

### Step 2: Add API Keys
**Screen:** [`screens/onboarding-wizard.html`](../screens/onboarding-wizard.html) — Screen 1: API Keys (Step 1 of 4)

The user selects a provider from the dropdown (default: OpenAI), enters their API key in the password-masked field, optionally enters a display name, and clicks **"Add Key"**. The key appears in the Added Keys list with status "⚠ Not tested."

The user clicks **"Test Key"** (or "Test All Keys"). The app sends a minimal API request to the provider. On success, the status updates to "✓ Validated" in green. The user may add additional keys (Anthropic, Google, etc.) following the same flow.

**Validations:**
- Key must be non-empty
- Key must not be a duplicate of an already-added key (toast: "This API key is already added.")

**Decision point — Skip:** The user may click **"Skip"** at any point to advance without adding any keys. The app will function but no AI calls will succeed until keys are added later in [`screens/settings.html`](../screens/settings.html) → Providers.

**Decision point — Back:** Returns to Welcome screen.

When at least one key is added (validated or not), the user clicks **"Next"** → advances to Step 2.

### Step 3: Select Persona
**Screen:** [`screens/onboarding-wizard.html`](../screens/onboarding-wizard.html) — Screen 2: Persona (Step 2 of 4)

Three starter Persona cards are displayed: "General Assistant," "Code Helper," "Writing Coach." Each shows the Persona name and first 2-3 lines of the system prompt.

The user clicks a card to select it (e.g., "General Assistant"). A Customization Panel appears below the cards with:
- Display Name (pre-filled, editable)
- System Prompt (pre-filled, editable)
- Default Chat Mode: Standard / Text Completion (default: Standard)
- Info: "Will use your first validated API key with default model settings."

The user may customize the system prompt and display name, then clicks **"Save Persona"** → advances to Step 3.

**Alternative — Create from Scratch:** The user clicks "Create from Scratch" link → blank persona form expands → user fills all fields manually → "Save Persona" → advances to Step 3.

**Decision point — Skip:** Advances to Step 3. The "General Assistant" Persona with default settings is used as the fallback.

**Decision point — Back:** Returns to Step 1 (API Keys).

### Step 4: Configure Wiki Directory
**Screen:** [`screens/onboarding-wizard.html`](../screens/onboarding-wizard.html) — Screen 3: Wiki Directory (Step 3 of 4)

The user chooses one of two paths:

**Path A — Choose Existing Folder:** User clicks "Choose Existing Folder" → Windows folder picker opens. User navigates to an existing folder of `.md` files and selects it. The selected path displays with ".md files found: [N]" and "Total size: [X] KB/MB."

**Path B — Create New Wiki Folder:** User clicks "Create New Wiki Folder" → the app creates `Documents/MySecondBrain-Wiki/` with a starter `index.md` containing: `# My Wiki\n\nWelcome to your personal wiki. Add .md files here to build your second brain.` The new path displays with "Created with starter index.md."

After folder selection/creation, the user may optionally enable **Git Version Control:**
1. Checks "Initialize git repository for version control" → reveals GitHub sub-options
2. Optionally clicks "Configure..." → enters GitHub Personal Access Token + repository name
3. Clicks "Test Connection" → validates token, creates private repo if it doesn't exist
4. Success: "✓ Connected to github.com/[user]/[repo]" — auto-commit and auto-push enabled

**Decision point — Skip:** No wiki directory configured. The user can set it up later in [`screens/settings.html`](../screens/settings.html) → Wiki. A non-intrusive banner appears in the Studio sidebar after the first chat: "📝 Set up your wiki to save AI conversations as permanent notes."

**Decision point — Back:** Returns to Step 2 (Persona).

User clicks **"Next"** → advances to Step 4.

### Step 5: Review Hotkeys
**Screen:** [`screens/onboarding-wizard.html`](../screens/onboarding-wizard.html) — Screen 4: Hotkeys (Step 4 of 4)

A table displays the five default global hotkeys:

| Text Action | Default Hotkey | Capture Scope | Apply Mode |
|-------------|---------------|---------------|------------|
| Rewrite | Alt+Q | selection | replaceSelection |
| Summarize | Alt+W | selection | showOnly |
| Explain | Alt+E | selection | showOnly |
| Translate | Alt+R | selection | replaceSelection |
| Continue Writing | Alt+C | focusedElement | insertAtCursor |
| Command Bar | Alt+Space | — | — |

The user may click **[Change]** on any row → key recorder overlay opens: "Press new key combination..." → next key combo captured → conflict detection runs. If a conflict is detected, a warning appears: "⚠️ [combo] may conflict with [app/function]. Use anyway?" with Confirm/Cancel. On confirm, the table updates.

User may click "Reset to Defaults" to restore all defaults.

**Decision point — Skip:** All default hotkeys are used as-is.

**Decision point — Back:** Returns to Step 3 (Wiki Directory).

User clicks **"Finish"** → advances to the Finish screen.

### Step 6: Finish and Launch
**Screen:** [`screens/onboarding-wizard.html`](../screens/onboarding-wizard.html) — Screen 5: Finish

A summary card displays what was configured:
- "🔑 [N] API key(s) added" or "⚠️ No API keys — add them in Settings to use AI"
- "🤖 Persona: [name]"
- "📝 Wiki: [path]" or "⚠️ No wiki configured — set up in Settings"
- "⌨️ Hotkeys: [N] configured"

**Primary action — Launch Studio:** User clicks **"Launch Studio"** → wizard window closes → the Studio window opens ([`screens/studio-chat.html`](../screens/studio-chat.html)) with a new chat tab using the configured Persona pre-selected (or "General Assistant" if skipped). The textbox is ready to type. The sidebar shows the empty chat list: "No chats yet. Press Ctrl+N to start a new conversation." Global hotkeys are now active system-wide.

**Secondary action — Import from ChatGPT/Claude:** User clicks **"Import from ChatGPT or Claude"** → launches I2 import flow (file picker → preview → confirm → import). After import, navigates to Studio with the imported chats visible in the sidebar.

**Tertiary info:** "You can re-run this wizard anytime from Settings → Onboarding."

### Step 7: First Chat Experience (Post-Onboarding)
**Screen:** [`screens/studio-chat.html`](../screens/studio-chat.html)

The user types their first message in the new chat tab and presses Enter. The AI responds with streaming tokens, Markdown rendering, and code highlighting per the configured Persona's system prompt.

After the first chat completes, if the user skipped the Wiki step (Step 4), a non-intrusive banner appears at the bottom of the Studio sidebar: "📝 Set up your wiki to save AI conversations as permanent notes." with a "Set Up" link that opens [`screens/settings.html`](../screens/settings.html) → Wiki section.

---

## Alternative Paths

### Path B: Import Instead of Fresh Start
On the Finish screen, the user chooses "Import from ChatGPT or Claude" instead of "Launch Studio." The I2 import flow runs:
1. File picker opens — user selects ChatGPT or Claude export JSON
2. App parses and validates the file
3. Preview displays: chat title, message count, date range
4. User confirms import
5. New ChatThreads created and appear in sidebar
6. User navigates to Studio with imported chats visible

### Path C: Wizard Closed Mid-Way
At any step (1-4), if the user clicks the X button to close the wizard:
- Confirmation dialog: "You haven't finished setup. Your progress is saved. You can continue later or re-run the wizard from Settings." Options: "Continue Setup" / "Close Anyway"
- All completed steps are saved
- On next app launch: wizard re-opens at the first incomplete step
- If all 4 steps eventually complete: wizard never auto-launches again

### Path D: Skip All Steps
The user clicks "Skip" on every step (1-4):
- No API keys → AI calls will fail until keys are added
- Persona falls back to "General Assistant" default
- No wiki directory → wiki features show empty states
- Default hotkeys used
- User lands in Studio with "General Assistant" Persona
- Wiki setup banner appears after first chat

### Path E: Re-run Wizard from Settings
At any time, the user navigates to [`screens/settings.html`](../screens/settings.html) → clicks "Re-run Onboarding Wizard" at the bottom of the category sidebar. The wizard opens at Screen 0 (Welcome) with all existing settings pre-populated (existing API keys shown, existing Persona selected, existing wiki path displayed, existing hotkeys shown). The user can modify any setting or skip unchanged steps.

---

## Failure Points

### At Step 2 (API Keys):
| Failure | Handling |
|---------|----------|
| API key invalid (401) | Red "✕ Invalid" status on key row. Tooltip shows error code + message from provider. Key remains in list. |
| Network error during test | Yellow "⚠ Could not reach [provider]" status. User can retry or continue. |
| Duplicate key | Toast: "This API key is already added." Key not added. |
| Provider API down | Yellow "⚠ Could not reach [provider]" status. User advised to try later. |

### At Step 3 (Persona):
| Failure | Handling |
|---------|----------|
| System prompt too long | Character counter shows limit. "Save Persona" disabled if exceeded. Tooltip: "System prompt must be under [N] characters." |
| No API keys configured | Persona saved but Model Configuration uses a placeholder. Warning: "No API key configured. This Persona won't work until you add a key in Settings." |

### At Step 4 (Wiki Directory):
| Failure | Handling |
|---------|----------|
| Folder picker cancelled | No change — previous selection (if any) remains displayed. |
| Wiki folder inaccessible (permissions) | Red error: "Cannot access this folder. Check permissions." |
| `git init` fails | Red error: "Could not initialize git repository. [error details]." Git checkbox unchecked. |
| GitHub token invalid | Red: "Authentication failed. Check your token and try again." |
| GitHub repo creation fails | Red: "Could not create repository. [error details]. You can create it manually on GitHub." |

### At Step 4 (Hotkeys):
| Failure | Handling |
|---------|----------|
| Hotkey conflict detected | Yellow warning: "⚠️ [combo] may conflict with [app/function]. Use anyway?" with Confirm/Cancel. |

### At Finish Screen:
| Failure | Handling |
|---------|----------|
| Studio fails to open | "Could not open Studio. [error details]. Try restarting the app." with "Retry" button. |
| Import file corrupted | "Could not parse file. Unsupported format or corrupted file." Return to Finish screen. |
| Import file empty | "The selected file contains no recognizable chat data." Return to Finish screen. |

---

## Edge Cases

1. **User has no API keys at all:** They can still complete the wizard (skip or manually add keys later). Studio opens but AI calls fail with "No API key configured" error until keys are added.

2. **User has API keys but all fail validation:** Keys are added with "✕ Invalid" status. Studio opens but AI calls will fail. The user must fix keys in Settings → Providers.

3. **User is offline during onboarding:** "Test Key" will fail with network error for all providers. Keys are added with "⚠ Could not reach [provider]" status. User can continue offline and validate later. Studio opens; AI calls will fail until connectivity is restored.

4. **Wiki directory contains non-.md files:** Only `.md` files are indexed. Non-`.md` files are hidden from the file tree by default (toggle: "Show all files" reveals them).

5. **Wiki directory is on a network drive:** If the path is accessible at setup time but becomes unavailable later, Wiki Browser shows: "Wiki directory is not accessible. Check your network connection."

6. **User imports ChatGPT/Claude data during onboarding:** Imported chats appear in the sidebar alongside the new chat tab. The imported chats are permanent (IsTransient=false) and fully functional.

7. **App crashes during onboarding:** On next launch, the wizard resumes from the first incomplete step. All completed step data is saved.

8. **User runs onboarding, then immediately re-runs it from Settings:** Existing settings are pre-populated. Changing and re-saving overwrites previous settings.

---

## Completion
The user lands on [`screens/studio-chat.html`](../screens/studio-chat.html) with:
- A new chat tab open, pre-configured with the selected (or default) Persona
- Global hotkeys active system-wide (Alt+Q/W/E/R/C for text actions, Alt+Space for Command Bar)
- Wiki directory configured (or a banner prompting setup after first chat)
- API keys validated and ready for use
- The full Studio workspace available: sidebar, chat list, conversation view, right panel

The user's first action is typically typing a message into the textbox and pressing Enter — beginning their first AI conversation in MySecondBrain.

---

## Cross-References
- Screen spec: [`screens/onboarding-wizard.md`](../screens/onboarding-wizard.md) — all 6 wizard screens
- Screen spec: [`screens/studio-chat.md`](../screens/studio-chat.md) — post-onboarding destination
- Screen spec: [`screens/settings.md`](../screens/settings.md) — all settings available for later reconfiguration
- Feature spec: [`features/settings-configuration.md`](../features/settings-configuration.md) A8 — Onboarding Wizard
- Feature spec: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B1, B3 — API Keys, Personas
- Feature spec: [`features/personal-wiki.md`](../features/personal-wiki.md) N1 — Wiki Directory Configuration
- Feature spec: [`features/windows-os-integration.md`](../features/windows-os-integration.md) P1 — Global Hotkeys
- Feature spec: [`features/import-export.md`](../features/import-export.md) I2 — Import from ChatGPT/Claude
