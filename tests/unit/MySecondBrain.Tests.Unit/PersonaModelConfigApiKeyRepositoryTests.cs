using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;
using MySecondBrain.Data.Repositories;
using CoreModels = MySecondBrain.Core.Models;

namespace MySecondBrain.Tests.Unit;

public class PersonaModelConfigApiKeyRepositoryTests : DataLayerTestBase
{
    // ════════════════════════════════════════════════════════════════
    // PersonaRepository, ModelConfigurationRepository, ApiKeyRepository
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PersonaRepository_CreateAsync_ReturnsCreatedPersona()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var persona = new CoreModels.Persona { DisplayName = "Test Persona", SystemPrompt = "You are a test." };
            var result = await repo.CreateAsync(persona);
            Assert.NotNull(result);
            Assert.Equal("Test Persona", result.DisplayName);
            Assert.Equal("You are a test.", result.SystemPrompt);
            Assert.NotEmpty(result.Id);
        }
    }

    [Fact]
    public async Task PersonaRepository_GetByIdAsync_ReturnsCorrectPersona()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Find Me" });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal("Find Me", found!.DisplayName);
        }
    }

    [Fact]
    public async Task PersonaRepository_GetByIdAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            Assert.Null(await repo.GetByIdAsync("nonexistent-id"));
        }
    }

    [Fact]
    public async Task PersonaRepository_GetAllAsync_ReturnsAllPersonas()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            await repo.CreateAsync(new CoreModels.Persona { DisplayName = "A" });
            await repo.CreateAsync(new CoreModels.Persona { DisplayName = "B" });
            var results = await repo.GetAllAsync();
            Assert.Equal(4, results.Count); // 2 seed personas + 2 created
        }
    }

    [Fact]
    public async Task PersonaRepository_GetDefaultAsync_ReturnsBuiltInOrFirst()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            // Seed data has 2 built-in personas; ordered by Id, "General Assistant" (000...01) comes first
            var defaultPersona = await repo.GetDefaultAsync();
            Assert.NotNull(defaultPersona);
            Assert.True(defaultPersona!.IsBuiltIn);
            Assert.Equal("00000000000000000000000000000001", defaultPersona.Id);
            Assert.Equal("General Assistant", defaultPersona.DisplayName);

            // Add a non-default persona and verify built-in is still returned as default
            await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Custom", IsBuiltIn = false });
            var stillDefault = await repo.GetDefaultAsync();
            Assert.NotNull(stillDefault);
            Assert.True(stillDefault!.IsBuiltIn);
            Assert.Equal("00000000000000000000000000000001", stillDefault.Id);
        }
    }

    [Fact]
    public async Task PersonaRepository_GetDefaultAsync_FallbackToFirstAvailable()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Delete all seed personas so no built-in exists
            var allSeed = db.Personas.ToList();
            db.Personas.RemoveRange(allSeed);
            await db.SaveChangesAsync();

            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Only Persona", IsBuiltIn = false });
            var defaultPersona = await repo.GetDefaultAsync();
            Assert.NotNull(defaultPersona);
            Assert.Equal(created.Id, defaultPersona!.Id);
            Assert.Equal("Only Persona", defaultPersona.DisplayName);
        }
    }

    [Fact]
    public async Task PersonaRepository_UpdateAsync_UpdatesName()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "Original" });
            created.DisplayName = "Updated";
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated", updated!.DisplayName);
        }
    }

    [Fact]
    public async Task PersonaRepository_DeleteAsync_RemovesPersona()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new PersonaRepository(db);
            var created = await repo.CreateAsync(new CoreModels.Persona { DisplayName = "To Delete" });
            await repo.DeleteAsync(created.Id);
            Assert.Null(await repo.GetByIdAsync(created.Id));
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_CreateAsync_ReturnsCreatedConfig()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var config = new CoreModels.ModelConfiguration
            {
                DisplayName = "GPT-4o",
                ProviderType = CoreModels.ProviderType.OpenAI,
                ModelIdentifier = "gpt-4o",
                Temperature = 0.8,
                MaxOutputTokens = 8192
            };
            var result = await repo.CreateAsync(config);
            Assert.NotNull(result);
            Assert.Equal("GPT-4o", result.DisplayName);
            Assert.Equal(CoreModels.ProviderType.OpenAI, result.ProviderType);
            Assert.Equal("gpt-4o", result.ModelIdentifier);
            Assert.NotEmpty(result.Id);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_GetByIdAsync_ReturnsCorrectConfig()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                DisplayName = "Claude Sonnet",
                ProviderType = CoreModels.ProviderType.Anthropic,
                ModelIdentifier = "claude-sonnet-4-20250514"
            });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal("Claude Sonnet", found!.DisplayName);
            Assert.Equal(CoreModels.ProviderType.Anthropic, found.ProviderType);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_DeleteAsync_ReferencedByPersona_NullifiesFk()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create a model config and a persona referencing it
            var configEntity = new ModelConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Referenced Config",
                Provider = "OpenAI"
            };
            db.ModelConfigurations.Add(configEntity);

            var personaEntity = new Persona
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Test Persona",
                DefaultModelConfigId = configEntity.Id
            };
            db.Personas.Add(personaEntity);
            await db.SaveChangesAsync();

            var repo = new ModelConfigurationRepository(db);
            await repo.DeleteAsync(configEntity.Id);

            // Config should be deleted
            Assert.Null(await db.ModelConfigurations.FindAsync(configEntity.Id));

            // Persona FK should be nullified
            var persona = await db.Personas.FindAsync(personaEntity.Id);
            Assert.NotNull(persona);
            Assert.Null(persona!.DefaultModelConfigId);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_DeleteAsync_NoReferences_Succeeds()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                DisplayName = "Unreferenced Config",
                ProviderType = CoreModels.ProviderType.OpenAI,
                ModelIdentifier = "gpt-4o"
            });
            // Should not throw
            await repo.DeleteAsync(created.Id);
            Assert.Null(await repo.GetByIdAsync(created.Id));
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_GetAllAsync_ReturnsAllConfigs()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                DisplayName = "GPT-4o", ProviderType = CoreModels.ProviderType.OpenAI, ModelIdentifier = "gpt-4o"
            });
            await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                DisplayName = "Claude", ProviderType = CoreModels.ProviderType.Anthropic, ModelIdentifier = "claude-3"
            });
            var results = await repo.GetAllAsync();
            Assert.Equal(2, results.Count);
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_GetByIdAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            Assert.Null(await repo.GetByIdAsync("nonexistent-id"));
        }
    }

    [Fact]
    public async Task ModelConfigurationRepository_UpdateAsync_UpdatesAllProperties()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ModelConfigurationRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ModelConfiguration
            {
                DisplayName = "Original Config",
                ProviderType = CoreModels.ProviderType.Google,
                ModelIdentifier = "gemini-pro",
                Temperature = 0.5,
                MaxOutputTokens = 2048,
                MaxContextWindow = 64000,
                ThinkingEnabled = false,
                ContextOverflowStrategy = "SlidingWindow"
            });
            created.DisplayName = "Updated Config";
            created.ProviderType = CoreModels.ProviderType.Anthropic;
            created.ModelIdentifier = "claude-3-opus";
            created.Temperature = 0.9;
            created.MaxOutputTokens = 8192;
            created.MaxContextWindow = 128000;
            created.ThinkingEnabled = true;
            created.ApiKeyId = null; // Nullable FK; set to null to avoid FK constraint
            created.PricingInputPer1K = 2.50m;
            created.PricingOutputPer1K = 10.00m;
            created.ContextOverflowStrategy = "AutoSummarize";
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated Config", updated!.DisplayName);
            Assert.Equal(CoreModels.ProviderType.Anthropic, updated.ProviderType);
            Assert.Equal("claude-3-opus", updated.ModelIdentifier);
            Assert.Equal(0.9, updated.Temperature);
            Assert.Equal(8192, updated.MaxOutputTokens);
            Assert.Equal(128000, updated.MaxContextWindow);
            Assert.True(updated.ThinkingEnabled);
            Assert.Null(updated.ApiKeyId); // Set to null in update to avoid FK constraint
            Assert.Equal(2.50m, updated.PricingInputPer1K);
            Assert.Equal(10.00m, updated.PricingOutputPer1K);
            Assert.Equal("AutoSummarize", updated.ContextOverflowStrategy);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_CreateAsync_ReturnsCreatedKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var key = new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "encrypted-key-value",
                Label = "My OpenAI Key"
            };
            var result = await repo.CreateAsync(key);
            Assert.NotNull(result);
            Assert.Equal(CoreModels.ProviderType.OpenAI, result.ProviderType);
            Assert.Equal("encrypted-key-value", result.EncryptedValue);
            Assert.Equal("My OpenAI Key", result.Label);
            Assert.NotEmpty(result.Id);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_GetByIdAsync_ReturnsCorrectKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.Anthropic,
                EncryptedValue = "anthropic-key",
                Label = "Claude Key"
            });
            var found = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(found);
            Assert.Equal(CoreModels.ProviderType.Anthropic, found!.ProviderType);
            Assert.Equal("Claude Key", found.Label);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_GetAllAsync_ReturnsAllKeys()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "key1",
                Label = "Key 1"
            });
            await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.Google,
                EncryptedValue = "key2",
                Label = "Key 2"
            });
            var results = await repo.GetAllAsync();
            Assert.Equal(2, results.Count);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_GetByIdAsync_NotFound_ReturnsNull()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            Assert.Null(await repo.GetByIdAsync("nonexistent-id"));
        }
    }

    [Fact]
    public async Task ApiKeyRepository_UpdateAsync_UpdatesAllProperties()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "original-value",
                Label = "Original Label"
            });
            created.Label = "Updated Label";
            created.ProviderType = CoreModels.ProviderType.Anthropic;
            created.EncryptedValue = "updated-value";
            await repo.UpdateAsync(created);
            var updated = await repo.GetByIdAsync(created.Id);
            Assert.Equal("Updated Label", updated!.Label);
            Assert.Equal(CoreModels.ProviderType.Anthropic, updated.ProviderType);
            Assert.Equal("updated-value", updated.EncryptedValue);
        }
    }

    [Fact]
    public async Task ApiKeyRepository_DeleteAsync_RemovesKey()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            var repo = new ApiKeyRepository(db);
            var created = await repo.CreateAsync(new CoreModels.ApiKey
            {
                ProviderType = CoreModels.ProviderType.OpenAI,
                EncryptedValue = "to-delete",
                Label = "Delete Me"
            });
            await repo.DeleteAsync(created.Id);
            Assert.Null(await repo.GetByIdAsync(created.Id));
        }
    }

    [Fact]
    public async Task ApiKeyRepository_DeleteAsync_NullifiesModelConfigReferences()
    {
        var (db, connection) = CreateTestDbContext();
        using (db)
        using (connection)
        {
            // Create an API key and a ModelConfiguration referencing it
            var keyEntity = new ApiKey
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Test Key",
                Provider = "OpenAI",
                KeyValue = "test-value"
            };
            db.ApiKeys.Add(keyEntity);

            var configEntity = new ModelConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "Config With Key",
                Provider = "OpenAI",
                ApiKeyId = keyEntity.Id
            };
            db.ModelConfigurations.Add(configEntity);
            await db.SaveChangesAsync();

            var repo = new ApiKeyRepository(db);
            await repo.DeleteAsync(keyEntity.Id);

            // Key should be deleted
            Assert.Null(await repo.GetByIdAsync(keyEntity.Id));

            // ModelConfiguration FK should be nullified
            var config = await db.ModelConfigurations.FindAsync(configEntity.Id);
            Assert.NotNull(config);
            Assert.Null(config!.ApiKeyId);
        }
    }
}
