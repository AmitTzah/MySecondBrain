# Database Knowledge — MySecondBrain

> **Global database schemas, models, migrations, and data-layer patterns.**  
> Source: Feature 1/245 — .NET 8.0 WPF Solution Scaffold.

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=msb.db");
        }
    }
}
```

**Key design decisions:**
- Single `DbContext` singleton for single-user desktop app (no concurrency concerns)
- `OnConfiguring` fallback to `Data Source=msb.db` when not configured via DI
- DI path: `services.AddDbContext<AppDbContext>(...)` in `App.xaml.cs` `ConfigureServices`
- Database file `msb.db` lives in the app data directory (resolved at runtime)

---

## 3. Entity Catalog — 13 Planned Entities

All entities will be defined in `MySecondBrain.Data/Entities/` as EF Core entity classes with `IEntityTypeConfiguration<T>` configurations in `MySecondBrain.Data/Configurations/`.

| # | Entity | Purpose |
|---|--------|---------|
| 1 | `ApiKey` | Encrypted storage for LLM provider API keys |
| 2 | `Persona` | AI persona definitions (system prompts, behaviors) |
| 3 | `ModelConfiguration` | Per-model provider settings (temperature, max_tokens, etc.) |
| 4 | `ChatThread` | Chat conversation container (title, created, archived) |
| 5 | `Message` | Individual messages within a chat thread (role, content, tokens) |
| 6 | `Artifact` | Generated artifacts (code, documents, diagrams) with versioning |
| 7 | `MediaItem` | Uploaded/recorded media (images, audio, video) |
| 8 | `PromptTemplate` | Reusable prompt templates with parameter slots |
| 9 | `TextAction` | Tier1 text actions (rewrite, summarize, translate, etc.) |
| 10 | `UsageRecord` | Token usage and cost tracking per provider/model |
| 11 | `WikiFile` | Wiki page metadata (path, title, tags, links) |
| 12 | `WikiVersionSnapshot` | Git-backed wiki version history snapshots |
| 13 | `BackupSnapshot` | Backup metadata (timestamp, provider, checksum) |

> **Note:** Full entity attributes, relationships, and FK dependencies are defined in [`agent-workspace/project-director/planning/data-model.md`](../project-director/planning/data-model.md). Entity classes will be created by subsequent Wave 1 features.

---

## 4. Repository Pattern Conventions

- **Interface location:** `MySecondBrain.Core/Interfaces/` (e.g., `IChatThreadRepository`)
- **Implementation location:** `MySecondBrain.Data/Repositories/`
- **Injection:** Services depend on repository interfaces (in Core), not on EF Core directly
- **DbContext access:** Repositories receive `AppDbContext` via constructor injection
- **Lifetime:** Singleton (single-user desktop app)

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
├── Entities/                     # EF Core entity classes (13 planned)
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
