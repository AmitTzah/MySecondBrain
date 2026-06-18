# Feature Reference: Data Layer — All Entities, DbContext & Repositories

## Global & Shared Documentation

- **EF Core + SQLite Patterns:** [`agent-workspace/external-docs/ef-core-sqlite.md`](agent-workspace/external-docs/ef-core-sqlite.md) — DbContext configuration, Fluent API relationships, migrations, FTS5 raw SQL, seed data, repository pattern
- **Architecture Knowledge Base:** [`agent-workspace/knowledge/architecture.md`](agent-workspace/knowledge/architecture.md) — DI lifetimes, stub pattern, platform-specific service placement, AppDbContext factory delegate
- **Database Knowledge Base:** [`agent-workspace/knowledge/database.md`](agent-workspace/knowledge/database.md) — entity catalog, PK conventions, entity vs DTO separation, repository catalog, migration strategy

---

## Step-Specific Documentation

### Step 1: Complete all 13 entity classes with vision-aligned attributes, add MessageDrafts entity, and add AppSetting entity

- **Library:** Entity Framework Core 8.x (built-in `System.ComponentModel.DataAnnotations`)
- **Key Attributes Used:**
  - `[Key]` — marks primary key property
  - `[MaxLength(N)]` — string length constraint
  - `[ForeignKey(nameof(NavigationProperty))]` — explicit FK relationship
  - `[Index(nameof(Property), IsUnique = true)]` — database index
  - `[Range(min, max)]` — numeric range validation
  - `[NotMapped]` — exclude property from database
- **Key Pattern — Entity with Navigation Properties:**
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MySecondBrain.Data.Entities;

public class ChatThread
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(200)]
    public string? Title { get; set; }

    public bool IsTransient { get; set; }

    // FK properties
    public string? PersonaId { get; set; }
    public string? ModelConfigId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(PersonaId))]
    public Persona? Persona { get; set; }

    [ForeignKey(nameof(ModelConfigId))]
    public ModelConfiguration? ModelConfig { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
    public ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
    public ICollection<UsageRecord> UsageRecords { get; set; } = new List<UsageRecord>();
}
```
- **MessageDrafts Entity (NEW):**
```csharp
using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class MessageDrafts
{
    /// <summary>Thread ID serves as the primary key (one draft per thread).</summary>
    [Key]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Current textbox content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Current cursor position in the textbox.</summary>
    public int CursorPosition { get; set; }

    /// <summary>When the draft was last saved.</summary>
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
```
- **MediaItem Soft-Delete Pattern:**
```csharp
// Add to MediaItem entity:
public bool IsDeleted { get; set; }
public DateTimeOffset? DeletedAt { get; set; }
```
- **AppSetting Entity (NEW — backs SettingsRepository):**
```csharp
using System.ComponentModel.DataAnnotations;

namespace MySecondBrain.Data.Entities;

public class AppSetting
{
    /// <summary>Unique setting key (e.g., "theme", "last-active-thread").</summary>
    [Key]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Setting value. JSON-serialized for complex types.</summary>
    public string Value { get; set; } = string.Empty;
}
```
Add `public DbSet<AppSetting> Settings => Set<AppSetting>();` to `AppDbContext` in Step 2.
- **Vision Entity Property Checklist (per entity):**

| Entity | Required Properties (must exist) |
|--------|-------------------------------|
| ApiKey | Id, DisplayName, Provider, CustomProviderName?, CustomEndpointUrl?, KeyValue, IsValid, LastTestedAt?, CreatedAt |
| Artifact | Id, Name, Type, ThreadId, VersionCount, CreatedAt, UpdatedAt |
| ChatThread | Id, Title?, IsTransient, PersonaId?, ModelConfigId?, SystemMessage?, ChatMode, ThinkingEnabled, IsMuted, IsFavorite, IsPinned, IsArchived, ColorLabel?, Tags?, FolderId?, IsDeleted, DeletedAt?, SourceHWND?, SourceAppName?, SourceDocTitle?, OriginalHighlightedText?, CreatedAt, LastActivityAt |
| MediaItem | Id, FileName, FilePath, MediaType, MimeType, FileSize, Source, ThreadId, MessageId?, GeneratedPrompt?, IsSavedToDisk, IsSavedToWiki, IsDeleted, DeletedAt?, CreatedAt |
| Message | Id, ThreadId, Role, Content, RawContent?, PersonaId?, ModelConfigId?, TokenCount?, EstimatedCost?, GenerationTimeMs?, Feedback?, ParentMessageId?, VersionNumber, BranchId, IsActiveBranch, IsDirectTransformation?, CreatedAt |
| ModelConfiguration | Id, DisplayName, Provider, ApiKeyId?, ModelIdentifier?, Temperature, MaxOutputTokens, MaxContextWindow, ThinkingEnabled, PricingInputPer1K?, PricingOutputPer1K?, ContextOverflowStrategy, CreatedAt, UpdatedAt |
| Persona | Id, DisplayName, SystemPrompt?, DefaultModelConfigId?, DefaultChatMode, IsBuiltIn, CreatedAt, UpdatedAt |
| PromptTemplate | Id, Name, Text, Tags?, FolderId?, CreatedAt, UpdatedAt |
| TextAction | Id, DisplayName, SystemPrompt?, ModelConfigId?, Hotkey?, IsBuiltIn, CreatedAt, UpdatedAt |
| UsageRecord | Id, MessageId, ThreadId, PersonaId?, ModelConfigId?, Provider, ModelIdentifier, PromptTokens, CompletionTokens, TotalTokens, EstimatedCost?, CreatedAt |
| WikiFile | FilePath (PK), FileName, H1Title?, Headings?, Content?, WordCount?, LastModifiedAt?, CrossLinksOut?, CrossLinksIn? |
| WikiVersionSnapshot | Id, WikiFilePath (FK), Content, Source, CreatedAt |

---

### Step 2: Complete AppDbContext with OnModelCreating Fluent API, indexes, and seed data

- **Library:** `Microsoft.EntityFrameworkCore` 8.x
- **Fluent API Relationship Patterns (from Context7 reference):**

**Standard One-to-Many:**
```csharp
modelBuilder.Entity<ChatThread>()
    .HasOne(t => t.Persona)
    .WithMany(p => p.ChatThreads)
    .HasForeignKey(t => t.PersonaId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);
```

**Self-Referencing (Message branching):**
```csharp
modelBuilder.Entity<Message>()
    .HasOne(m => m.ParentMessage)
    .WithMany(m => m.ChildMessages)
    .HasForeignKey(m => m.ParentMessageId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.Restrict);
```

**Alternate Key FK (WikiVersionSnapshot → WikiFile by FilePath):**
```csharp
modelBuilder.Entity<WikiVersionSnapshot>()
    .HasOne(v => v.WikiFile)
    .WithMany(f => f.WikiVersionSnapshots)
    .HasPrincipalKey(f => f.FilePath)
    .HasForeignKey(v => v.WikiFilePath)
    .OnDelete(DeleteBehavior.Cascade);
```

**Restrict Delete (ModelConfiguration referenced by Personas):**
```csharp
modelBuilder.Entity<Persona>()
    .HasOne(p => p.DefaultModelConfig)
    .WithMany(mc => mc.Personas)
    .HasForeignKey(p => p.DefaultModelConfigId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.Restrict);
```

- **Index Creation via Fluent API:**
```csharp
modelBuilder.Entity<Message>()
    .HasIndex(m => m.ThreadId);

modelBuilder.Entity<Message>()
    .HasIndex(m => m.CreatedAt);

modelBuilder.Entity<ChatThread>()
    .HasIndex(t => t.LastActivityAt);

modelBuilder.Entity<ChatThread>()
    .HasIndex(t => t.IsTransient);

modelBuilder.Entity<ChatThread>()
    .HasIndex(t => t.IsDeleted);
```

- **Seed Data Pattern:**
```csharp
// Fixed GUIDs required for deterministic migrations
modelBuilder.Entity<Persona>().HasData(
    new Persona
    {
        Id = "00000000000000000000000000000001",
        DisplayName = "General Assistant",
        SystemPrompt = "You are a helpful, thoughtful assistant.",
        DefaultChatMode = "Standard",
        IsBuiltIn = true,
        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    },
    new Persona
    {
        Id = "00000000000000000000000000000002",
        DisplayName = "Code Helper",
        SystemPrompt = "You are an expert software developer. Provide clean, well-documented code.",
        DefaultChatMode = "Standard",
        IsBuiltIn = true,
        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    }
);

modelBuilder.Entity<TextAction>().HasData(
    new TextAction { Id = "a000000000000000000000000000001", DisplayName = "Rewrite",
        SystemPrompt = "Rewrite the following text to improve clarity, flow, and impact while preserving the original meaning.",
        IsBuiltIn = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
    new TextAction { Id = "a000000000000000000000000000002", DisplayName = "Summarize",
        SystemPrompt = "Summarize the following text concisely, capturing the key points.",
        IsBuiltIn = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
    new TextAction { Id = "a000000000000000000000000000003", DisplayName = "Explain",
        SystemPrompt = "Explain the following text clearly and thoroughly, as if teaching someone new to the topic.",
        IsBuiltIn = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
    new TextAction { Id = "a000000000000000000000000000004", DisplayName = "Translate",
        SystemPrompt = "Translate the following text to English. Preserve formatting and tone.",
        IsBuiltIn = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
    new TextAction { Id = "a000000000000000000000000000005", DisplayName = "Fix Grammar",
        SystemPrompt = "Fix grammar, spelling, and punctuation errors in the following text. Preserve the original meaning and style.",
        IsBuiltIn = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
    new TextAction { Id = "a000000000000000000000000000006", DisplayName = "Enhance Prompt",
        SystemPrompt = "Improve the following prompt to be more specific, detailed, and effective. Add relevant context and constraints.",
        IsBuiltIn = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }
);
```

- **OnDelete Behavior Summary:**
| Relationship | OnDelete |
|-------------|----------|
| ChatThread → Persona | SetNull |
| ChatThread → ModelConfiguration | SetNull |
| Message → ChatThread | Cascade |
| Message → Persona | SetNull |
| Message → ModelConfiguration | SetNull |
| Message → Message (self) | Restrict |
| Persona → ModelConfiguration | Restrict |
| ModelConfiguration → ApiKey | SetNull |
| Artifact → ChatThread | Cascade |
| MediaItem → ChatThread | Cascade |
| MediaItem → Message | SetNull |
| UsageRecord → Message | Cascade |
| UsageRecord → ChatThread | Cascade |
| UsageRecord → Persona | SetNull |
| UsageRecord → ModelConfiguration | SetNull |
| TextAction → ModelConfiguration | SetNull |
| WikiVersionSnapshot → WikiFile | Cascade |

---

### Step 3: Create InitialCreate migration with FTS5 virtual tables and auto-apply at startup

- **Library:** `Microsoft.EntityFrameworkCore.Design` 8.0.* (design-time), `Microsoft.Data.Sqlite` 8.0.* (FTS5)
- **Migration Creation Command:**
```bash
dotnet ef migrations add InitialCreate --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI
```

- **FTS5 Virtual Table Raw SQL (add to Up() method):**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // ... auto-generated table creation code ...

    // FTS5 virtual tables with content-sync triggers
    migrationBuilder.Sql(@"
        CREATE VIRTUAL TABLE IF NOT EXISTS MessageFts USING fts5(
            Content,
            content=Messages,
            content_rowid=rowid
        );

        CREATE TRIGGER IF NOT EXISTS Messages_AI AFTER INSERT ON Messages BEGIN
            INSERT INTO MessageFts(rowid, Content) VALUES (new.rowid, new.Content);
        END;

        CREATE TRIGGER IF NOT EXISTS Messages_AD AFTER DELETE ON Messages BEGIN
            INSERT INTO MessageFts(MessageFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
        END;

        CREATE TRIGGER IF NOT EXISTS Messages_AU AFTER UPDATE ON Messages BEGIN
            INSERT INTO MessageFts(MessageFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
            INSERT INTO MessageFts(rowid, Content) VALUES (new.rowid, new.Content);
        END;

        CREATE VIRTUAL TABLE IF NOT EXISTS WikiFileFts USING fts5(
            Content,
            content=WikiFiles,
            content_rowid=rowid
        );

        CREATE TRIGGER IF NOT EXISTS WikiFiles_AI AFTER INSERT ON WikiFiles BEGIN
            INSERT INTO WikiFileFts(rowid, Content) VALUES (new.rowid, new.Content);
        END;

        CREATE TRIGGER IF NOT EXISTS WikiFiles_AD AFTER DELETE ON WikiFiles BEGIN
            INSERT INTO WikiFileFts(WikiFileFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
        END;

        CREATE TRIGGER IF NOT EXISTS WikiFiles_AU AFTER UPDATE ON WikiFiles BEGIN
            INSERT INTO WikiFileFts(WikiFileFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
            INSERT INTO WikiFileFts(rowid, Content) VALUES (new.rowid, new.Content);
        END;
    ", suppressTransaction: true);
}
```

- **FTS5 Cleanup (add to Down() method):**
```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // ... auto-generated table drop code ...

    migrationBuilder.Sql(@"
        DROP TRIGGER IF EXISTS Messages_AI;
        DROP TRIGGER IF EXISTS Messages_AD;
        DROP TRIGGER IF EXISTS Messages_AU;
        DROP TABLE IF EXISTS MessageFts;
        DROP TRIGGER IF EXISTS WikiFiles_AI;
        DROP TRIGGER IF EXISTS WikiFiles_AD;
        DROP TRIGGER IF EXISTS WikiFiles_AU;
        DROP TABLE IF EXISTS WikiFileFts;
    ", suppressTransaction: true);
}
```

- **Auto-Migrate in App.xaml.cs (add after IServiceProvider build):**
```csharp
// After _serviceProvider = services.BuildServiceProvider();
try
{
    var db = _serviceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    var startupLogger = _serviceProvider.GetRequiredService<ILogger<App>>();
    startupLogger.LogInformation("Database migration applied successfully");
}
catch (Exception ex)
{
    var startupLogger = _serviceProvider.GetRequiredService<ILogger<App>>();
    startupLogger.LogError(ex, "Database migration failed");
    throw; // Re-throw — app cannot function without database
}
```

- **FTS5 Search Query Pattern (for later use in repositories):**
```csharp
// Search Messages via FTS5
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

---

### Step 4: Implement ChatThreadRepository and MessageRepository with real EF Core queries

- **Library:** `Microsoft.EntityFrameworkCore` 8.x
- **ChatThreadRepository — Key Queries:**

**GetByIdAsync with includes:**
```csharp
public async Task<ChatThread?> GetByIdAsync(string id) =>
    await _db.ChatThreads
        .Include(t => t.Persona)
        .Include(t => t.ModelConfig)
        .FirstOrDefaultAsync(t => t.Id == id);
```

**GetAllPermanentAsync with sort:**
```csharp
public async Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort) =>
    sort switch
    {
        ChatSortOrder.CreatedAsc => await _db.ChatThreads
            .Where(t => !t.IsTransient && !t.IsDeleted)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(),
        ChatSortOrder.TitleAsc => await _db.ChatThreads
            .Where(t => !t.IsTransient && !t.IsDeleted)
            .OrderBy(t => t.Title)
            .ToListAsync(),
        _ => await _db.ChatThreads
            .Where(t => !t.IsTransient && !t.IsDeleted)
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync()
    };
```

**CleanupTransientAsync with exception checks (O4 logic):**
```csharp
public async Task<int> CleanupTransientAsync(DateTimeOffset olderThan)
{
    // Auto-elevate threads with exceptions before deleting
    var exceptions = await _db.ChatThreads
        .Where(t => t.IsTransient && t.CreatedAt < olderThan)
        .Where(t => t.IsFavorite || t.Tags != null || t.IsPinned || t.IsArchived
            || t.Messages.Any(m => m.Role == "User" && m.VersionNumber == 1)
            || t.Artifacts.Any())
        .ToListAsync();

    foreach (var thread in exceptions)
        thread.IsTransient = false;

    // Hard-delete remaining transient threads
    var toDelete = await _db.ChatThreads
        .Where(t => t.IsTransient && t.CreatedAt < olderThan)
        .Where(t => !t.IsFavorite && t.Tags == null && !t.IsPinned && !t.IsArchived)
        .ToListAsync();

    _db.ChatThreads.RemoveRange(toDelete);
    await _db.SaveChangesAsync();
    return toDelete.Count;
}
```

- **MessageRepository — Key Queries:**

**GetActiveBranchAsync with recursive CTE:**
```csharp
public async Task<IReadOnlyList<Message>> GetActiveBranchAsync(string threadId)
{
    // EF Core doesn't support recursive CTE natively, use raw SQL
    return await _db.Messages
        .FromSqlRaw(@"
            WITH RECURSIVE active_chain AS (
                SELECT * FROM Messages 
                WHERE ThreadId = {0} AND ParentMessageId IS NULL AND IsActiveBranch = 1
                UNION ALL
                SELECT m.* FROM Messages m
                INNER JOIN active_chain a ON m.ParentMessageId = a.Id
                WHERE m.IsActiveBranch = 1
            )
            SELECT * FROM active_chain ORDER BY CreatedAt
        ", threadId)
        .AsNoTracking()
        .ToListAsync();
}
```

**SetActiveBranch (branch navigation):**
```csharp
public async Task SetActiveBranch(string messageId, string branchId)
{
    var message = await _db.Messages.FindAsync(messageId);
    if (message == null) return;

    // Deactivate old active branch from this point forward
    var chain = await GetActiveBranchAsync(message.ThreadId);
    var fromIndex = chain.FindIndex(m => m.Id == messageId);
    for (int i = fromIndex; i < chain.Count; i++)
        chain[i].IsActiveBranch = false;

    // Activate new branch
    var newBranch = await _db.Messages
        .Where(m => m.BranchId == branchId && m.IsActiveBranch == false)
        .ToListAsync();
    foreach (var m in newBranch)
        m.IsActiveBranch = true;

    await _db.SaveChangesAsync();
}
```

---

### Step 5: Implement PersonaRepository, ModelConfigurationRepository, and ApiKeyRepository

- **Library:** `Microsoft.EntityFrameworkCore` 8.x
- **PersonaRepository — GetDefaultAsync:**
```csharp
public async Task<Persona?> GetDefaultAsync() =>
    await _db.Personas
        .FirstOrDefaultAsync(p => p.IsBuiltIn)
    ?? await _db.Personas.FirstOrDefaultAsync();
```

- **ModelConfigurationRepository — DeleteAsync with restrict check:**
```csharp
public async Task DeleteAsync(string id)
{
    var config = await _db.ModelConfigurations
        .Include(mc => mc.Personas)
        .FirstOrDefaultAsync(mc => mc.Id == id);

    if (config == null) return;

    if (config.Personas.Count > 0)
        throw new InvalidOperationException(
            $"Cannot delete ModelConfiguration '{config.DisplayName}' — " +
            $"it is referenced by {config.Personas.Count} Persona(s).");

    _db.ModelConfigurations.Remove(config);
    await _db.SaveChangesAsync();
}
```

- **ApiKeyRepository — Standard CRUD (encryption handled at service layer):**
```csharp
public async Task<ApiKey?> GetByIdAsync(string id) =>
    await _db.ApiKeys.FindAsync(id);

public async Task<IReadOnlyList<ApiKey>> GetAllAsync() =>
    await _db.ApiKeys.AsNoTracking().ToListAsync();

public async Task<ApiKey> CreateAsync(ApiKey key)
{
    _db.ApiKeys.Add(key);
    await _db.SaveChangesAsync();
    return key;
}

public async Task UpdateAsync(ApiKey key)
{
    _db.Entry(key).State = EntityState.Modified;
    await _db.SaveChangesAsync();
}

public async Task DeleteAsync(string id)
{
    var key = await _db.ApiKeys
        .Include(k => k.ModelConfigurations)
        .FirstOrDefaultAsync(k => k.Id == id);

    if (key == null) return;

    // Nullify FK on ModelConfigurations referencing this key
    foreach (var mc in key.ModelConfigurations)
        mc.ApiKeyId = null;

    _db.ApiKeys.Remove(key);
    await _db.SaveChangesAsync();
}
```

---

### Step 6: Implement WikiIndexRepository, UsageRepository, and SettingsRepository

- **Library:** `Microsoft.EntityFrameworkCore` 8.x, `System.Text.Json` (for SettingsRepository)
- **WikiIndexRepository — FTS5 Search:**
```csharp
public async Task<IReadOnlyList<WikiFile>> SearchAsync(string query, int maxResults)
{
    return await _db.WikiFiles
        .FromSqlRaw(@"
            SELECT w.* FROM WikiFiles w
            INNER JOIN WikiFileFts fts ON w.rowid = fts.rowid
            WHERE WikiFileFts MATCH {0}
            ORDER BY rank
            LIMIT {1}
        ", query, maxResults)
        .AsNoTracking()
        .ToListAsync();
}
```

- **WikiIndexRepository — UpsertAsync:**
```csharp
public async Task<WikiFile> UpsertAsync(WikiFile file)
{
    var existing = await _db.WikiFiles.FindAsync(file.FilePath);
    if (existing == null)
        _db.WikiFiles.Add(file);
    else
        _db.Entry(existing).CurrentValues.SetValues(file);

    await _db.SaveChangesAsync();
    return file;
}
```

- **WikiIndexRepository — PruneSnapshotsAsync (30-per-file, 50MB cap):**
```csharp
public async Task PruneSnapshotsAsync(string filePath, int maxSnapshots, long maxTotalBytes)
{
    var snapshots = await _db.WikiVersionSnapshots
        .Where(s => s.WikiFilePath == filePath)
        .OrderByDescending(s => s.CreatedAt)
        .ToListAsync();

    // Per-file limit: keep newest maxSnapshots
    if (snapshots.Count > maxSnapshots)
    {
        var toRemove = snapshots.Skip(maxSnapshots);
        _db.WikiVersionSnapshots.RemoveRange(toRemove);
    }

    // Global storage cap: remove oldest across all files
    var totalBytes = await _db.WikiVersionSnapshots
        .SumAsync(s => (long)s.Content.Length);
    
    if (totalBytes > maxTotalBytes)
    {
        var oldest = await _db.WikiVersionSnapshots
            .OrderBy(s => s.CreatedAt)
            .Take((int)((totalBytes - maxTotalBytes) / 10000) + 1)
            .ToListAsync();
        _db.WikiVersionSnapshots.RemoveRange(oldest);
    }

    await _db.SaveChangesAsync();
}
```

- **UsageRepository — Aggregation Queries:**
```csharp
public async Task<UsageSummary> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to)
{
    return await _db.UsageRecords
        .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
        .GroupBy(_ => 1)
        .Select(g => new UsageSummary(
            g.Count(),
            g.Sum(r => r.TotalTokens),
            g.Sum(r => r.PromptTokens),
            g.Sum(r => r.CompletionTokens),
            g.Sum(r => r.EstimatedCost ?? 0)
        ))
        .FirstOrDefaultAsync() ?? new UsageSummary(0, 0, 0, 0, 0);
}
```

- **SettingsRepository — Backed by AppSetting DbSet (entity created in Step 1):**
```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db;
    public SettingsRepository(AppDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _db.Settings.FindAsync(key);
        return setting?.Value;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var json = await GetAsync(key);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync(string key, string value)
    {
        var existing = await _db.Settings.FindAsync(key);
        if (existing == null)
        {
            _db.Settings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }
        await _db.SaveChangesAsync();
    }

    public async Task SetAsync<T>(string key, T value) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await SetAsync(key, json);
    }

    public async Task DeleteAsync(string key)
    {
        var setting = await _db.Settings.FindAsync(key);
        if (setting != null)
        {
            _db.Settings.Remove(setting);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        return await _db.Settings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }
}
```

- **AppSetting entity** (created in Step 1, DbSet added to AppDbContext in Step 2):
  - `Key` (string PK, `[MaxLength(200)]`) — unique setting identifier
  - `Value` (string) — setting value, JSON-serialized for complex types
  - No FK relationships, no navigation properties
  - Included in `InitialCreate` migration alongside all other entities
