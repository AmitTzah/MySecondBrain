# Settings — Screen Specification

## Purpose

The Settings screen provides a single, organized interface for configuring all global application settings. It uses a sidebar + content layout: a category sidebar on the left navigates between 14 setting categories; the selected category's settings appear in the content area on the right. All settings changes take effect immediately — there is no "Save" button.

## Layout

Two-column layout within the standard Studio frame:
- **Left:** Studio app navigation sidebar (same as studio-chat)
- **Center-Left:** Settings category sidebar (narrow, ~200px)
- **Center-Right:** Settings content area (scrollable)

The Studio sidebar (Chats, Wiki, Media, Artifacts, Usage, Settings) remains visible on the left. The Settings category sidebar appears to its right. The right panel (Artifacts + Chat Nav) is hidden on this screen.

## Regions

### Region 1: Studio Sidebar (Leftmost)
Same as [`studio-chat.md`](studio-chat.md) Region 1. The ⚙️ Settings nav item is active. All other nav items link to their respective screens.

### Region 2: Settings Category Sidebar
**Width:** Fixed ~200px.

Categories listed vertically:
1. 🔑 Providers
2. 👤 Profiles
3. 🎨 Appearance
4. 📝 Wiki
5. ☁️ Backup
6. ⌨️ Hotkeys
7. 🔧 Tools
8. 🔔 Notifications
10. 🚀 Startup
11. 🔄 Updates
12. 💰 Pricing
13. 🔒 Security
14. 🛠️ Maintenance

Active category highlighted. Clicking a category changes the content area.

At bottom:
- "Re-run Onboarding Wizard" link → opens [`onboarding-wizard.html`](onboarding-wizard.html)

### Region 3: Settings Content Area
Scrollable area showing the active category's settings. Each category section has:
- Category title (h2)
- Settings controls (inputs, toggles, dropdowns, buttons) with labels and descriptions
- Settings take effect immediately on change

## Category Content

### 🔑 Providers
- **API Keys list:** Table of all configured API keys. Columns: Provider, Display Name, Masked Key, Status (✓ Validated / ⚠ Not tested / ✕ Invalid), Actions (Edit, Delete, Test).
- **"Add API Key" button:** Opens form with Provider dropdown, Key field, Display Name, "Test Key" button, "Save" button. See B1.
- **Encryption status:** "API keys encrypted via Windows DPAPI" with green checkmark.

### 👤 Profiles
- **Default Persona (A2):** Dropdown of all Personas + "Last Used" option.
- **Personas list:** Table of all Personas. Columns: Name, System Prompt (truncated), Default Model Config, Chat Mode. Actions: Edit, Duplicate, Delete.
- **"New Persona" button:** Opens Persona creation form (B3).
- **Model Configurations list:** Table of all Model Configurations. Columns: Name, Provider, Model ID, Temperature, Context Strategy. Actions: Edit, Duplicate, Delete.
- **"New Model Configuration" button:** Opens Model Config creation form (B2).

### 🎨 Appearance
- **Dark Mode / Light Mode (A5):** Toggle or dropdown. Dark (default) | Light.
- **Chat Visual Theme (A3):** Dropdown: Classic, Compact, Bubble.
- **Font Family:** Dropdown of system fonts or text input.
- **Font Size:** Numeric stepper (10-24px, default 14).
- **Font Weight:** Toggle: Normal | Bold.
- **Live Preview:** Sample message rendering showing current settings.

### 📝 Wiki
- **Wiki Directory:** Current path displayed. "Change" button opens folder picker.
- **Indexing Status:** "✓ [N] .md files indexed" with last index time. "Re-index Now" button.
- **Git Version Control:** Checkbox "Initialize git repository for version control" with GitHub sub-options (same as onboarding wizard Step 3).
- **Wiki Snapshots (N6):** Current snapshot count and total size. "Manage Snapshots" button.

### ☁️ Backup
- **Google Cloud Storage:** Configuration status. "Configure" button if not set up.
- **Backup Schedule (R2):** Radio: Daily, Weekly, Manual Only.
- **"Backup Now" button (R3):** Triggers immediate backup.
- **Last Backup:** Date/time of last successful backup.
- **Restore (R4):** "Restore from Backup" button with confirmation.

### ⌨️ Hotkeys
- **Hotkey Table:** Same as onboarding wizard Step 4. Action | Hotkey | Change button.
- Defaults: Alt+Q (Rewrite), Alt+W (Summarize), Alt+E (Explain), Alt+R (Translate), Alt+Space (Command Bar).
- "Change" opens key recorder overlay.
- "Reset to Defaults" link.

### 🔧 Tools
- **Tool Auto-Approval Defaults (H5):** Per-tool toggles: Browser Search, Terminal/Script, File Generation, File Editing. Each: Ask (default) | Auto-Approve | Disabled.
- **STT Provider (A10):** Provider dropdown, Model field, API Key, "Test Microphone" button.

### 🌐 Language
- **UI Language:** Dropdown: English (default). Hebrew available per Q2.
- **RTL Detection:** Toggle: Auto-detect RTL from Unicode ranges (default: ON).

### 🔔 Notifications
- **Sound on Completion (A4):** Toggle. Plays system sound when generation completes.
- **Disable Streaming (A4):** Toggle. When ON, responses appear all at once.
- **Cross-Tab Completion Alert (C35):** Toggle for pulsing green dot on inactive tabs.

### 🚀 Startup
- **Launch on Windows Startup (A6):** Toggle.
- **Restore Last Session (A6):** Toggle. Reopens all chats/tabs from previous session.
- **Minimize to Tray on Close:** Toggle. When ON, closing window minimizes to tray instead of exiting.

### 🔄 Updates
- **Check Frequency (A7):** Radio: On Startup, Daily, Weekly, Manual Only.
- **Current Version:** Displayed. "Check Now" button.

### 💰 Pricing
- **Monthly Budget Limit (S5):** Numeric input with currency. Blank = no limit.
- **Warning Threshold:** Percentage (default 80%). When reached, shows warning.
- **Block API on Limit:** Toggle. When ON, blocks API calls when budget exceeded.

### 🔒 Security
- **API Key Encryption:** Status indicator. "Encrypted via Windows DPAPI ✓"
- **Locked Chat Global Password (C31):** "Set Global Password" button. "Change Global Password" if already set.
- **"Hide Locked Chats from Sidebar" (C31):** Toggle.

### 🛠️ Maintenance
- **Database File Size:** Current size displayed.
- **"Compact Database" button (A9):** Runs VACUUM. Shows before/after size.
- **Reclaimable Space:** Estimated space that can be reclaimed.
- **Last Compaction:** Date/time.

## Data Displayed

- API keys: [`data/api-key.md`](../data/api-key.md)
- Personas: [`data/persona.md`](../data/persona.md)
- Model Configurations: [`data/model-configuration.md`](../data/model-configuration.md)
- Settings stored in SQLite database

## Actions

| Action | Trigger | Behavior |
|--------|---------|----------|
| Navigate category | Click category in sidebar | Content area updates to show that category's settings. |
| Change setting | Any input change | Setting takes effect immediately. No save button. |
| Add API Key | "Add API Key" button | Opens B1 form. |
| Test API Key | "Test Key" button | Validates against provider API. |
| New Persona / Model Config | Button | Opens creation form. |
| Edit / Delete Persona or Config | Table action | Opens edit form or confirmation dialog. |
| Change Wiki Directory | "Change" button | Opens folder picker. |
| Re-index Wiki | "Re-index Now" button | Rebuilds wiki index. Shows progress. |
| Backup Now | Button | Triggers immediate GCS backup. |
| Change Hotkey | "Change" button | Opens key recorder overlay. |
| Test Microphone | Button | Records short clip, sends to STT provider. |
| Compact Database | Button | Runs VACUUM. Shows before/after. |
| Re-run Onboarding Wizard | Link at bottom of category sidebar | Opens [`onboarding-wizard.html`](onboarding-wizard.html). |
| Navigate back to Studio | Click 💬 Chats in Studio sidebar | Returns to [`studio-chat.html`](studio-chat.html). |

## Empty States

| Context | State |
|---------|-------|
| No API keys | "No API keys configured. Add one to start using AI models." |
| No Personas | "No custom Personas. Built-in defaults are available." |
| No Model Configs | "No Model Configurations. Create one to use with a Persona." |
| Wiki not configured | "No wiki directory selected. Choose a folder of .md files." |
| Backup not configured | "Google Cloud Storage not configured. Set up backup to protect your data." |

## Loading States

| Context | State |
|---------|-------|
| Testing API key | Spinner on "Test Key" button. |
| Re-indexing wiki | Progress bar: "Indexing [N]/[M] files..." |
| Backing up | Progress bar: "Uploading backup..." |
| Compacting database | Progress bar: "Compacting database..." |

## Error States

| Context | State |
|---------|-------|
| API key invalid | Red error on key row. |
| Directory not accessible | "Cannot access this directory. Check permissions." |
| Hotkey conflict | Warning: "This hotkey is already assigned to [action]." |
| VACUUM fails | "Compaction failed. Check available disk space." |
| Backup fails | "Backup failed: [reason]. Check your Google Cloud Storage configuration." |

## Navigation

**Entry Points:**
- Studio sidebar → ⚙️ Settings
- System tray → Settings
- Onboarding wizard → "Configure Later" links

**Exit Points:**
- Studio sidebar → any nav item (💬 Chats, 📝 Wiki, etc.)
- "Re-run Onboarding Wizard" → [`onboarding-wizard.html`](onboarding-wizard.html)

## Cross-References

- Feature spec: [`features/settings-configuration.md`](../features/settings-configuration.md) A1-A10
- API keys: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B1
- Personas/Models: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B2, B3
- Wiki: [`features/personal-wiki.md`](../features/personal-wiki.md) N1
- Backup: [`features/backup-recovery.md`](../features/backup-recovery.md) R1-R4
- Hotkeys: [`features/windows-os-integration.md`](../features/windows-os-integration.md) P1
- Tools: [`features/tool-use-agents.md`](../features/tool-use-agents.md) H5
- Git wiki: [`features/soft-delete-trash.md`](../features/soft-delete-trash.md) — not yet in settings (deferred)
