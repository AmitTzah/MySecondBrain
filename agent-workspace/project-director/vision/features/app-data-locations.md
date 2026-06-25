# App Data Locations — Feature Spec

## What the User Accomplishes

The user views a comprehensive reference of every file and folder the app writes to or reads from on their computer. This is a transparency and troubleshooting feature — the user can see exactly where their data lives, how much space it uses, whether they can safely edit it, and open any location in Windows Explorer.

## Trigger

- Settings → "System Info" category (19th category in the Settings sidebar, after Diagnostics)
- "?" (Help) icon in the app header bar → "App Data Locations" menu item

## Detailed Behavior

### System Info — Settings Category

The "System Info" category appears in the Settings sidebar as the 19th category (after Diagnostics):

```
17. 🩺 Diagnostics
18. ℹ️ System Info    ← NEW
```

Content area shows:

**App Data Locations Table:**

A comprehensive reference table listing every file and folder the app uses. Columns:

| Column | Description |
|--------|-------------|
| **Location** | Full file system path (using `%LOCALAPPDATA%` / `%USERPROFILE%` notation for readability). Actual expanded path shown in tooltip on hover. |
| **Purpose** | What this file/folder is for. Clear, non-technical description. |
| **Size on Disk** | Current size (e.g., "2.3 MB", "145 KB", "—" for files that don't exist yet). Updated in real-time when the category is viewed. |
| **User Can Edit?** | Badge: ✅ Yes (user-editable), ⚠️ Caution (editable but risky), ❌ No (app-managed, do not touch). |
| **Actions** | "Open in Explorer" button — opens the parent folder in Windows Explorer with the file/folder selected. |

**Entries in the table:**

| Location | Purpose | User Can Edit? |
|----------|---------|----------------|
| `%LOCALAPPDATA%/MySecondBrain/msb.db` | Main SQLite database — all user data (chats, messages, personas, settings, API keys encrypted via DPAPI) | ❌ No — app-managed |
| `%LOCALAPPDATA%/MySecondBrain/msb.db-wal` | SQLite Write-Ahead Log — temporary journal file. Part of normal database operation. | ❌ No — app-managed |
| `%LOCALAPPDATA%/MySecondBrain/msb.db-shm` | SQLite Shared Memory — temporary index file. Part of normal database operation. | ❌ No — app-managed |
| `%LOCALAPPDATA%/MySecondBrain/logs/` | Serilog rolling JSON log files — one file per day, retained 30 days. Configured in Settings → Diagnostics. | ⚠️ Caution — safe to delete old logs, but current day's log is in use |
| `%LOCALAPPDATA%/MySecondBrain/workspace/` | Per-chat sandbox directories for bash/code execution. Subdirectory per chat: `workspace/{chat-id}/`. Cleaned automatically — files older than 24h removed on startup. | ❌ No — auto-managed, cleaned every 24h |
| `%LOCALAPPDATA%/MySecondBrain/artifacts/{chat-id}/` | AI-generated files surfaced via `present_files` — per-chat subdirectory. Cleaned up when source chat is deleted. | ⚠️ Caution — files can be opened/saved externally, but deleting may break artifact references |
| `%LOCALAPPDATA%/MySecondBrain/skills/` | User-added community Agent Skills. Never overwritten by app updates. Users add skills by copying folders here. | ✅ Yes — user-managed skills directory |
| `[Wiki Directory]` | User-configured wiki directory (set in Settings → Wiki). Contains user's personal .md knowledge base. | ✅ Yes — user's own files |
| `[Backup Directory]` | User-configured backup destination (Google Cloud Storage bucket or local path). Set in Settings → Backup. | ✅ Yes — user-configured |
| `%LOCALAPPDATA%/MySecondBrain/settings.json` | Application settings (persisted preferences). May be stored in SQLite instead depending on implementation. | ❌ No — app-managed |

**Clear distinctions between categories:**

- **❌ App-Managed (Don't Touch):** Database files, logs, workspace, settings. These are internal — modifying them may corrupt data or crash the app.
- **⚠️ Caution (Editable but Risky):** Artifacts, old logs. Can be manually managed but understand the consequences.
- **✅ Yes (User-Editable):** Wiki directory, skills directories, backup directory. These are the user's own files.

### "?" Icon in App Header

A "?" (Help) icon appears in the app header bar (top-right area of the main window). Click opens a dropdown:

- **App Data Locations** → navigates to Settings → System Info
- **Keyboard Shortcuts** → opens Ctrl+/ shortcut reference overlay (C12)
- **About MySecondBrain** → version, license, credits

The "?" icon is always visible regardless of which screen the user is on.

## Data

- File sizes read from disk in real-time when the System Info category is viewed
- No persistent data entity — this is a read-only reference panel

## Success/Failure States

- **All paths accessible:** Table displays with sizes and "Open in Explorer" buttons functional.
- **Path not yet created (e.g., no logs generated yet):** Size shows "—" (not yet created). "Open in Explorer" button opens the parent directory.
- **Path inaccessible (permission error):** Size shows "⚠️ Cannot access". Tooltip: "Permission denied — this path may require administrator access."
- **Wiki directory not configured:** Shows "[Not configured]" with a link to Settings → Wiki.
- **Backup not configured:** Shows "[Not configured]" with a link to Settings → Backup.

## Permissions

- Single-user app. All paths shown are on the user's own machine under their own user profile.
- "Open in Explorer" uses the user's own file permissions.

## Interactions

- Settings → System Info category (19th in sidebar)
- "?" icon in app header → App Data Locations
- Wiki directory path references Settings → Wiki (N1)
- Backup directory path references Settings → Backup (R1)
- Skills directories reference Agent Skills discovery (W4)
- Workspace cleanup schedule references P10 (workspace isolation)
