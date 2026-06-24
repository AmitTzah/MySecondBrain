# Database Knowledge — MySecondBrain

> **Global database schemas, models, migrations, and data-layer patterns.**  
> Source: Features W1.1–W1.3 — Solution Scaffold, DI Container, Logging.

---

## 1. Data Layer Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| ORM | Entity Framework Core | 8.x |
| Database | SQLite | via `Microsoft.Data.Sqlite` |
| EF Core SQLite Provider | `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.* |
| ADO.NET Provider | `Microsoft.Data.Sqlite` | 8.0.* (FTS5 support) |
| Migrations Tooling | `Microsoft.EntityFrameworkCore.Design` | 8.0.* (`PrivateAssets=all`) |

---

## 2. AppDbContext Pattern

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 15 DbSet<T> properties — one per entity
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageDrafts> MessageDrafts => Set<MessageDrafts>();
    public DbSet<ModelConfiguration> ModelConfigurations => Set<ModelConfiguration>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<TextAction> TextActions => Set<TextAction>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<WikiFile> WikiFiles => Set<WikiFile>();
    public DbSet<WikiVersionSnapshot> WikiVersionSnapshots => Set<WikiVersionSnapshot>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MySecondBrain", "msb.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // All FK relationships, indexes, unique constraints, and seed data
        // configured via Fluent API. See §8 (FK Delete Behavior) and §9 (Seed Data).
        // WikiVersionSnapshot → WikiFile uses HasPrincipalKey/HasForeignKey
        // because WikiFile uses FilePath (string) as its natural primary key.
    }
}
```

**Key design decisions:**
- Single `DbContext` singleton for single-user desktop app (no concurrency concerns). See [Architecture §16](architecture.md#16-singleton-appdbcontext--lifetime-rationale).
- **Runtime DI registration:** Factory delegate in `App.xaml.cs` creates `DbContextOptions<AppDbContext>` with SQLite path at `%LOCALAPPDATA%\MySecondBrain\msb.db`, auto-creating directory if missing. See [Architecture §3.3](architecture.md#33-appdbcontext-factory-delegate-registration).
- **Design-time fallback:** `OnConfiguring` fallback resolves the same `%LOCALAPPDATA%` path for EF Core tooling (migrations, scaffolding) when no DI-provided options exist.
- **`OnModelCreating`:** Configures all 17 FK relationships, indexes on frequently-queried columns, unique constraints, and seed data via Fluent API. All configuration lives in `OnModelCreating` (no separate `IEntityTypeConfiguration<T>` files).
- **Auto-migration:** `db.Database.Migrate()` is called in `App.xaml.cs` `OnStartup` after DI build. See [Architecture §15](architecture.md#15-startup-lifecycle--database-auto-migration).

### 2.1 Database File Path Convention

| Property | Value |
|----------|-------|
| Base directory | `%LOCALAPPDATA%\MySecondBrain\` |
| Database file | `msb.db` |
| Full path | `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MySecondBrain", "msb.db")` |
| Directory creation | `Directory.CreateDirectory()` called before first use — both in factory delegate and `OnConfiguring` fallback |


---

## 3. Entity Catalog — 15 Entities, 18 FK Relationships

All entities are defined in `MySecondBrain.Data/Entities/` as EF Core entity classes. Each uses `[Key]` attribute on its primary key and `string` GUIDs for PKs (see §3.1). The `InitialCreate` migration created the original 14 tables plus 2 FTS5 virtual tables. A subsequent migration `AddMemoryEntry` added the 15th entity.

| # | Entity | PK | FK Relationships | Notes |
|--|--------|----|-----------------|-------|
|---|--------|----|-----------------|-------|
| 1 | `ApiKey` | `Id` (string GUID) | ← referenced by `ModelConfiguration.ApiKeyId` | CreatedAt timestamp. KeyValue stored encrypted (encryption is service-layer). |
| 2 | `AppSetting` | `Key` (string, MaxLength 256) | — (standalone key-value) | Backs `SettingsRepository`. Value column holds plain strings or JSON-serialized objects. |
| 3 | `Artifact` | `Id` (string GUID) | `ThreadId` → `ChatThread.Id` (required, Cascade) | CreatedAt, UpdatedAt timestamps. |
| 4 | `ChatThread` | `Id` (string GUID) | `PersonaId` → `Persona.Id` (optional, SetNull); `ModelConfigId` → `ModelConfiguration.Id` (optional, SetNull) | 25 properties. Soft-delete via `IsDeleted` + `DeletedAt`. `IsTransient` for auto-cleanup. |
| 5 | `MediaItem` | `Id` (string GUID) | `ThreadId` → `ChatThread.Id` (required, Cascade); `MessageId` → `Message.Id` (optional, SetNull) | Soft-delete via `IsDeleted` + `DeletedAt`. |
| 6 | `Message` | `Id` (string GUID) | `ThreadId` → `ChatThread.Id` (required, Cascade); `PersonaId` → `Persona.Id` (optional, SetNull); `ModelConfigId` → `ModelConfiguration.Id` (optional, SetNull); `ParentMessageId` → `Message.Id` (self-ref, optional, Restrict) | 18 properties. Branching via `BranchId`, `VersionNumber`, `IsActiveBranch`. |
| 7 | `MessageDrafts` | `ThreadId` (string, PK) | — (standalone, one draft per thread) | Lightweight auto-save: `Content`, `CursorPosition`, `SavedAt`. No separate repository. |
| 8 | `ModelConfiguration` | `Id` (string GUID) | `ApiKeyId` → `ApiKey.Id` (optional, SetNull) | CreatedAt, UpdatedAt timestamps. Unique `DisplayName`. Restrict delete if referenced by Personas. |
| 9 | `Persona` | `Id` (string GUID) | `DefaultModelConfigId` → `ModelConfiguration.Id` (optional, Restrict) | CreatedAt, UpdatedAt timestamps. Unique `DisplayName`. 2 built-in personas seeded. |
| 10 | `PromptTemplate` | `Id` (string GUID) | — (standalone) | UpdatedAt timestamp. |
| 11 | `TextAction` | `Id` (string GUID) | `ModelConfigId` → `ModelConfiguration.Id` (optional, SetNull) | CaptureScope + ApplyMode for three-tier capture/transform/apply system. 10 built-in text actions seeded. |
| 12 | `UsageRecord` | `Id` (string GUID) | `MessageId` → `Message.Id` (required, Cascade); `ThreadId` → `ChatThread.Id` (required, Cascade); `PersonaId` → `Persona.Id` (optional, SetNull); `ModelConfigId` → `ModelConfiguration.Id` (optional, SetNull) | Token/cost tracking per message. |
| 13 | `WikiFile` | `FilePath` (string — natural key) | — (referenced by WikiVersionSnapshot) | Content, headings, cross-links. FTS5-indexed for full-text search. |
| 14 | `WikiVersionSnapshot` | `Id` (string GUID) | `WikiFilePath` → `WikiFile.FilePath` (HasPrincipalKey, Cascade) | Snapshot pruning: 30-per-file, 50MB global cap. |
| 15 | **`MemoryEntryEntity`** | `Id` (string GUID) | `SourceThreadId` → `ChatThread.Id` (optional, SetNull) | Key-value memory store. `Key` indexed. `CreatedAt` indexed. Added in migration `AddMemoryEntry`. |

> **Note:** `BackupSnapshot` is planned for W3.16 (Backup & Recovery) and is not yet implemented. It will be a standalone entity (no FK relationships). A future `Skill` entity may be added when skills need persisted configuration.

### FK Relationship Count: 18

The 18 foreign key relationships are configured in `OnModelCreating` via Fluent API. See §8 for the complete OnDelete behavior reference. MemoryEntryEntity.SourceThreadId → ChatThread adds the 18th FK.

### 3.1 Primary Key Convention: String GUIDs

All entity primary keys use `string` typed GUIDs with no dashes:

```csharp
[Key]
public string Id { get; set; } = Guid.NewGuid().ToString("N");
```

This avoids auto-increment integer keys and provides globally unique, URL-safe identifiers across the application without relying on database-generated sequences. The `"N"` format produces 32-character hex strings (no hyphens).

The sole exception is `WikiFile`, which uses `FilePath` (relative wiki path) as its natural primary key.

### 3.2 Entity vs. DTO Separation

EF Core entities (`Data/Entities/`) and domain DTOs/records (`Core/Models/`) are **independent type hierarchies**. They serve different purposes:

| Aspect | Entity (`Data/Entities/`) | DTO/Record (`Core/Models/`) |
|--------|--------------------------|---------------------------|
| Purpose | Persistence (EF Core mapping) | In-memory data transfer between layers |
| Navigation properties | Yes (`ICollection<T>`, FK references) | No — flat records only |
| EF Core attributes | Yes (`[Key]`, `[MaxLength]`) | No |
| Constructors | Parameterless (EF Core requirement) + property initializers | Primary constructor (record) or explicit |
| Where consumed | Repositories, DbContext | Services, ViewModels, API surface |

Services map between entities and DTOs at the repository boundary. Core never references EF Core types.

### 3.3 String-Based Enums for Flexibility

Enum-like values (Provider, Role, lifecycle states) are stored as strings in the database, not as integer-backed .NET enums. This allows adding new values without migrations and provides human-readable database values for debugging. The .NET `enum` types in `Core/Models/Enums.cs` are used for compile-time safety in code; conversion to/from strings happens at the repository boundary.


---

## 4. Repository Pattern Conventions

- **Interface location:** `MySecondBrain.Core/Interfaces/` (e.g., `IChatThreadRepository`)
- **Implementation location:** `MySecondBrain.Data/Repositories/`
- **Injection:** Services depend on repository interfaces (in Core), not on EF Core directly
- **DbContext access:** Repositories receive `AppDbContext` via constructor injection
- **Lifetime:** Singleton (single-user desktop app)
- **DI registration:** `services.AddSingleton<IChatThreadRepository, ChatThreadRepository>()` per repository

### 4.1 Concrete Repository Catalog (8 Repositories)

| Repository | Interface | Primary Entity |
|-----------|-----------|---------------|
| `ChatThreadRepository` | `IChatThreadRepository` | `ChatThread` |
| `MessageRepository` | `IMessageRepository` | `Message` |
| `PersonaRepository` | `IPersonaRepository` | `Persona` |
| `ModelConfigurationRepository` | `IModelConfigurationRepository` | `ModelConfiguration` |
| `ApiKeyRepository` | `IApiKeyRepository` | `ApiKey` |
| `WikiIndexRepository` | `IWikiIndexRepository` | `WikiFile` |
| `UsageRepository` | `IUsageRepository` | `UsageRecord` |
| `SettingsRepository` | `ISettingsRepository` | Application settings (key-value) |

### 4.2 Repository Stub Convention

Repositories are initially created as stubs following the same pattern used across all layers (see [Architecture §4](architecture.md#4-stub-pattern-parallelizable-feature-development)):

```csharp
public class ChatThreadRepository : IChatThreadRepository
{
    private readonly AppDbContext _db;

    public ChatThreadRepository(AppDbContext db) => _db = db;

    public Task<ChatThread?> GetByIdAsync(string id) =>
        Task.FromResult<ChatThread?>(null);

    public Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort) =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    // ... remaining methods follow same stub pattern
}
```

Return type conventions for stubs:
- `T?` → `Task.FromResult<T?>(null)`
- `IReadOnlyList<T>` → `Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>())`
- `int` (count) → `Task.FromResult(0)`
- `void` / no meaningful return → `Task.CompletedTask`

---

## 5. Migrations Strategy

- **Directory:** `MySecondBrain.Data/Migrations/`
- **Tooling:** `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all` (design-time only)
- **Creation:** Migrations will be created by the feature that first defines entity classes
- **Application:** Applied at startup via `dbContext.Database.Migrate()` in DI bootstrap
- **Seeding:** Seed data (default personas, prompt templates) handled via `HasData()` in `IEntityTypeConfiguration<T>` or dedicated seeder classes

---

## 6. Data Layer Project Structure

```
MySecondBrain.Data/
├── MySecondBrain.Data.csproj    # EF Core + SQLite NuGet refs, ProjectRef→Core
├── GlobalUsings.cs
├── AppDbContext.cs               # DbContext with OnConfiguring fallback
├── Entities/                     # EF Core entity classes (15 entities)
├── Configurations/               # IEntityTypeConfiguration<T> classes
├── Repositories/                 # Repository implementations
└── Migrations/                   # EF Core migrations (auto-generated)
```

---

## 7. File-Based Data (Wiki)

Beyond SQLite, wiki content is stored as plain `.md` files:
- Git-versioned via `LibGit2Sharp`
- Wiki metadata (path, title, tags) stored in SQLite via `WikiFile` entity
- Version snapshots tracked in `WikiVersionSnapshot` entity
- Full-text search via SQLite FTS5 (`Microsoft.Data.Sqlite`)

---

## 8. FK Delete Behavior — Complete Reference

All 17 foreign key relationships are configured in `OnModelCreating` with explicit `OnDelete()` behavior. The three behaviors used:

| Behavior | When Applied | Entities |
|----------|-------------|----------|
| **Cascade** | Child has no meaning without parent | Message→ChatThread, Artifact→ChatThread, MediaItem→ChatThread, UsageRecord→Message, UsageRecord→ChatThread, WikiVersionSnapshot→WikiFile |
| **SetNull** | Child can exist independently; FK becomes null | ChatThread→Persona, ChatThread→ModelConfiguration, Message→Persona, Message→ModelConfiguration, ModelConfiguration→ApiKey, MediaItem→Message, UsageRecord→Persona, UsageRecord→ModelConfiguration, TextAction→ModelConfiguration |
| **Restrict** | Delete blocked; must be handled in application code | Message→Message (self-ref, branching), Persona→ModelConfiguration |

### 8.1 Restrict Delete — Application-Level Handling

When `Restrict` is configured, the repository throws `InvalidOperationException` with a descriptive message. Example from `ModelConfigurationRepository.DeleteAsync`:

```csharp
if (config.Personas.Count > 0)
    throw new InvalidOperationException(
        $"Cannot delete ModelConfiguration '{config.DisplayName}' — " +
        $"it is referenced by {config.Personas.Count} Persona(s).");
```

The UI layer catches this exception and shows a warning dialog before attempting the delete, so the user sees a friendly message instead of a crash.

---

## 9. Seed Data — Fixed GUIDs via HasData()

Built-in data is seeded in `OnModelCreating` using `HasData()` with **fixed string GUIDs** (not `Guid.NewGuid()`). Fixed GUIDs ensure deterministic migrations — the migration SQL is identical across all developer machines.

### 9.1 Seeded Personas (2)

| Fixed GUID | DisplayName | SystemPrompt |
|------------|-------------|-------------|
| `00000000000000000000000000000001` | General Assistant | "You are a helpful, thoughtful assistant." |
| `00000000000000000000000000000002` | Code Helper | "You are an expert software developer. Provide clean, well-documented code." |

Both have `DefaultChatMode = "Standard"`, `IsBuiltIn = true`, `CreatedAt = 2026-01-01T00:00:00+00:00`.

### 9.2 Seeded TextActions (10)

| Fixed GUID | DisplayName | CaptureScope | ApplyMode | Hotkey | SystemPrompt (abbreviated) |
|------------|-------------|-------------|-----------|--------|---------------------------|
| `a000000000000000000000000000001` | Rewrite | selection | replaceSelection | Alt+Q | "Rewrite the following text to improve clarity, flow, and impact..." |
| `a000000000000000000000000000002` | Summarize | selection | showOnly | Alt+W | "Summarize the following text concisely, capturing the key points." |
| `a000000000000000000000000000003` | Explain | selection | showOnly | Alt+E | "Explain the following text clearly and thoroughly..." |
| `a000000000000000000000000000004` | Translate | selection | replaceSelection | Alt+R | "Translate the following text to English. Preserve formatting and tone." |
| `a000000000000000000000000000005` | Fix Grammar | selection | replaceSelection | — | "Fix grammar, spelling, and punctuation errors..." |
| `a000000000000000000000000000006` | Enhance Prompt | selection | replaceSelection | — | "Improve the following prompt to be more specific, detailed, and effective..." |
| `a000000000000000000000000000007` | Continue Writing | focusedElement | insertAtCursor | Alt+C | "Continue writing from where the text left off..." |
| `a000000000000000000000000000008` | Improve Flow | focusedElement | replaceFocusedElement | — | "Rewrite the following text to improve logical flow, transitions..." |
| `a000000000000000000000000000009` | Summarize Page | fullDocument | showOnly | — | "Summarize the following content concisely, capturing the key points and overall structure." |
| `a000000000000000000000000000010` | Explain Screen | fullDocument,screenshot | showOnly | — | "Explain what is shown in the provided content..." |

All have `IsBuiltIn = true`, `CreatedAt = 2026-01-01T00:00:00+00:00`.

### 9.3 Seed Data Pattern

```csharp
modelBuilder.Entity<Persona>().HasData(
    new Persona
    {
        Id = "00000000000000000000000000000001",  // FIXED — not Guid.NewGuid()
        DisplayName = "General Assistant",
        SystemPrompt = "You are a helpful, thoughtful assistant.",
        DefaultChatMode = "Standard",
        IsBuiltIn = true,
        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    },
    // ...
);
```

**Rule:** Every seed entity must have an explicit, fixed `Id`. Using `Guid.NewGuid()` in `HasData()` would produce a different migration SQL on every run, breaking determinism.

---

## 10. FTS5 Full-Text Search — Virtual Tables with Content-Sync Triggers

Full-text search is powered by SQLite FTS5 virtual tables created in the `InitialCreate` migration via raw SQL in `migrationBuilder.Sql()`.

### 10.1 FTS5 Virtual Tables

Two FTS5 virtual tables, each with `content=` pointing to the source table:

| Virtual Table | Content Table | Indexed Column |
|---------------|---------------|----------------|
| `MessageFts` | `Messages` | `Content` |
| `WikiFileFts` | `WikiFiles` | `Content` |

### 10.2 Content-Sync Triggers (6 total)

Each content table gets three triggers that keep the FTS5 index in sync:

| Trigger | Fires On | Action |
|---------|----------|--------|
| `Messages_AI` / `WikiFiles_AI` | AFTER INSERT | Insert new rowid + Content into FTS5 |
| `Messages_AD` / `WikiFiles_AD` | AFTER DELETE | Delete old rowid from FTS5 |
| `Messages_AU` / `WikiFiles_AU` | AFTER UPDATE | Delete old + insert new (replace semantics) |

### 10.3 FTS5 Creation SQL (in Migration Up())

```csharp
migrationBuilder.Sql(@"
    CREATE VIRTUAL TABLE IF NOT EXISTS MessageFts USING fts5(
        Content,
        content=Messages,
        content_rowid=rowid
    );
    CREATE TRIGGER IF NOT EXISTS Messages_AI AFTER INSERT ON Messages BEGIN
        INSERT INTO MessageFts(rowid, Content) VALUES (new.rowid, new.Content);
    END;
    -- ... AD, AU triggers ...
", suppressTransaction: true);
```

`suppressTransaction: true` is required because `CREATE VIRTUAL TABLE` cannot run inside a transaction.

### 10.4 FTS5 Search Query Pattern

FTS5 queries use `FromSqlRaw` with parameterized queries to prevent FTS5 query injection:

```csharp
var results = await _db.Messages
    .FromSqlRaw(@"
        SELECT m.* FROM Messages m
        INNER JOIN MessageFts fts ON m.rowid = fts.rowid
        WHERE MessageFts MATCH {0}
        ORDER BY rank
        LIMIT {1}", query, maxResults)
    .AsNoTracking()
    .ToListAsync();
```

The JOIN on `rowid` links the FTS5 virtual table back to the content table. `ORDER BY rank` returns the most relevant results first.

---

## 11. Soft-Delete Pattern — ChatThread & MediaItem

Two entities support soft-delete (mark as deleted without removing data). Both use the same column pattern:

```csharp
public bool IsDeleted { get; set; }
public DateTimeOffset? DeletedAt { get; set; }
```

### 11.1 Soft-Delete in Repository

```csharp
public async Task SoftDeleteAsync(string id)
{
    var thread = await _db.ChatThreads.FindAsync(id);
    if (thread == null) return;
    thread.IsDeleted = true;
    thread.DeletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync();
}
```

### 11.2 Query Filtering

All list queries that return "active" items must filter out soft-deleted records:

```csharp
// Get all permanent (non-transient, non-deleted) threads
_db.ChatThreads.Where(t => !t.IsTransient && !t.IsDeleted)
```

Trash queries filter the inverse:

```csharp
_db.ChatThreads.Where(t => t.IsDeleted).OrderBy(t => t.DeletedAt)
```

---

## 12. SQLite DateTimeOffset Limitations

SQLite has no native `DateTimeOffset` type. EF Core stores `DateTimeOffset` as a string in ISO 8601 format. This has two consequences:

### 12.1 No Server-Side DateTimeOffset Filtering

LINQ queries that filter on `DateTimeOffset` properties (e.g., `.Where(t => t.CreatedAt > cutoff)`) may not translate to SQL correctly in all EF Core + SQLite versions. When EF Core cannot translate the expression, it falls back to **client-side evaluation**, which downloads the entire table and filters in memory.

**Mitigation:** For performance-critical date queries on large tables, consider storing Unix timestamps (long) alongside DateTimeOffset, or using raw SQL with string comparison on ISO 8601 values.

### 12.2 UTC Convention

All `DateTimeOffset` values must be stored in UTC (`DateTimeOffset.UtcNow`). The application layer converts to local time for display. This avoids ambiguity when the user changes time zones.

---

## 13. WikiVersionSnapshot Pruning — 30-per-file, 50MB Cap

Snapshot storage is bounded to prevent unbounded disk usage:

### 13.1 Per-File Limit (30 snapshots)

```csharp
var snapshots = await _db.WikiVersionSnapshots
    .Where(s => s.WikiFilePath == filePath)
    .OrderByDescending(s => s.CreatedAt)
    .ToListAsync();

if (snapshots.Count > 30)
{
    var toRemove = snapshots.Skip(30);  // Keep newest 30
    _db.WikiVersionSnapshots.RemoveRange(toRemove);
}
```

### 13.2 Global Storage Cap (50 MB)

```csharp
var totalBytes = await _db.WikiVersionSnapshots
    .SumAsync(s => (long)s.Content.Length);

if (totalBytes > 50_000_000)  // 50 MB
{
    var oldest = await _db.WikiVersionSnapshots
        .OrderBy(s => s.CreatedAt)
        .Take(/* estimate how many to remove */)
        .ToListAsync();
    _db.WikiVersionSnapshots.RemoveRange(oldest);
}
```

Both checks run inside `PruneSnapshotsAsync()` in `WikiIndexRepository`. The method is called after each new snapshot is created.

---

## 14. MessageDrafts — Per-Thread Auto-Save Entity

`MessageDrafts` is a lightweight entity with no FK relationships and no separate repository. It stores a single draft per thread (thread ID is the PK):

```csharp
public class MessageDrafts
{
    [Key]
    public string ThreadId { get; set; } = string.Empty;  // PK = thread ID

    public string Content { get; set; } = string.Empty;    // Current textbox content
    public int CursorPosition { get; set; }                  // Cursor position in textbox
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

**Access pattern:** Read/write directly via `AppDbContext.MessageDrafts` (no repository). A future `DraftService` will manage auto-save timing and debouncing. One row per chat thread — upsert on save (find-then-add-or-update).

---

## 15. AppSetting Entity — Key-Value Settings Store

`AppSetting` backs `SettingsRepository` with a simple key-value model:

```csharp
public class AppSetting
{
    [Key]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
```

Exposed via `public DbSet<AppSetting> Settings => Set<AppSetting>();` on `AppDbContext`.

### 15.1 SettingsRepository — JSON Serialization

`SettingsRepository` wraps `AppSetting` with typed get/set operations:

- `GetAsync(string key)` → returns raw `Value` string or null
- `GetAsync<T>(string key)` → deserializes `Value` JSON to `T`
- `SetAsync(string key, string value)` → upserts `AppSetting` row
- `SetAsync<T>(string key, T value)` → serializes `T` to JSON, then upserts

Uses `System.Text.Json` for serialization. Find-then-add-or-update pattern via `_db.Settings.FindAsync(key)` followed by `_db.Settings.Add()` or `_db.Entry(existing).CurrentValues.SetValues()`.

No raw SQL needed — standard EF Core CRUD operations on the `AppSetting` entity.

---

## 16. Theme/Font Settings Key Conventions

`SettingsRepository` (backed by `AppSetting` entity, see [§15](#15-appsetting-entity--key-value-settings-store)) stores theme and font preferences as key-value pairs. These keys are read at startup in `App.xaml.cs` and written by `WpfThemeProvider` on every change.

### 16.1 Persisted Keys

| Key | Type | Example Value | Written By | Read At |
|-----|------|--------------|------------|---------|
| `"AppTheme"` | `string` | `"Light"`, `"Dark"` | `WpfThemeProvider.SetAppTheme()` | `App.xaml.cs` OnStartup |
| `"ChatTheme"` | `string` | `"Classic"`, `"Compact"`, `"Bubble"` | `WpfThemeProvider.SetChatTheme()` | — (applied on next theme change via ViewModel) |
| `"FontFamily"` | `string` | `"Segoe UI"`, `"Cascadia Code"` | `WpfThemeProvider.SetFontSettings()` | `App.xaml.cs` OnStartup |
| `"FontSize"` | `string` | `"14"`, `"18.5"` | `WpfThemeProvider.SetFontSettings()` | `App.xaml.cs` OnStartup |
| `"FontWeight"` | `string` | `"Normal"`, `"Bold"`, `"SemiBold"` | `WpfThemeProvider.SetFontSettings()` | `App.xaml.cs` OnStartup |

### 16.2 FontWeight Persistence — FontWeightConverter

`FontWeight` is a WPF struct (not an enum), so it cannot be round-tripped via `Enum.Parse`. The persistence layer uses `FontWeightConverter`:

**Save:**
```csharp
var weightString = fontWeight.ToString();  // e.g., "Normal", "Bold", "SemiBold"
await _settings.SetAsync("FontWeight", weightString);
```

**Restore:**
```csharp
var saved = await _settings.GetAsync("FontWeight");
var fontWeight = FontWeights.Normal;  // fallback default
if (saved is not null)
{
    try
    {
        var converter = new FontWeightConverter();
        var result = converter.ConvertFromString(saved);
        if (result is FontWeight fw)
            fontWeight = fw;
    }
    catch { /* fallback to FontWeights.Normal */ }
}
```

This pattern applies to any WPF value type that has an associated `TypeConverter`. The converter lives in `System.ComponentModel` and is discovered via `TypeDescriptor`.

### 16.3 FontSize Clamping

`FontSize` is validated in `WpfThemeProvider.SetFontSettings()` to the range **10–24px** (vision spec A3 constraint). Values outside this range throw `ArgumentOutOfRangeException`. The persisted string uses `CultureInfo.InvariantCulture` formatting:

```csharp
fontSize.ToString(CultureInfo.InvariantCulture)  // "14.5", not "14,5"
```

### 16.4 Startup Restore Pattern

In `App.xaml.cs` `OnStartup`, after DI build and migration:

```csharp
var themeProvider = _serviceProvider.GetRequiredService<IThemeProvider>();
var settings = _serviceProvider.GetRequiredService<ISettingsRepository>();

// 1. Restore theme (if saved; otherwise App.xaml Light default stays)
var savedTheme = await settings.GetAsync("AppTheme");
if (savedTheme is not null && Enum.TryParse<AppTheme>(savedTheme, out var theme))
    themeProvider.SetAppTheme(theme);

// 2. Restore font (family, size, AND weight — all three together)
var savedFontFamily = await settings.GetAsync("FontFamily");
var savedFontSize = await settings.GetAsync("FontSize");
var savedFontWeight = await settings.GetAsync("FontWeight");
var fontSize = 14.0;
var fontWeight = FontWeights.Normal;
if (savedFontSize is not null)
    double.TryParse(savedFontSize, NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize);
if (savedFontWeight is not null)
{
    try
    {
        var converter = new FontWeightConverter();
        var result = converter.ConvertFromString(savedFontWeight);
        if (result is FontWeight fw) fontWeight = fw;
    }
    catch { }
}
if (savedFontFamily is not null)
    themeProvider.SetFontSettings(savedFontFamily, fontSize, fontWeight);
```

**Ordering:** Theme restore happens BEFORE font restore. This ensures font resources are set on top of the correct theme dictionary. If font restore ran first and then theme swap cleared the dictionaries, font settings would be lost.

---

## 17. Platform Infrastructure AppSetting Keys (Feature 6)

Feature 6 introduced two new `AppSetting` keys that enable platform infrastructure services. Both keys use the standard `SettingsRepository` pattern defined in [§15](#15-appsetting-entity--key-value-settings-store).

### 17.1 Key Catalog

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `"WebSocketAuthToken"` | `string` | Auto-generated (64-char uppercase hex) | WebSocket authentication token for Kestrel server |
| `"MinimizeToTray"` | `string` | `"true"` | Minimize to system tray on window close (boolean stored as string) |

### 17.2 WebSocketAuthToken — Auto-Generation on First Run

The auth token is generated once on first launch and persisted permanently:

```csharp
// In KestrelWebSocketServer constructor:
var existingToken = await _settings.GetAsync("WebSocketAuthToken");
if (string.IsNullOrEmpty(existingToken))
{
    var bytes = RandomNumberGenerator.GetBytes(32);       // 32 bytes = 256 bits
    _authToken = Convert.ToHexString(bytes);               // 64-char uppercase hex
    await _settings.SetAsync("WebSocketAuthToken", _authToken);
}
else
{
    _authToken = existingToken;                            // Reuse existing token
}
```

**Key properties:**
- **Length:** 64-character uppercase hexadecimal string (32 bytes × 2 hex chars/byte)
- **Entropy:** 256 bits, cryptographically random via `System.Security.Cryptography.RandomNumberGenerator`
- **Lifetime:** Generated once, never rotated automatically. Regeneration is user-initiated via Settings UI (Feature 8)
- **Storage:** Plain text in SQLite `AppSettings` table. No encryption needed — the token only authenticates loopback connections

### 17.3 MinimizeToTray — Read Pattern in MainWindow.OnClosing

The minimize-to-tray setting controls whether clicking the X button hides the window to the system tray or fully exits the application:

```csharp
// In MainWindow.xaml.cs OnClosing:
var settings = App.Current.Services.GetRequiredService<ISettingsRepository>();
var minimizeToTray = await settings.GetAsync("MinimizeToTray") ?? "true";

if (minimizeToTray == "true" && _trayService.IsVisible)
{
    e.Cancel = true;     // Prevent window close
    this.Hide();          // Hide to tray
}
else
{
    Application.Current.Shutdown();  // Full exit
}
```

**Key properties:**
- **Default:** `"true"` (minimize to tray is the default behavior)
- **Read path:** `ISettingsRepository.GetAsync("MinimizeToTray")` with `?? "true"` fallback
- **Write path:** Settings UI (Feature 8) writes via `ISettingsRepository.SetAsync("MinimizeToTray", value)`
- **Override:** The `ExitRequested` event from `ISystemTrayService` bypasses this check and always fully exits

### 17.4 SettingsRepository Access Pattern (Consistent with Existing Keys)

Both keys follow the same find-then-upsert pattern used by theme/font keys in [§16](#16-themefont-settings-key-conventions):

| Operation | Pattern |
|-----------|---------|
| **Read** | `await settings.GetAsync(key)` → returns `string?` (null if not set) |
| **Write** | `await settings.SetAsync(key, value)` → upserts the `AppSetting` row |
| **Default fallback** | `?? "defaultValue"` at the call site, not in the repository |

The repository's `SetAsync` method uses `_db.Settings.FindAsync(key)` followed by `_db.Settings.Add()` or `_db.Entry(existing).CurrentValues.SetValues()` — the same upsert pattern documented in [§15.1](#151-settingsrepository--json-serialization). No raw SQL is needed.

### 17.5 Future Platform Keys

Additional platform infrastructure keys expected in future features:

| Key | Feature | Purpose |
|-----|---------|---------|
| `"HotkeyAssignments"` | Feature 13 (Text Actions) | JSON-serialized hotkey → action mapping, overrides default seed-data hotkeys |
| `"UpdateFeedUrl"` | Feature 8 (Settings) | User-configurable auto-update feed URL, overriding the hardcoded default |
| `"WebSocketPort"` | Feature 13 (Word Add-in) | User-specified preferred port for the Kestrel WebSocket server |

---

## 18. Entity Field Completion Reference — ApiKey, ModelConfiguration, Persona

Feature 7 (Model Configurations, API Keys & Personas) completed these three entities with their full field set. The fields below are the complete canonical schema for each entity.

### 18.1 ApiKey — Complete Field Schema

| Field | Type | Required | Default | Notes |
|-------|------|----------|---------|-------|
| `Id` | `string` (GUID, PK) | Yes | `Guid.NewGuid().ToString("N")` | 32-char hex, no dashes |
| `DisplayName` | `string` (≤100) | Yes | — | User-friendly label |
| `Provider` | `string` (enum as string) | Yes | — | One of: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAICompatible |
| `KeyValue` | `string` | Yes | — | DPAPI-encrypted ciphertext (Base64). Must be `[REDACTED]` in all log output. |
| `CustomProviderName` | `string?` | No | `null` | Display name for OpenAI-Compatible custom provider |
| `CustomEndpointUrl` | `string?` | No | `null` | Base URL for OpenAI-Compatible provider (e.g., `http://localhost:11434/v1`) |
| `IsValid` | `bool` | Yes | `false` | Set by Test Key validation. `true` = last validation succeeded. |
| `LastTestedAt` | `DateTimeOffset?` | No | `null` | Timestamp of last successful validation |
| `CreatedAt` | `DateTimeOffset` | Yes | `DateTimeOffset.UtcNow` | Immutable creation timestamp |

**FK relationships:** `ApiKey` is referenced by `ModelConfiguration.ApiKeyId` (SetNull on delete).

### 18.2 ModelConfiguration — Complete Field Schema

| Field | Type | Required | Default | Notes |
|-------|------|----------|---------|-------|
| `Id` | `string` (GUID, PK) | Yes | `Guid.NewGuid().ToString("N")` | 32-char hex, no dashes |
| `DisplayName` | `string` (≤100) | Yes | — | Unique. User-friendly label (e.g., "GPT-4o Fast"). |
| `Provider` | `string` (enum as string) | Yes | — | Same enum as ApiKey.Provider |
| `ModelIdentifier` | `string` (≤200) | Yes | — | Provider-specific model ID (e.g., `gpt-4o`, `claude-sonnet-4-20250514`) |
| `ApiKeyId` | `string?` (FK) | No | `null` | FK to `ApiKey`. SetNull on ApiKey delete. |
| `Temperature` | `double` | Yes | `1.0` | Range 0.0–2.0. Controls response randomness. |
| `MaxOutputTokens` | `int` | Yes | `4096` | Maximum tokens in the generated response |
| `MaxContextWindow` | `int` | Yes | `128000` | Maximum context window size (total input tokens). Provider-level limit. |
| `ThinkingEnabled` | `bool` | Yes | `false` | Whether extended thinking/reasoning is enabled |
| `PricingInputPer1K` | `decimal?` | No | `null` | Cost per 1,000 input tokens (USD) |
| `PricingOutputPer1K` | `decimal?` | No | `null` | Cost per 1,000 output tokens (USD) |
| `ContextOverflowStrategy` | `string` | Yes | `"SlidingWindow"` | One of: SlidingWindow, HardStop, AutoSummarize |
| `CreatedAt` | `DateTimeOffset` | Yes | `DateTimeOffset.UtcNow` | Immutable creation timestamp |
| `UpdatedAt` | `DateTimeOffset` | Yes | `DateTimeOffset.UtcNow` | Updated on every save |

**FK relationships:**
- `ApiKeyId` → `ApiKey.Id` (optional, SetNull)
- Referenced by `Persona.DefaultModelConfigId` (Restrict — delete blocked if any Persona references)
- Referenced by `ChatThread.ModelConfigId` (optional, SetNull)
- Referenced by `Message.ModelConfigId` (optional, SetNull)
- Referenced by `TextAction.ModelConfigId` (optional, SetNull)
- Referenced by `UsageRecord.ModelConfigId` (optional, SetNull)

### 18.3 Persona — Complete Field Schema

| Field | Type | Required | Default | Notes |
|-------|------|----------|---------|-------|
| `Id` | `string` (GUID, PK) | Yes | `Guid.NewGuid().ToString("N")` | 32-char hex. Built-in personas use fixed GUIDs. |
| `DisplayName` | `string` (≤100) | Yes | — | Unique. User-friendly label. |
| `SystemPrompt` | `string` | Yes | — | Full system prompt. Supports `{{date}}`, `{{time}}`, `{{user_name}}` variables. |
| `DefaultModelConfigId` | `string?` (FK) | No | `null` | FK to `ModelConfiguration`. The persona's default engine. |
| `DefaultChatMode` | `string` | Yes | `"Standard"` | One of: Standard, TextCompletion |
| `IsBuiltIn` | `bool` | Yes | `false` | `true` for the 2 seeded personas (General Assistant, Code Helper) |
| `CreatedAt` | `DateTimeOffset` | Yes | `DateTimeOffset.UtcNow` | Immutable creation timestamp |
| `UpdatedAt` | `DateTimeOffset` | Yes | `DateTimeOffset.UtcNow` | Updated on every save |

**FK relationships:**
- `DefaultModelConfigId` → `ModelConfiguration.Id` (optional, Restrict — cannot delete ModelConfig if any Persona references it)
- Referenced by `ChatThread.PersonaId` (optional, SetNull)
- Referenced by `Message.PersonaId` (optional, SetNull)
- Referenced by `UsageRecord.PersonaId` (optional, SetNull)

---

## 19. Two-Layer FK Chain — Delete Behavior Reference

The ApiKey → ModelConfiguration → Persona chain has cascading delete implications that span three entities:

```
ApiKey (SetNull) → ModelConfiguration (Restrict) → Persona
```

| Delete Target | Effect on ModelConfiguration | Effect on Persona | Application Behavior |
|---------------|------------------------------|-------------------|---------------------|
| **ApiKey** | `ApiKeyId` set to `null` (EF Core SetNull) | No direct effect | Model configs survive without a key. UI warns: "Any Model Configurations using this key will need a new key." |
| **ModelConfiguration** | — | Blocked if any Persona references it (EF Core Restrict) | Repository throws `InvalidOperationException` with count of referencing Personas. UI shows confirmation with count before attempting delete. |
| **Persona** | No effect | — | Simple delete. ChatThreads/ Messages referencing this persona get their `PersonaId` set to `null` (SetNull). |

### 19.1 Repository Delete Guard Pattern

When `Restrict` is configured, the repository must check and throw before calling `SaveChanges()`:

```csharp
// In ModelConfigurationRepository.DeleteAsync
public async Task DeleteAsync(string id)
{
    var config = await _db.ModelConfigurations
        .Include(c => c.Personas)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (config is null) return;

    if (config.Personas.Count > 0)
        throw new InvalidOperationException(
            $"Cannot delete ModelConfiguration '{config.DisplayName}' — " +
            $"it is referenced by {config.Personas.Count} Persona(s).");

    _db.ModelConfigurations.Remove(config);
    await _db.SaveChangesAsync();
}
```

The UI layer catches this exception in the ViewModel and shows a warning dialog via `IConfirmationService` before attempting the delete.

---

## 20. Pricing Fields Convention

Model configurations store per-1K token pricing as `decimal?` (nullable) in USD. These are user-provided values for cost tracking, not fetched from provider APIs.

### 20.1 Field Schema

| Field | Type | Unit | Example |
|-------|------|------|---------|
| `PricingInputPer1K` | `decimal?` | USD per 1,000 input tokens | `0.0025` (GPT-4o input) |
| `PricingOutputPer1K` | `decimal?` | USD per 1,000 output tokens | `0.0100` (GPT-4o output) |

### 20.2 Cost Calculation Pattern

```csharp
// UsageRecord stores token counts; cost is calculated at query time
public decimal? CalculateCost(int inputTokens, int outputTokens, ModelConfiguration config)
{
    if (config.PricingInputPer1K is null || config.PricingOutputPer1K is null)
        return null;

    var inputCost = (inputTokens / 1000.0m) * config.PricingInputPer1K.Value;
    var outputCost = (outputTokens / 1000.0m) * config.PricingOutputPer1K.Value;
    return inputCost + outputCost;
}
```

### 20.3 Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Per-1K, not per-token** | Provider pricing pages display per-1K-token prices. Storing per-1K avoids floating-point precision issues with per-token values like `0.0000025`. |
| **Nullable** | Pricing is optional. Users may not know or care about pricing. `null` means "cost not tracked" rather than "cost is zero." |
| **Not auto-fetched** | No provider API returns pricing information. Pricing is user-provided and manually maintained. |

---

## 21. New SettingsRepository Keys (Feature 7)

Feature 7 introduces one new persistent settings key:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `"RecentPersonaIds"` | `List<string>` (JSON) | `[]` | Ordered list of recently-used persona IDs. Max 5 entries. New selection pushes to front, oldest removed if >5. |

### 21.1 RecentPersonaIds — Read/Write Pattern

```csharp
// Read: deserialize from JSON
var recentIds = await _settings.GetAsync<List<string>>("RecentPersonaIds") ?? [];

// Write after persona selection:
recentIds.Remove(id);           // Remove if already present (avoids duplicates)
recentIds.Insert(0, id);        // Push to front
if (recentIds.Count > 5)
    recentIds = recentIds.Take(5).ToList();
await _settings.SetAsync("RecentPersonaIds", recentIds);
```

### 21.2 Usage in PersonaList Sorting

```csharp
// In ChatThreadViewModel: sort personas by recently-used
var allPersonas = await _personaRepo.GetAllAsync();
var recentIds = await _settingsRepo.GetAsync<List<string>>("RecentPersonaIds") ?? [];

var sorted = allPersonas
    .OrderByDescending(p => recentIds.IndexOf(p.Id)) // -1 sorts last
    .ThenBy(p => p.DisplayName)
    .ToList();
```

### 21.3 Settings Key Registry Update

The complete `AppSetting` key catalog (extending §16 and §17) now includes:

| Key | Type | Feature | Purpose |
|-----|------|---------|---------|
| `"AppTheme"` | `string` | W1.3 (Shell) | Theme: Light / Dark |
| `"ChatTheme"` | `string` | W1.3 (Shell) | Chat visual theme: Classic / Compact / Bubble |
| `"FontFamily"` | `string` | W1.3 (Shell) | Font family name |
| `"FontSize"` | `string` | W1.3 (Shell) | Font size 10–24px |
| `"FontWeight"` | `string` | W1.3 (Shell) | Font weight: Normal / Bold / SemiBold |
| `"WebSocketAuthToken"` | `string` | W1.6 (Platform) | 64-char hex for Kestrel WS auth |
| `"MinimizeToTray"` | `string` | W1.6 (Platform) | Minimize-to-tray toggle |
| `"RecentPersonaIds"` | `List<string>` (JSON) | W3.7 (Model Configs) | Recently-used persona IDs (max 5) |

---

## 22. Context Overflow Strategy Convention

When the conversation exceeds the model's `MaxContextWindow`, the `ContextOverflowStrategy` determines how the application handles overflowed tokens.

### 22.1 Strategy Options

| Strategy | Stored Value | Behavior |
|----------|-------------|----------|
| **Sliding Window** | `"SlidingWindow"` | Drop oldest messages until the context fits within the window. Preserves system prompt. Default. |
| **Hard Stop** | `"HardStop"` | Return an error when context is exceeded. User must manually prune conversation or switch to a larger model. |
| **Auto-Summarize** | `"AutoSummarize"` | Summarize older messages via an additional API call, then continue. Incurs extra token cost. |

### 22.2 Storage Convention

All three strategies are stored as strings in the database, not as integer-backed enums. This allows adding new strategies without migrations. The .NET `ContextOverflowStrategy` enum (in Core/Models/Enums.cs) provides compile-time safety; conversion to/from string happens at the repository boundary (same pattern as §3.3 String-Based Enums).

### 22.3 Default

`"SlidingWindow"` is the default for all new `ModelConfiguration` entities. Built-in personas use SlidingWindow via their default model config.

---

## 23. TextAction Entity & ITextActionRepository Pattern

The `TextAction` entity defines three-tier capture/transform/apply actions triggered by global hotkeys. Feature 8 introduces the `ITextActionRepository` interface and its implementation for CRUD operations on text actions.

### 23.1 TextAction Entity Schema

| Field | Type | Required | Default | Notes |
|-------|------|----------|---------|-------|
| `Id` | `string` (GUID, PK) | Yes | `Guid.NewGuid().ToString("N")` | 32-char hex. Built-in actions use fixed GUIDs (`a000...`). |
| `DisplayName` | `string` | Yes | — | User-visible name (e.g., "Rewrite", "Summarize") |
| `SystemPrompt` | `string` | Yes | — | The AI system prompt. Supports `{{variables}}`. |
| `CaptureScope` | `string` | Yes | `"selection"` | Comma-separated flags: `selection`, `focusedElement`, `surroundingContext`, `fullDocument`, `screenshot` |
| `ApplyMode` | `string` | Yes | `"showOnly"` | One of: `replaceSelection`, `showOnly`, `insertAtCursor`, `replaceFocusedElement`, `appendToDocument`, `copyToClipboard`, `newChatTab` |
| `ModelConfigId` | `string?` (FK) | No | `null` | FK to `ModelConfiguration`. SetNull on delete. |
| `AssignedHotkey` | `string?` | No | `null` | e.g., `"Alt+Q"`. Assigned by user in Hotkeys settings. |
| `IsBuiltIn` | `bool` | Yes | `false` | `true` for the 10 seeded text actions |
| `CreatedAt` | `DateTimeOffset` | Yes | `DateTimeOffset.UtcNow` | Immutable creation timestamp |

### 23.2 ITextActionRepository Interface

```csharp
// Defined in Core/Interfaces/ITextActionRepository.cs
public interface ITextActionRepository
{
    Task<IReadOnlyList<TextAction>> GetAllAsync();
    Task<TextAction?> GetByIdAsync(string id);
    Task<TextAction> CreateAsync(TextAction action);
    Task UpdateAsync(TextAction action);
    Task DeleteAsync(string id);
    Task<TextAction> DuplicateAsync(string id);
}
```

### 23.3 Duplicate Pattern

`DuplicateAsync` creates a copy with a new GUID and `" (Copy)"` appended to `DisplayName`. All other fields (SystemPrompt, CaptureScope, ApplyMode, ModelConfigId) are copied verbatim. The `AssignedHotkey` is set to `null` on the duplicate to avoid hotkey conflicts.

### 23.4 CaptureScope and ApplyMode as String Arrays

Both fields are stored as comma-separated strings in SQLite (e.g., `"selection,focusedElement"`). In the ViewModel, they are split into bool properties for checkbox binding. The conversion happens in `TextActionDisplayItem`:

```csharp
public List<string> CaptureScopes => CaptureScope?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];
public bool CaptureSelection => CaptureScopes.Contains("selection");
public bool CaptureFocusedElement => CaptureScopes.Contains("focusedElement");
// ... etc.
```

### 23.5 Built-In Text Actions (10)

10 text actions are seeded via `HasData()` in `OnModelCreating` with fixed GUIDs (`a000000000000000000000000000001` through `a000000000000000000000000000010`). Five have default hotkey assignments (Alt+Q/W/E/R/C). Built-in actions cannot be deleted — the UI hides the delete button when `IsBuiltIn = true`.

### 23.6 FK Relationship

```
TextAction.ModelConfigId → ModelConfiguration.Id (optional, SetNull)
```

Deleting a `ModelConfiguration` sets the FK to `null` on any referencing `TextAction`. The text action survives and uses no specific model until reassigned.

---

## 24. AppSetting Keys — Complete Feature 8 Registry

Feature 8 adds ~30 new `AppSetting` keys for settings persistence. Combined with existing keys, the complete key-value settings store now spans 40+ keys.

### 24.1 Feature 8 — New Settings Keys (34 keys)

#### Diagnostics (9 keys)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `LogLevel` | `string` | `"Information"` | Minimum log level: Information, Debug, Verbose |
| `LogCategory_LLMApiCalls` | `string` | `"true"` | LLM/Provider API call logging |
| `LogCategory_Tier1HotkeyPipeline` | `string` | `"true"` | Tier 1 hotkey pipeline logging |
| `LogCategory_Tier2CommandBar` | `string` | `"true"` | Tier 2 command bar logging |
| `LogCategory_Database` | `string` | `"false"` | Database/SQL/EF Core logging |
| `LogCategory_WikiFileSystem` | `string` | `"false"` | Wiki file system logging |
| `LogCategory_WebSocket` | `string` | `"false"` | WebSocket/Kestrel logging |
| `LogCategory_StartupShutdown` | `string` | `"false"` | App startup/shutdown logging |
| `LogCategory_SystemIntegration` | `string` | `"false"` | System tray/update integration |

#### Appearance (no new keys — uses existing §16 keys)
Existing keys: `AppTheme`, `ChatTheme`, `FontFamily`, `FontSize`, `FontWeight`.

#### Notifications (3 keys)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SoundOnCompletion` | `string` | `"false"` | Play sound when AI response completes |
| `DisableStreaming` | `string` | `"false"` | Disable token-by-token streaming |
| `CrossTabCompletionAlert` | `string` | `"true"` | Show alert when a background tab completes |

#### Startup (2 keys)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `LaunchOnWindowsStartup` | `string` | `"false"` | Add shortcut to Windows startup |
| `RestoreLastSession` | `string` | `"false"` | Restore open chat tabs on launch |

#### Updates (1 key)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `UpdateCheckFrequency` | `string` | `"OnStartup"` | One of: OnStartup, Daily, Weekly, ManualOnly |

#### Language (1 key)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `AutoDetectRtl` | `string` | `"true"` | Auto-detect RTL text (Hebrew U+0590–U+05FF) |

#### Maintenance (1 key)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `LastCompaction` | `string` | `null` | ISO 8601 timestamp of last VACUUM |

#### Wiki (2 keys)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `WikiDirectoryPath` | `string` | `null` | Absolute path to wiki root directory |
| `GitVersionControlEnabled` | `string` | `"false"` | Enable git version control for wiki |

#### Backup (1 key)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `BackupSchedule` | `string` | `"Daily"` | One of: Daily, Weekly, ManualOnly |

#### Tools (6 keys)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `WebSearchAutoApproval` | `string` | `"Ask"` | One of: Ask, AutoApprove, Disabled |
| `TerminalAutoApproval` | `string` | `"Ask"` | Always "Ask" — cannot be AutoApproved |
| `FileGenerateAutoApproval` | `string` | `"Ask"` | One of: Ask, AutoApprove, Disabled |
| `FileEditAutoApproval` | `string` | `"Ask"` | One of: Ask, AutoApprove, Disabled |
| `SttProvider` | `string` | `"OpenAI Whisper"` | Speech-to-text provider |
| `SttModel` | `string` | `null` | STT model identifier |

#### Pricing (3 keys)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MonthlyBudgetLimit` | `string` | `null` | Monthly budget cap in USD (null = unlimited) |
| `WarningThreshold` | `string` | `"80"` | Percentage threshold for budget warning (50–100) |
| `BlockApiOnLimit` | `string` | `"false"` | Block API calls when budget exceeded |

#### Security (1 key)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `HideLockedChats` | `string` | `"false"` | Hide password-locked chats from thread list |

#### Onboarding (5 keys)
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Onboarding_Step1_Completed` | `string` | `null` | API Keys step completed (null = not done) |
| `Onboarding_Step2_Completed` | `string` | `null` | Persona step completed |
| `Onboarding_Step3_Completed` | `string` | `null` | Wiki Directory step completed |
| `Onboarding_Step4_Completed` | `string` | `null` | Hotkeys step completed |
| `Onboarding_Completed` | `string` | `null` | All steps completed (set when wizard finishes) |

### 24.2 Existing Keys (unchanged, 8 keys)

| Key | Feature | Purpose |
|-----|---------|---------|
| `AppTheme` | W1.3 Shell | Theme: Light / Dark |
| `ChatTheme` | W1.3 Shell | Chat theme: Classic / Compact / Bubble |
| `FontFamily` | W1.3 Shell | Font family name |
| `FontSize` | W1.3 Shell | Font size 10–24px |
| `FontWeight` | W1.3 Shell | Font weight: Normal / Bold / SemiBold |
| `WebSocketAuthToken` | W1.6 Platform | 64-char hex for Kestrel WS auth |
| `MinimizeToTray` | W1.6 Platform | Minimize-to-tray toggle |
| `RecentPersonaIds` | W3.7 Model Configs | Recently-used persona IDs (max 5, JSON array) |

### 24.3 Boolean Convention: String "true"/"false"

All boolean settings are stored as strings `"true"` or `"false"`, consistent with the existing `MinimizeToTray` key (§17.3). This avoids type ambiguity in the key-value store and matches `ISettingsRepository.GetAsync()` which returns `string?`.

### 24.4 Setting Persistence Pattern — Write-Through, No Save Button

Every setting is persisted immediately on change. There is no "Save" or "Apply" button in any settings category:

```
User toggles CheckBox
  → ViewModel partial OnPropertyChanged handler fires
    → ISettingsRepository.SetAsync("KeyName", value.ToString().ToLower())
      → Setting takes effect immediately (theme swap, log filter update, etc.)
```

This applies to all 40+ settings keys. The pattern avoids stale state where the UI shows one value but the persisted value is different.

---

## 25. SQLite VACUUM — Database Maintenance Pattern

SQLite's `VACUUM` command rebuilds the database file, reclaiming space from deleted rows and defragmenting the file. Feature 8 exposes this through the Maintenance settings category.

### 25.1 VACUUM Characteristics

| Property | Value |
|----------|-------|
| **Command** | `VACUUM;` (raw SQL via `ExecuteSqlRawAsync`) |
| **Effect** | Rebuilds entire database file. Reclaims space from deleted rows. Defragments b-tree pages. |
| **Disk requirement** | Temporary free disk space equal to current database size |
| **Locking** | Exclusive lock on database during VACUUM. All other operations blocked. |
| **Duration** | Proportional to database size. ~100ms for 10MB, ~5s for 500MB. |

### 25.2 Implementation Pattern

```csharp
[RelayCommand]
private async Task CompactDatabaseAsync()
{
    var dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain", "msb.db");

    var beforeSize = new FileInfo(dbPath).Length;
    IsCompacting = true;
    StatusMessage = "Compacting database...";

    try
    {
        await _db.Database.ExecuteSqlRawAsync("VACUUM;");
        var afterSize = new FileInfo(dbPath).Length;
        var reclaimed = beforeSize - afterSize;

        ReclaimableSpace = FormatFileSize(reclaimed);
        LastCompaction = DateTimeOffset.UtcNow.ToString("g");
        await _settingsRepo.SetAsync("LastCompaction", LastCompaction);

        StatusMessage = $"Compaction complete. Reclaimed {FormatFileSize(reclaimed)}.";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "VACUUM failed");
        StatusMessage = "Compaction failed. Check available disk space.";
    }
    finally
    {
        IsCompacting = false;
    }
}

private static string FormatFileSize(long bytes) => bytes switch
{
    < 1024 => $"{bytes} B",
    < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
    < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
    _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
};
```

### 25.3 Design Decision

| Decision | Rationale |
|----------|-----------|
| **Raw SQL, not EF Core API** | EF Core has no `VACUUM` equivalent. `ExecuteSqlRawAsync` is the standard approach for database-specific maintenance commands. |
| **Before/after size comparison** | Gives the user feedback on how much space was reclaimed. `FileInfo.Length` is a fast, non-locking call. |
| **Progress indicator** | `IsCompacting = true` disables the "Compact" button and shows a status message. Prevents double-triggering. |
| **Persist last compaction time** | `LastCompaction` key in `ISettingsRepository` lets the UI show "Last compacted: ..." on next visit. |
| **Not scheduled** | VACUUM is user-initiated only. No automatic periodic compaction. |

---

## 26. Onboarding Step Completion Flags Pattern

The onboarding wizard persists per-step completion using boolean flags in `ISettingsRepository`. These flags drive first-launch detection, wizard resume, and the "Re-run Onboarding Wizard" feature.

### 26.1 Flag Keys

```
Onboarding_Step1_Completed  → API Keys step done
Onboarding_Step2_Completed  → Persona step done
Onboarding_Step3_Completed  → Wiki Directory step done
Onboarding_Step4_Completed  → Hotkeys step done
Onboarding_Completed         → All steps done (set on Finish screen)
```

### 26.2 First-Launch Detection

```csharp
// In App.xaml.cs OnStartup:
var onboardingCompleted = await settings.GetAsync("Onboarding_Completed");
if (onboardingCompleted != "true")
{
    // Show wizard — first launch or incomplete onboarding
    var wizardWindow = _serviceProvider.GetRequiredService<OnboardingWizardWindow>();
    wizardWindow.Show();
}
```

### 26.3 Step Completion Flow

```
User completes Step 1 (API Keys) → Next/Finish clicked
  → Onboarding_Step1_Completed = "true" (immediate persist)
  → CurrentStep advances to 2

User skips Step 2
  → Onboarding_Step2_Completed = "true" (skip = completed)
  → CurrentStep advances to 3

User closes wizard mid-way (X button)
  → Completed step flags ARE persisted (steps done so far are saved)
  → Next launch: wizard resumes at first incomplete step

User reaches Finish screen and clicks "Launch Studio"
  → Onboarding_Completed = "true"
  → All 4 step flags already "true" from individual steps
  → Next launch: wizard does NOT appear
```

### 26.4 Resume Logic

```csharp
// In OnboardingWizardViewModel constructor:
var step1Done = await _settings.GetAsync("Onboarding_Step1_Completed") == "true";
var step2Done = await _settings.GetAsync("Onboarding_Step2_Completed") == "true";
var step3Done = await _settings.GetAsync("Onboarding_Step3_Completed") == "true";
var step4Done = await _settings.GetAsync("Onboarding_Step4_Completed") == "true";

if (!step1Done) CurrentStep = 0;
else if (!step2Done) CurrentStep = 1;
else if (!step3Done) CurrentStep = 2;
else if (!step4Done) CurrentStep = 3;
else CurrentStep = -1; // Welcome (all steps done but Onboarding_Completed not set)
```

### 26.5 Design Decision

| Decision | Rationale |
|----------|-----------|
| **Per-step flags, not a single integer** | Steps can be skipped independently. A single "current step" integer would lose which steps were actually completed. |
| **Resume, not restart** | Closing the wizard mid-way preserves progress. The user doesn't redo completed steps. |
| **Skip = completed** | Skipping a step means the user is OK with defaults for that step. It should not re-prompt on next launch. |
| **Separate `Onboarding_Completed` flag** | Allows re-running the wizard from Settings even though all step flags are `"true"`. The wizard checks individual step flags for resume, not the aggregate `Onboarding_Completed`. |
| **Null = not done, "true" = done** | Consistent with other boolean settings. `null` is the initial state (key doesn't exist). `"false"` is never set — a step is either done or not done. |

---

## 27. MemoryEntry Entity — Key-Value Memory Store

`MemoryEntryEntity` (`Data/Entities/MemoryEntryEntity.cs`) is the 15th entity, added by the `AddMemoryEntry` migration. It implements a simple key-value store for thread-scoped persistent memory, following Anthropic's `memory_20250818` schema.

### 27.1 Entity Schema

```csharp
public class MemoryEntryEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(10240)]
    public string Value { get; set; } = string.Empty;

    public string? SourceThreadId { get; set; }      // Optional FK → ChatThread

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [ForeignKey(nameof(SourceThreadId))]
    public ChatThread? SourceThread { get; set; }
}
```

### 27.2 Database Configuration

| Property | Value |
|----------|-------|
| **Table** | `MemoryEntries` |
| **PK** | `Id` (string, GUID, 32-char hex `"N"` format) |
| **Index on `Key`** | Fast lookup by memory key |
| **Index on `CreatedAt`** | Time-based queries and cleanup |
| **FK** | `SourceThreadId` → `ChatThread.Id`, optional, `OnDelete(DeleteBehavior.SetNull)` |
| **Key max length** | 200 characters (`MemoryEntryEntityConsts.KeyMaxLength`) |
| **Value max length** | 10,240 characters (~10KB, `MemoryEntryEntityConsts.ValueMaxLength`) |

### 27.3 DbContext Registration

```csharp
public DbSet<MemoryEntryEntity> MemoryEntries => Set<MemoryEntryEntity>();
```

### 27.4 Entity vs. Domain Model

| Layer | Type | Location |
|-------|------|----------|
| Data (EF Core) | `MemoryEntryEntity` | `Data/Entities/MemoryEntryEntity.cs` |
| Core (DTO) | `MemoryEntry` | `Core/Models/DomainModels.cs` |

Both carry the same fields. `MemoryEntry.KeyMaxLength` and `MemoryEntry.ValueMaxLength` constants in Core must be kept in sync with `MemoryEntryEntityConsts.KeyMaxLength`/`ValueMaxLength` in Data.

### 27.5 Migration

Created via: `dotnet ef migrations add AddMemoryEntry --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI`

---

## 28. ToolAutoApprovalSettings — 10 Tool-Specific Auto-Approval Flags

`ToolAutoApprovalSettings` (in `Core/Models/DomainModels.cs`) controls which tools can execute without user confirmation. Each tool has its own boolean flag:

```csharp
public class ToolAutoApprovalSettings
{
    public bool AutoApproveBash { get; set; }
    public bool AutoApproveTextEditor { get; set; }
    public bool AutoApproveWebSearch { get; set; }
    public bool AutoApproveWebFetch { get; set; }
    public bool AutoApproveWikiSearch { get; set; }
    public bool AutoApproveMemory { get; set; }
    public bool AutoApproveSkillLoad { get; set; }
    public bool AutoApproveAskUserInput { get; set; }
    public bool AutoApprovePresentFiles { get; set; }
    public bool AutoApproveImageSearch { get; set; }
    public int MaxConsecutiveAutoApprovals { get; set; } = 10;
}
```

### 28.1 Default Behavior

By default, all `AutoApprove*` properties are `false` (no tool auto-approved without explicit user opt-in). The `MaxConsecutiveAutoApprovals` cap defaults to 10 to prevent runaway tool execution loops even when auto-approval is enabled.

### 28.2 Persistence

The settings are persisted via `ISettingsRepository` (key-value in `AppSetting` table). The keys follow the pattern `AutoApprove_{ToolName}` (e.g., `AutoApprove_Bash`, `AutoApprove_TextEditor`). The settings UI (Settings → Tools category) binds checkboxes to these values.

### 28.3 Usage

The orchestrator (`ToolOrchestrator.GetAutoApprovalSettings()`) returns the current settings. Tool executors check `CanAutoApprove` (per-executor capability) AND the corresponding `AutoApprove{Name}` flag (user preference) before executing without confirmation.
