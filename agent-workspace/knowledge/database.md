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

    // 12 DbSet<T> properties — one per entity
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<ModelConfiguration> ModelConfigurations => Set<ModelConfiguration>();
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
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
        // FK configuration for entities where the FK property name
        // doesn't follow EF Core convention, or for composite keys
        modelBuilder.Entity<WikiVersionSnapshot>()
            .HasOne<WikiFile>()
            .WithMany()
            .HasForeignKey(s => s.WikiFilePath)
            .HasPrincipalKey(f => f.FilePath);
    }
}
```

**Key design decisions:**
- Single `DbContext` singleton for single-user desktop app (no concurrency concerns)
- **Runtime DI registration:** Factory delegate in `App.xaml.cs` creates `DbContextOptions<AppDbContext>` with SQLite path at `%LOCALAPPDATA%\MySecondBrain\msb.db`, auto-creating directory if missing. See [Architecture §3.3](architecture.md#33-appdbcontext-factory-delegate-registration).
- **Design-time fallback:** `OnConfiguring` fallback resolves the same `%LOCALAPPDATA%` path for EF Core tooling (migrations, scaffolding) when no DI-provided options exist.
- **`OnModelCreating`:** Used only for FK relationships that deviate from EF Core conventions (e.g., `WikiVersionSnapshot` references `WikiFile` by `FilePath` rather than a generated integer key).

### 2.1 Database File Path Convention

| Property | Value |
|----------|-------|
| Base directory | `%LOCALAPPDATA%\MySecondBrain\` |
| Database file | `msb.db` |
| Full path | `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MySecondBrain", "msb.db")` |
| Directory creation | `Directory.CreateDirectory()` called before first use — both in factory delegate and `OnConfiguring` fallback |


---

## 3. Entity Catalog — 12 Entities with FK Relationships

All entities are defined in `MySecondBrain.Data/Entities/` as EF Core entity classes. Each uses `[Key]` attribute on its primary key and `string` GUIDs for PKs (see §3.1).

> **Note:** The full vision and planning data model defines 13 entities. The 13th entity, `BackupSnapshot`, is planned for W3.16 (Backup & Recovery) and has not yet been implemented. It will be added as a standalone entity (no FK relationships) when that feature is built.

| # | Entity | PK | Key FK Relationships |
|---|--------|----|---------------------|
| 1 | `ApiKey` | `Id` (string GUID) | `ModelConfigurationId` → `ModelConfiguration.Id` |
| 2 | `Persona` | `Id` (string GUID) | `DefaultModelConfigId` → `ModelConfiguration.Id` |
| 3 | `ModelConfiguration` | `Id` (string GUID) | — (referenced by ApiKey, Persona, ChatThread) |
| 4 | `ChatThread` | `Id` (string GUID) | `PersonaId` → `Persona.Id`; `ModelConfigId` → `ModelConfiguration.Id`; `ICollection<Message>` (1:many) |
| 5 | `Message` | `Id` (string GUID) | `ThreadId` → `ChatThread.Id`; `ParentMessageId` → `Message.Id` (self-ref, nullable, for branching); `ICollection<Artifact>` (1:many) |
| 6 | `Artifact` | `Id` (string GUID) | `MessageId` → `Message.Id` |
| 7 | `MediaItem` | `Id` (string GUID) | `MessageId` → `Message.Id` (nullable) |
| 8 | `PromptTemplate` | `Id` (string GUID) | — (standalone) |
| 9 | `TextAction` | `Id` (string GUID) | — (standalone) |
| 10 | `UsageRecord` | `Id` (string GUID) | `ModelConfigId` → `ModelConfiguration.Id` (nullable) |
| 11 | `WikiFile` | `FilePath` (string — natural key) | `ICollection<WikiVersionSnapshot>` (1:many) |
| 12 | `WikiVersionSnapshot` | `Id` (string GUID) | `WikiFilePath` → `WikiFile.FilePath` (non-standard FK, configured in `OnModelCreating`) |

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
