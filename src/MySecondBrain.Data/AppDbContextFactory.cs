using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MySecondBrain.Data;

/// <summary>
/// Design-time DbContext factory for EF Core migrations.
/// The EF CLI tools discover this class automatically to create
/// and apply migrations without running the full application.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
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

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new AppDbContext(optionsBuilder.Options);
    }
}
