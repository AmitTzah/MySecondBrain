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

    // 14 DbSet<T> properties — one per entity
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

## 3. Entity Catalog — 14 Entities, 17 FK Relationships

All entities are defined in `MySecondBrain.Data/Entities/` as EF Core entity classes. Each uses `[Key]` attribute on its primary key and `string` GUIDs for PKs (see §3.1). The `InitialCreate` migration creates all 14 tables plus 2 FTS5 virtual tables.

| # | Entity | PK | FK Relationships | Notes |
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

> **Note:** `BackupSnapshot` is planned for W3.16 (Backup & Recovery) and is not yet implemented. It will be a standalone entity (no FK relationships).

### FK Relationship Count: 17

The 17 foreign key relationships are configured in `OnModelCreating` via Fluent API. See §8 for the complete OnDelete behavior reference.

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
├── Entities/                     # EF Core entity classes (12 entities)
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
