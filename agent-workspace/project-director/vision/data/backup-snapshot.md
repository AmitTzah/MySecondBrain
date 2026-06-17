# BackupSnapshot — Data Entity (Conceptual)

## Description
A BackupSnapshot represents a backup of all app data (SQLite database, wiki files, artifacts) stored in Google Cloud Storage. This is a conceptual entity — metadata is tracked locally, but the actual backup data lives in GCS.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| createdAt | datetime | Yes | Auto-set | When backup was created |
| sizeBytes | integer | No | Total backup size | Size of all backed-up data |
| type | enum | Yes | Scheduled, Manual | Backup trigger type |
| status | enum | Yes | Complete, Failed, InProgress | Backup status |
| errorMessage | string | No | Only if status=Failed | Error details |
| gcsObjectPath | string | Yes | Path within GCS bucket | Where backup is stored |

## Lifecycle

### Create
- **Scheduled:** Auto-created per R2 schedule. Status = InProgress → Complete/Failed.
- **Manual:** Created when user clicks "Backup Now" (R3).

### Update
- Status updates during backup: InProgress → Complete or Failed.
- Immutable after reaching Complete or Failed.

### Delete
- User deletes old backups from GCS (via Restore screen R4).
- ⚠️ FLAGGED: Backup retention/rotation policy not specified. Should old backups be auto-deleted?

## Relationships
- Standalone entity. Not linked to other data entities.

## UI Visibility
- [`screens/settings.md`](screens/settings.md) — Backup section. Shows last backup time, next scheduled, status.
- Restore screen (R4) — List of available backups in GCS.
