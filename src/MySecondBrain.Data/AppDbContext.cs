using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data.Entities;

namespace MySecondBrain.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
                "MySecondBrain",
                "msb.db");

            var dbDir = Path.GetDirectoryName(dbPath);
            if (dbDir is not null)
            {
                Directory.CreateDirectory(dbDir);
            }

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // WikiVersionSnapshot FK: WikiFilePath references WikiFile.FilePath (non-standard PK name)
        modelBuilder.Entity<WikiVersionSnapshot>()
            .HasOne(v => v.WikiFile)
            .WithMany(f => f.WikiVersionSnapshots)
            .HasForeignKey(v => v.WikiFilePath)
            .HasPrincipalKey(f => f.FilePath);
    }
}
