using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data.Entities;

namespace MySecondBrain.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
            var dbPath = Environment.GetEnvironmentVariable("MSB_DB_PATH")
                ?? Path.Combine(
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
        // ────────────────────────────────────────────────────────────
        // ChatThread relationships
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<ChatThread>()
            .HasOne(t => t.Persona)
            .WithMany(p => p.ChatThreads)
            .HasForeignKey(t => t.PersonaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ChatThread>()
            .HasOne(t => t.ModelConfig)
            .WithMany(mc => mc.ChatThreads)
            .HasForeignKey(t => t.ModelConfigId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ────────────────────────────────────────────────────────────
        // Message relationships
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Thread)
            .WithMany(t => t.Messages)
            .HasForeignKey(m => m.ThreadId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.ParentMessage)
            .WithMany(m => m.ChildMessages)
            .HasForeignKey(m => m.ParentMessageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Persona)
            .WithMany(p => p.Messages)
            .HasForeignKey(m => m.PersonaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.ModelConfig)
            .WithMany(mc => mc.Messages)
            .HasForeignKey(m => m.ModelConfigId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ────────────────────────────────────────────────────────────
        // Persona → ModelConfiguration (Restrict)
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<Persona>()
            .HasOne(p => p.DefaultModelConfig)
            .WithMany(mc => mc.Personas)
            .HasForeignKey(p => p.DefaultModelConfigId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        // ModelConfiguration → ApiKey (SetNull)
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<ModelConfiguration>()
            .HasOne(mc => mc.ApiKey)
            .WithMany(k => k.ModelConfigurations)
            .HasForeignKey(mc => mc.ApiKeyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ────────────────────────────────────────────────────────────
        // Artifact → ChatThread (Cascade)
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<Artifact>()
            .HasOne(a => a.Thread)
            .WithMany(t => t.Artifacts)
            .HasForeignKey(a => a.ThreadId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // ────────────────────────────────────────────────────────────
        // MediaItem relationships
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<MediaItem>()
            .HasOne(mi => mi.Thread)
            .WithMany(t => t.MediaItems)
            .HasForeignKey(mi => mi.ThreadId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MediaItem>()
            .HasOne(mi => mi.Message)
            .WithMany(m => m.MediaItems)
            .HasForeignKey(mi => mi.MessageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ────────────────────────────────────────────────────────────
        // UsageRecord → Message (1:1, Cascade)
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<UsageRecord>()
            .HasOne(u => u.Message)
            .WithOne(m => m.UsageRecord)
            .HasForeignKey<UsageRecord>(u => u.MessageId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // ────────────────────────────────────────────────────────────
        // UsageRecord denormalized relationships
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<UsageRecord>()
            .HasOne(u => u.Thread)
            .WithMany(t => t.UsageRecords)
            .HasForeignKey(u => u.ThreadId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UsageRecord>()
            .HasOne(u => u.Persona)
            .WithMany(p => p.UsageRecords)
            .HasForeignKey(u => u.PersonaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UsageRecord>()
            .HasOne(u => u.ModelConfig)
            .WithMany(mc => mc.UsageRecords)
            .HasForeignKey(u => u.ModelConfigId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ────────────────────────────────────────────────────────────
        // TextAction → ModelConfiguration (SetNull)
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<TextAction>()
            .HasOne(ta => ta.ModelConfig)
            .WithMany(mc => mc.TextActions)
            .HasForeignKey(ta => ta.ModelConfigId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ────────────────────────────────────────────────────────────
        // WikiVersionSnapshot → WikiFile (alternate key FK, Cascade)
        // ────────────────────────────────────────────────────────────

        modelBuilder.Entity<WikiVersionSnapshot>()
            .HasOne(v => v.WikiFile)
            .WithMany(f => f.WikiVersionSnapshots)
            .HasForeignKey(v => v.WikiFilePath)
            .HasPrincipalKey(f => f.FilePath)
            .OnDelete(DeleteBehavior.Cascade);

        // ────────────────────────────────────────────────────────────
        // Indexes
        // ────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────
        // Seed data — built-in Personas and TextActions
        // Fixed GUIDs for deterministic migrations.
        // ────────────────────────────────────────────────────────────

        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<Persona>().HasData(
            new Persona
            {
                Id = "00000000000000000000000000000001",
                DisplayName = "General Assistant",
                SystemPrompt = "You are a helpful, thoughtful assistant.",
                DefaultChatMode = "Standard",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new Persona
            {
                Id = "00000000000000000000000000000002",
                DisplayName = "Code Helper",
                SystemPrompt = "You are an expert software developer. Provide clean, well-documented code.",
                DefaultChatMode = "Standard",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            }
        );

        modelBuilder.Entity<TextAction>().HasData(
            new TextAction
            {
                Id = "a000000000000000000000000000001",
                DisplayName = "Rewrite",
                SystemPrompt = "Rewrite the following text to improve clarity, flow, and impact while preserving the original meaning.",
                Hotkey = "Alt+Q",
                CaptureScope = "selection",
                ApplyMode = "replaceSelection",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000002",
                DisplayName = "Summarize",
                SystemPrompt = "Summarize the following text concisely, capturing the key points.",
                Hotkey = "Alt+W",
                CaptureScope = "selection",
                ApplyMode = "showOnly",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000003",
                DisplayName = "Explain",
                SystemPrompt = "Explain the following text clearly and thoroughly, as if teaching someone new to the topic.",
                Hotkey = "Alt+E",
                CaptureScope = "selection",
                ApplyMode = "showOnly",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000004",
                DisplayName = "Translate",
                SystemPrompt = "Translate the following text to English. Preserve formatting and tone.",
                Hotkey = "Alt+R",
                CaptureScope = "selection",
                ApplyMode = "replaceSelection",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000005",
                DisplayName = "Fix Grammar",
                SystemPrompt = "Fix grammar, spelling, and punctuation errors in the following text. Preserve the original meaning and style.",
                CaptureScope = "selection",
                ApplyMode = "replaceSelection",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000006",
                DisplayName = "Enhance Prompt",
                SystemPrompt = "Improve the following prompt to be more specific, detailed, and effective. Add relevant context and constraints.",
                CaptureScope = "selection",
                ApplyMode = "replaceSelection",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000007",
                DisplayName = "Continue Writing",
                SystemPrompt = "Continue writing from where the text left off. Match the existing tone, style, and formatting. Maintain coherence with the preceding content.",
                Hotkey = "Alt+C",
                CaptureScope = "focusedElement",
                ApplyMode = "insertAtCursor",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000008",
                DisplayName = "Improve Flow",
                SystemPrompt = "Rewrite the following text to improve logical flow, transitions between ideas, and overall readability while preserving the original meaning.",
                CaptureScope = "focusedElement",
                ApplyMode = "replaceFocusedElement",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000009",
                DisplayName = "Summarize Page",
                SystemPrompt = "Summarize the following content concisely, capturing the key points and overall structure.",
                CaptureScope = "fullDocument",
                ApplyMode = "showOnly",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new TextAction
            {
                Id = "a000000000000000000000000000010",
                DisplayName = "Explain Screen",
                SystemPrompt = "Explain what is shown in the provided content. Describe the layout, key elements, and purpose clearly.",
                CaptureScope = "fullDocument,screenshot",
                ApplyMode = "showOnly",
                IsBuiltIn = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            }
        );
    }
}
