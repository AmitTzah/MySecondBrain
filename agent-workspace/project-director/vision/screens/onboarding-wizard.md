# Onboarding Wizard — Screen Specification

## Purpose

The Onboarding Wizard is the first-launch guided setup experience for MySecondBrain. It walks a new user through essential configuration — API keys, persona selection, wiki directory, and hotkeys — before they enter the Studio. Each step is skippable, and the wizard is re-launchable from Settings at any time. The wizard occupies a dedicated full window with no Studio chrome visible.

## Layout

Full-window dedicated layout with no Studio sidebar or chrome:
- **Header region:** App logo/name + "Onboarding" label
- **Step indicator:** Horizontal step dots (4 dots for the 4 config steps) with labels below
- **Content region:** Central card containing the current step's content
- **Navigation bar:** Back, Next/Skip buttons at the bottom-right of the content card

The Welcome screen and Finish screen are separate pages that bookend the 4-step sequence. Step dots are only shown during steps 1–4 (not on Welcome or Finish).

## Regions

### Region 1: Header

Fixed header bar at the top of the window:
- **Left:** App icon + "MySecondBrain" text
- **Right:** "Onboarding" label in muted text
- Subtle bottom border separating header from content

### Region 2: Step Indicator (Steps 1–4 only)

Horizontal row of 4 connected dots centered below the header:
- Each dot: hollow circle (○) for incomplete, filled (●) for current, checkmark (✓) for completed
- Step labels below each dot: "API Keys", "Persona", "Wiki", "Hotkeys"
- Clicking a completed step dot navigates back to that step (allows revisiting)
- Clicking a future step dot does nothing
- Current step dot is slightly larger and filled

### Region 3: Content Card (All screens)

Centered card (max-width ~640px) containing the active step's content:
- Step title (h2)
- Step subtitle/description (muted text)
- Step-specific interactive content (form fields, cards, tables)
- Progress feedback where applicable (e.g., key test results, file counts)

### Region 4: Navigation Bar (Steps 1–4)

Horizontal button row at the bottom of the content card:
- **Back button** (left): Returns to previous step. Disabled/hidden on Step 1 (or goes to Welcome screen).
- **Skip button** (center): Skips current step, advances to next step. Always available on Steps 1–4.
- **Next/Finish button** (right): Primary action. Labeled "Next" on Steps 1–3, "Finish" on Step 4.

## Screens (in sequence)

---

### Screen 0: Welcome

**Purpose:** Introduce the app and set expectations before configuration begins.

**Content:**
- App icon (large, centered)
- "Welcome to MySecondBrain" heading
- Tagline: "Your unified AI hub — any model, any provider, your own keys"
- Three feature highlight cards in a horizontal row:
  1. "🔄 Three-Tier AI" — "Hotkey rewrites, Command Bar, and full Studio — AI is always one keystroke away"
  2. "🔑 Bring Your Own Keys" — "Use any AI provider. No subscription, no platform lock-in."
  3. "📝 Personal Wiki" — "Turn AI conversations into permanent .md notes on your own computer"
- "Get Started" primary button → advances to Step 1

**Empty/Loading/Error States:** Not applicable — static content.

---

### Screen 1: API Keys (Step 1 of 4)

**Purpose:** The user adds one or more API keys for AI providers.

**Content:**
- Title: "Add Your API Keys"
- Subtitle: "Connect to AI providers using your own keys. You can add more anytime in Settings."

**Add Key Form:**
- Provider dropdown: OpenAI, Anthropic (Claude), Google (Gemini), DeepSeek, Xiaomi MiMo, Moonshot, Mistral, OpenAI-Compatible. Default: OpenAI.
- API Key input field (password-masked). Placeholder: "sk-..."
- Display Name input field (optional). Placeholder: "My OpenAI Key"
- "Add Key" button — validates the key is non-empty and not a duplicate, then adds to the list below

**Added Keys List:**
- Table or card list below the form. Each entry shows:
  - Provider icon/name
  - Masked key (e.g., "sk-...abc123")
  - Display name (if set, otherwise blank)
  - Status: ✓ "Validated" (green) or ⚠ "Not tested" (yellow)
  - ✕ Remove button (with confirmation: "Remove this key?")
- "Test All Keys" button — sends a minimal API request for each untested key. Updates status per key.
- When at least one key is added and validated: list shows ✓ indicators.

**Empty State:** "No keys added yet. Add at least one API key to start using AI models." The "Add Key" form is always visible and ready.

**Loading State:** During "Test Key" or "Test All Keys": spinner on the key being tested with text "Testing..."

**Error States:**
- Key validation fails: Red "✕ Invalid" status with tooltip showing error details (e.g., "401 Unauthorized")
- Network error during test: Yellow "⚠ Could not reach provider" status
- Duplicate key: Toast "This key is already added."

**Skip Behavior:** Advances to Step 2. No keys configured. The user can add keys later in Settings → Providers.

**Navigation:** Back → Welcome screen. Next → Step 2.

---

### Screen 2: Persona (Step 2 of 4)

**Purpose:** The user selects a starter persona (AI behavior preset) and optionally customizes it.

**Content:**
- Title: "Choose Your Assistant"
- Subtitle: "A Persona defines how the AI behaves. Pick one and customize it to your style."

**Starter Persona Cards:**
Three cards displayed in a row (or vertical stack on narrow windows):
1. **"General Assistant"** — System prompt preview: "You are a helpful, thoughtful assistant. Provide clear, accurate, and concise responses. When you don't know something, say so honestly."
2. **"Code Helper"** — System prompt preview: "You are an expert software developer. Write clean, well-documented code. Explain your reasoning. Prefer practical solutions over theoretical ones."
3. **"Writing Coach"** — System prompt preview: "You are an experienced writing coach and editor. Help improve clarity, flow, and impact. Be constructive and specific in feedback. Preserve the author's voice."

Each card shows:
- Persona name (bold)
- First 2–3 lines of system prompt (truncated, muted text)
- Radio-style selection indicator (outline becomes filled when selected)
- Entire card is clickable to select

**Customization Panel (appears below cards when a persona is selected):**
- "Display Name" text input (pre-filled with selected persona name)
- "System Prompt" multi-line textarea (pre-filled with selected persona's system prompt, editable)
- "Default Chat Mode" radio: Standard / Text Completion (default: Standard)
- Model Configuration info: "Will use your first validated API key with default model settings. You can customize this in Settings → Profiles."
- "Save Persona" button — saves the customized persona and advances to Step 3

**"Create from Scratch" link** below the cards — expands a blank persona form (same fields as customization panel but empty). For users who want full control from the start.

**Skip Behavior:** Uses the "General Assistant" persona with its default settings. Advances to Step 3.

**Navigation:** Back → Step 1. Next → Step 3 (after selecting/saving a persona).

---

### Screen 3: Wiki Directory (Step 3 of 4)

**Purpose:** The user selects or creates the wiki directory — a folder of `.md` files that serves as their personal knowledge base.

**Content:**
- Title: "Set Up Your Personal Wiki"
- Subtitle: "Your wiki is a folder of Markdown files on your computer. AI can read it, write to it, and help you build your second brain."

**Folder Selection:**
- "Choose Existing Folder" button — opens Windows folder picker dialog
- "Create New Wiki Folder" button — creates `Documents/MySecondBrain-Wiki/` with a starter `index.md` file containing: "# My Wiki\n\nWelcome to your personal wiki. Add .md files here to build your second brain."

**Selected Path Display (appears after selection/creation):**
- Folder icon + full path in monospace text
- **If existing folder:** App immediately runs wiki indexing (N2) to scan all `.md` files and regenerate `index.md` (N11) at the wiki root. Display shows:
  - ".md files found: [N]" + "Total size: [X] KB/MB"
  - "index.md auto-generated" (or "index.md updated" if it already existed)
  - If the folder contains zero `.md` files: "No .md files found. index.md created as a starting point."
- **If new folder created:** "Created with starter index.md"

**Git Version Control (checkbox):**
- ☐ "Initialize git repository for version control"
- When checked, reveals sub-options:
  - "Auto-commit on file change" — enabled by default. The existing file system watcher (N1) detects wiki file changes. After a 30-second debounce period with no further changes, the app auto-commits with message: "wiki: [action] — [filename]" (e.g., "wiki: Write to Wiki — python-tips.md" or "wiki: External edit — project-notes.md").
  - "Connect to GitHub" with "Configure..." button
    - Clicking "Configure..." opens a sub-form:
      - GitHub Personal Access Token (password-masked input)
      - Repository name (text input, e.g., "my-second-brain-wiki")
      - "Create private repository" checkbox (default: checked)
      - "Test Connection" button — validates token, checks if repo exists, creates if not
      - Success: "✓ Connected to github.com/[user]/[repo]" 
      - Auto-push enabled: commits are pushed to GitHub after each auto-commit
  - Note: "GitHub free tier supports unlimited public/private repos up to 1GB each. Your .md wiki files are tiny — you won't hit this limit."

**No Git Selected / Skip:**
- If git is not initialized, N6 (Automatic Wiki Versioning snapshots) provides local version history instead.
- Note: N6 (Automatic Wiki Versioning) remains the primary versioning mechanism. Git is an optional bonus for power users — it does NOT replace N6.

**Skip Behavior:** No wiki directory configured. The user can set it up later in Settings → Wiki.

**Navigation:** Back → Step 2. Next → Step 4.

---

### Screen 4: Hotkeys (Step 4 of 4)

**Purpose:** The user reviews and optionally customizes global hotkey assignments.

**Content:**
- Title: "Review Your Hotkeys"
- Subtitle: "These global shortcuts work from any Windows application — no need to switch windows."

**Hotkey Table:**
| Text Action | Default Hotkey | |
|-------------|---------------|---|
| Rewrite | Alt+Q | [Change] |
| Summarize | Alt+W | [Change] |
| Explain | Alt+E | [Change] |
| Translate | Alt+R | [Change] |
| Command Bar | Alt+Space | [Change] |

- "Change" button on each row opens a key recorder overlay: "Press new key combination..." 
- The recorder captures the next key combination pressed and displays it
- Conflict detection: if the combination is already used by Windows or another app, display "⚠️ [combo] may conflict with [app/function]. Use anyway?" with Confirm/Cancel
- After recording: the table updates with the new combination
- "Reset to Defaults" link below the table — restores all default hotkeys

**Skip Behavior:** All default hotkeys are used as-is.

**Navigation:** Back → Step 3. Next button is labeled "Finish" → navigates to Finish screen.

---

### Screen 5: Finish

**Purpose:** Confirms setup is complete and transitions the user into the Studio.

**Content:**
- Large checkmark icon (✓) in a circle
- "You're All Set!" heading
- Summary card showing what was configured:
  - "🔑 [N] API key(s) added" or "⚠️ No API keys — add them in Settings to use AI"
  - "🤖 Persona: [name]" (e.g., "General Assistant")
  - "📝 Wiki: [path]" or "⚠️ No wiki configured — set up in Settings"
  - "⌨️ Hotkeys: [N] configured" (always shows 5 — either defaults or custom)

**Primary Action:**
- "Launch Studio" button (prominent, primary) — closes the wizard, opens [`studio-chat.html`](studio-chat.html) with a new chat using the selected Persona (or "General Assistant" default). The Studio sidebar and all features are now active.

**Secondary Action:**
- "Import from ChatGPT or Claude" link/button — launches the I2 import flow. Opens a file picker for ChatGPT/Claude export JSON files. See [`features/import-export.md`](../features/import-export.md) I2 for full import behavior. After import, navigates to Studio.

**Tertiary Info:**
- Muted text: "You can re-run this wizard anytime from Settings → Onboarding."

**Loading State:** "Launch Studio" shows a brief spinner while the app initializes the main window.

**Error State:** If Studio fails to open: "Could not open Studio. [error details]. Try restarting the app."

## Data Displayed

- **Step 1:** Reads from [`data/api-key.md`](../data/api-key.md) — provider types, key values (masked), validation status
- **Step 2:** Reads from [`data/persona.md`](../data/persona.md) — built-in starter personas, user-created personas
- **Step 3:** Reads file system for existing `.md` files in selected directory. Writes to disk if creating new wiki folder.
- **Step 4:** Reads hotkey assignments from settings. Defaults from [`features/windows-os-integration.md`](../features/windows-os-integration.md) P1.
- **Finish:** Summarizes all configured entities.

## Actions

| Action | Trigger | Behavior |
|--------|---------|----------|
| Get Started | Welcome screen button | Advances to Step 1 |
| Add Key | Step 1 button | Validates key is non-empty, non-duplicate. Adds to list with "Not tested" status. |
| Test Key / Test All Keys | Step 1 button | Sends minimal API request to provider. Updates status to ✓ Validated or ✕ Invalid with error detail. |
| Remove Key | Step 1 ✕ button | Confirmation dialog: "Remove this key?" On confirm, removes from list. |
| Select Persona | Step 2 card click | Expands customization panel below cards with pre-filled values. |
| Save Persona | Step 2 button | Saves persona with customizations. Advances to Step 3. |
| Create from Scratch | Step 2 link | Expands blank persona form. |
| Choose Existing Folder | Step 3 button | Opens Windows folder picker. On selection, displays path + file count. |
| Create New Wiki Folder | Step 3 button | Creates `Documents/MySecondBrain-Wiki/` with starter `index.md`. Displays path. |
| Initialize git repo (checkbox) | Step 3 checkbox | When checked: runs `git init` in wiki folder, reveals GitHub sub-options. |
| Configure GitHub | Step 3 button | Expands GitHub auth form: token + repo name + private toggle + Test Connection. |
| Change Hotkey | Step 4 button | Opens key recorder overlay. Captures next key combo. Validates for conflicts. |
| Reset to Defaults | Step 4 link | Restores all hotkeys to default values. |
| Back | Navigation bar | Returns to previous screen/step. |
| Skip | Navigation bar | Skips current step. All steps are independently skippable. |
| Next / Finish | Navigation bar | Advances to next step. On Step 4, navigates to Finish screen. |
| Launch Studio | Finish screen button | Closes wizard, opens Studio with new chat. |
| Import from ChatGPT/Claude | Finish screen link | Launches I2 import flow (file picker → preview → confirm → import). |

## Empty States

| Context | State |
|---------|-------|
| Step 1 — No keys | "No keys added yet. Add at least one API key to start using AI models." Add Key form is visible. |
| Step 2 — No persona selected | All three cards shown. None selected. Skip uses "General Assistant." |
| Step 3 — No folder selected | Both folder buttons shown. No path displayed. |
| Step 4 — All defaults | Table shows default hotkeys. No changes needed. |

## Loading States

| Context | State |
|---------|-------|
| Testing API key | Spinner on key row + "Testing..." text. Button disabled during test. |
| Testing GitHub connection | Spinner on Configure form + "Connecting..." text. |
| Creating wiki folder | Brief spinner + "Creating..." on the Create button. |
| Launching Studio | Brief spinner on "Launch Studio" button while main window initializes. |

## Error States

| Context | State |
|---------|-------|
| API key invalid | Red "✕ Invalid" on key row. Tooltip: error code + message from provider. |
| API key network error | Yellow "⚠ Could not reach [provider]" on key row. |
| Duplicate key | Toast: "This API key is already added." Key not added. |
| Folder picker cancelled | No change — previous selection (if any) remains displayed. |
| Wiki folder inaccessible | Red error: "Cannot access this folder. Check permissions." |
| git init fails | Red error: "Could not initialize git repository. [error details]." |
| GitHub token invalid | Red: "Authentication failed. Check your token and try again." |
| GitHub repo creation fails | Red: "Could not create repository. [error details]. You can create it manually on GitHub." |
| Hotkey conflict detected | Yellow warning: "⚠️ [combo] may conflict with [app/function]. Use anyway?" with Confirm/Cancel. |
| Studio fails to open | "Could not open Studio. [error details]. Try restarting the app." with "Retry" button. |

## Navigation

**Entry Points:**
- First launch of the application (no existing settings detected)
- Settings → "Re-run Onboarding Wizard" button

**Exit Points:**
- "Launch Studio" → [`studio-chat.html`](studio-chat.html) with new chat
- "Import from ChatGPT/Claude" → import flow (I2), then Studio
- Closing the wizard window mid-way: all completed steps are saved. On next launch, wizard resumes from the first incomplete step. If all 4 steps are complete, wizard does not re-appear (user goes directly to Studio).

**Wizard Close Behavior:**
- If user closes the wizard window (X button) before completing all steps:
  - Completed steps are saved to settings
  - On next app launch: if any step is incomplete, wizard re-opens at the first incomplete step
  - If all steps are complete: wizard does not open, user goes directly to Studio
- Confirmation dialog on close: "You haven't finished setup. Your progress is saved. You can continue later or re-run the wizard from Settings." Options: "Continue Setup" / "Close Anyway"

## Cross-References

- Feature spec: [`features/settings-configuration.md`](../features/settings-configuration.md) A8
- API key management: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B1
- Persona management: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B3
- Wiki directory: [`features/personal-wiki.md`](../features/personal-wiki.md) N1
- Hotkeys: [`features/windows-os-integration.md`](../features/windows-os-integration.md) P1
- Import: [`features/import-export.md`](../features/import-export.md) I2
- Wiki versioning: [`features/personal-wiki.md`](../features/personal-wiki.md) N6
- Git version control for wiki: ⚠️ NEW FEATURE — not yet in feature inventory. Described in this spec under Step 3. Architect must add to feature-inventory.md as a new Core Feature before implementation.

## Flagged Concerns

1. ⚠️ FLAGGED: Git version control for wiki is a new feature discovered during screen design. It must be added to [`feature-inventory.md`](../feature-inventory.md) as a Core Feature. N6 (Automatic Wiki Versioning snapshots) may become redundant if git is adopted — Architect should evaluate consolidation.

2. ⚠️ FLAGGED: GitHub Personal Access Token storage requires encryption (Windows DPAPI, consistent with B1 API key encryption). Token scope should be minimal (repo-only).

3. ⚠️ FLAGGED: Auto-commit on file change via file system watcher — must handle rapid successive changes (debounce). Must not commit while a file is being written (wait for write completion).
