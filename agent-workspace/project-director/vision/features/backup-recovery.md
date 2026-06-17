# Backup & Recovery — Feature Spec

## What the User Accomplishes
The user configures automatic backups of all app data (SQLite database, wiki .md files, artifact files) to Google Cloud Storage. Manual backups can be triggered anytime. Data can be restored from backup with confirmation.

## Trigger
- Settings → Backup section
- Scheduled: daily, weekly per configuration (R2)
- Manual: "Backup Now" button (R3)

## Detailed Behavior

### R1. Google Cloud Storage Backup
- **What's Backed Up:**
  - Full SQLite database file
  - All wiki .md files (from configured wiki directory N1)
  - All artifact files stored on disk
- **Configuration:** User provides Google Cloud Storage bucket name and credentials (service account JSON key file or application default credentials)
- **Credentials:** Stored encrypted via Windows DPAPI (same mechanism as API keys B1)
- **Test Connection:** "Test Connection" button verifies bucket access before saving
- ⚠️ FLAGGED: Google Cloud Storage dependency. User must have a GCP account and bucket. Consider also supporting local file backup as simpler alternative.

### R2. Backup Schedule
- **Frequency:** Radio: Daily, Weekly, Manual Only
- **Daily:** User-specified time (e.g., 2:00 AM)
- **Weekly:** User-specified day and time (e.g., Sunday 2:00 AM)
- **Default:** Daily
- **Missed Backup:** If computer was off during scheduled time, backup runs on next app startup
- **Background:** Backup runs in background. Non-blocking.

### R3. Manual Backup
- **Button:** "Backup Now" in Settings → Backup
- **Progress:** Progress bar or spinner during backup
- **Completion:** Toast: "Backup complete. [size] uploaded to [bucket name]."
- **In-Progress Indicator:** Status in Settings: "Last backup: [date/time]. Next: [date/time]."

### R4. Restore
- **Trigger:** "Restore from Backup" button in Settings → Backup
- **List Backups:** Shows available backups in bucket with dates and sizes
- **Select:** User selects a backup snapshot
- **Confirmation Dialog:** "This will replace ALL current data (chats, settings, wiki snapshots, artifacts) with the backup from [date]. Current data will be lost. Continue?"
- **Progress:** Progress bar during download and restore
- **Post-Restore:** App may need to restart. "Data restored. Restart now?" button.

## Data
- Backup configuration stored in SQLite settings
- Backup snapshots stored in Google Cloud Storage

## Success/Failure States
- **Backup Success:** Toast: "Backup complete. [size] uploaded."
- **Backup Failure — Auth:** "Backup failed: Could not authenticate with Google Cloud. Check your credentials."
- **Backup Failure — Network:** "Backup failed: Network error. Will retry on next scheduled run."
- **Backup Failure — Bucket:** "Backup failed: Bucket not found or access denied. Check bucket name and permissions."
- **Restore Success:** "Data restored from [date]. Restart to apply."
- **Restore Failure:** "Restore failed: [error]"

## Permissions
- Single-user app. Backup credentials tied to user's GCP account.

## Interactions
- R1 backs up: SQLite DB (all app data), wiki directory (N1), artifact files (F)
- Wiki .md files are also backed up — but user's own git repo (if any) is additional protection
