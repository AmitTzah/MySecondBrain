using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;

namespace MySecondBrain.Tests.Unit;

public abstract class DataLayerTestBase
{
    /// <summary>
    /// Creates an in-memory SQLite AppDbContext for testing.
    /// Keeps the connection open so the database persists for the lifetime of the context.
    /// Caller is responsible for disposing both the context and the connection.
    /// </summary>
    protected static (AppDbContext Db, SqliteConnection Connection) CreateTestDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    /// <summary>
    /// Creates an in-memory SQLite context and applies the full migration
    /// (including FTS5 virtual tables and seed data).
    /// </summary>
    protected static (AppDbContext Db, SqliteConnection Connection) CreateTestDbContextWithMigration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.Migrate();
        return (db, connection);
    }

    /// <summary>
    /// Determines whether a property is a navigation property (reference or collection)
    /// based on its type. Navigation properties point to other entities.
    /// </summary>
    protected static bool IsNavigationProperty(PropertyInfo property)
    {
        var propType = property.PropertyType;

        // Reference navigation: property type is an entity class
        if (propType.IsClass && propType != typeof(string) && propType.Namespace == typeof(ApiKey).Namespace)
            return true;

        // Collection navigation: ICollection<T> where T is an entity
        if (propType.IsGenericType)
        {
            var genericTypeDef = propType.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(ICollection<>))
            {
                var elementType = propType.GenericTypeArguments[0];
                if (elementType.Namespace == typeof(ApiKey).Namespace)
                    return true;
            }
        }

        return false;
    }
}
