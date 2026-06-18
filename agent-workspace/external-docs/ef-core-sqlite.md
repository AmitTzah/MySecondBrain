# Entity Framework Core + SQLite — Reference Patterns

> Source: Context7 query against `/dotnet/entityframework.docs` (Microsoft official documentation)
> Retrieved: 2026-06-18

---

## 1. DbContext Configuration with SQLite

### Basic SQLite DbContext
```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=path/to/db.db");
        }
    }
}
```

### DI Registration with Factory Delegate
```csharp
services.AddSingleton(sp =>
{
    var dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain", "msb.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={dbPath}")
        .Options;
    return new AppDbContext(options);
});
```

---

## 2. Fluent API Relationships

### One-to-Many with Standard FK
```csharp
modelBuilder.Entity<Blog>()
    .HasMany(e => e.Posts)
    .WithOne(e => e.Blog)
    .HasForeignKey(e => e.BlogId)
    .HasPrincipalKey(e => e.Id);
```

### One-to-Many with Alternate Key (Non-Standard PK)
```csharp
modelBuilder.Entity<Blog>()
    .HasMany(e => e.Posts)
    .WithOne(e => e.Blog)
    .HasPrincipalKey(e => e.AlternateId)
    .HasForeignKey(e => e.BlogId)
    .IsRequired();
```

### Self-Referencing Relationship (Message branching)
```csharp
modelBuilder.Entity<Message>()
    .HasOne(m => m.ParentMessage)
    .WithMany(m => m.ChildMessages)
    .HasForeignKey(m => m.ParentMessageId)
    .IsRequired(false);  // nullable for root messages
```

### Required vs Optional FK
```csharp
// Required (non-nullable FK property)
modelBuilder.Entity<Post>()
    .HasOne(e => e.Blog)
    .WithMany(e => e.Posts)
    .HasForeignKey(e => e.BlogId)
    .IsRequired();

// Optional (nullable FK property)
modelBuilder.Entity<Post>()
    .HasOne(e => e.Blog)
    .WithMany(e => e.Posts)
    .HasForeignKey(e => e.BlogId)
    .IsRequired(false);
```

---

## 3. Migrations

### Creating a Migration
```
dotnet ef migrations add InitialCreate --project src/MySecondBrain.Data --startup-project src/MySecondBrain.UI
```

### Applying Migrations at Startup (Single-user Desktop)
```csharp
// In App.xaml.cs OnStartup, after building IServiceProvider:
var db = _serviceProvider.GetRequiredService<AppDbContext>();
db.Database.Migrate();
```

### Raw SQL in Migrations (for FTS5 virtual tables)
```csharp
public partial class AddFts5Tables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE VIRTUAL TABLE IF NOT EXISTS MessageFts USING fts5(
                Content, 
                content=Messages, 
                content_rowid=rowid
            );
        ", suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS MessageFts;", suppressTransaction: true);
    }
}
```

---

## 4. Data Seeding with HasData

```csharp
modelBuilder.Entity<Persona>().HasData(
    new Persona 
    { 
        Id = Guid.NewGuid().ToString("N"), 
        DisplayName = "General Assistant",
        SystemPrompt = "You are a helpful assistant.",
        IsBuiltIn = true,
        DefaultChatMode = "Standard"
    }
);
```

---

## 5. Index Creation

### Via Data Annotation
```csharp
[Index(nameof(DisplayName), IsUnique = true)]
public class Persona { ... }
```

### Via Fluent API
```csharp
modelBuilder.Entity<Message>()
    .HasIndex(m => m.ThreadId);

modelBuilder.Entity<Message>()
    .HasIndex(m => m.CreatedAt);
```

---

## 6. String Primary Keys (GUIDs)

EF Core supports string primary keys. The `[Key]` attribute marks the property:
```csharp
[Key]
public string Id { get; set; } = Guid.NewGuid().ToString("N");
```

For non-standard PK types (like `WikiFile.FilePath`), use `HasPrincipalKey` in `OnModelCreating`.

---

## 7. Repository Pattern with EF Core

Repositories wrap DbContext and expose domain-specific queries:
```csharp
public class ChatThreadRepository : IChatThreadRepository
{
    private readonly AppDbContext _db;
    public ChatThreadRepository(AppDbContext db) => _db = db;

    public async Task<ChatThread?> GetByIdAsync(string id) =>
        await _db.ChatThreads.FindAsync(id);

    public async Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort) =>
        await _db.ChatThreads
            .Where(t => !t.IsTransient && !t.IsDeleted)
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync();
}
```

---

## 8. DbContext Change Tracking for Single-User
For a single-user desktop app, the DbContext is a singleton with no concurrency issues. `SaveChangesAsync()` is used for all writes. For read-only queries, use `AsNoTracking()`:
```csharp
await _db.ChatThreads.AsNoTracking().Where(...).ToListAsync();
```
