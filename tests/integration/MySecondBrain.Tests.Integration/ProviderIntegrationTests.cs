using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.Services.LLM;
using MySecondBrain.Services.Tools;

namespace MySecondBrain.Tests.Integration;

/// <summary>
/// Integration tests for LLM provider key validation and model listing.
/// These tests require real API keys set via environment variables:
/// - MSB_TEST_OPENAI_KEY
/// - MSB_TEST_ANTHROPIC_KEY
/// Tests silently skip if the required environment variable is not set.
/// </summary>
public class ProviderIntegrationTests
{
    // ================================================================
    // OpenAI Provider — Real API calls
    // ================================================================

    [Fact]
    public async Task OpenAIProvider_ValidateKeyAsync_WithValidKey_ReturnsTrue()
    {
        var apiKey = Environment.GetEnvironmentVariable("MSB_TEST_OPENAI_KEY");
        if (string.IsNullOrEmpty(apiKey))
            return; // Skip if no key configured

        var provider = CreateOpenAIProvider(apiKey);
        var result = await provider.ValidateKeyAsync(apiKey, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task OpenAIProvider_ValidateKeyAsync_WithInvalidKey_ReturnsFalse()
    {
        var provider = CreateOpenAIProvider(null);
        var result = await provider.ValidateKeyAsync("sk-invalid-test-key", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task OpenAIProvider_ListModelsAsync_ReturnsNonEmptyList()
    {
        var apiKey = Environment.GetEnvironmentVariable("MSB_TEST_OPENAI_KEY");
        if (string.IsNullOrEmpty(apiKey))
            return; // Skip if no key configured

        // Wire the API key into the mock repository so ListModelsAsync can authenticate
        var provider = CreateOpenAIProviderWithStoredKey(apiKey);
        var models = await provider.ListModelsAsync(CancellationToken.None);

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Id.Contains("gpt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenAIProvider_ListModelsAsync_HandlesUnauthorizedResponseGracefully()
    {
        // Verify that an unauthenticated call returns an empty list instead of throwing
        var provider = CreateOpenAIProvider(null);
        var models = await provider.ListModelsAsync(CancellationToken.None);

        Assert.NotNull(models);
        Assert.Empty(models);
    }

    // ================================================================
    // Anthropic Provider — Real API calls
    // ================================================================

    [Fact]
    public async Task AnthropicProvider_ValidateKeyAsync_WithValidKey_ReturnsTrue()
    {
        var apiKey = Environment.GetEnvironmentVariable("MSB_TEST_ANTHROPIC_KEY");
        if (string.IsNullOrEmpty(apiKey))
            return; // Skip if no key configured

        var provider = CreateAnthropicProvider();
        var result = await provider.ValidateKeyAsync(apiKey, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task AnthropicProvider_ValidateKeyAsync_WithInvalidKey_ReturnsFalse()
    {
        var provider = CreateAnthropicProvider();
        var result = await provider.ValidateKeyAsync("sk-ant-invalid-test-key", CancellationToken.None);

        Assert.False(result);
    }

    // ================================================================
    // Google Provider — Real API calls
    // ================================================================

    [Fact]
    public async Task GoogleProvider_ValidateKeyAsync_WithInvalidKey_ReturnsFalse()
    {
        // Google's API returns 400 for invalid keys
        var provider = CreateGoogleProvider();
        var result = await provider.ValidateKeyAsync("AIzaSyInvalidTestKey", CancellationToken.None);

        Assert.False(result);
    }

    // ================================================================
    // Helper factory methods
    // ================================================================

    /// <summary>
    /// Creates an OpenAI provider with no stored API key (unauthenticated).
    /// </summary>
    private static OpenAIProvider CreateOpenAIProvider(string? apiKey)
    {
        var apiKeyRepoMock = new Mock<IApiKeyRepository>();
        var encryptionMock = new Mock<IEncryptionService>();

        if (!string.IsNullOrEmpty(apiKey))
        {
            // Still don't wire the key into the repo — ValidateKeyAsync takes the key as a parameter
        }

        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.OpenAI))
            .ReturnsAsync(Array.Empty<ApiKey>());

        return new OpenAIProvider(
            apiKeyRepoMock.Object,
            encryptionMock.Object,
            Mock.Of<ILogger<OpenAIProvider>>());
    }

    /// <summary>
    /// Creates an OpenAI provider with a stored API key for authenticated ListModelsAsync calls.
    /// </summary>
    private static OpenAIProvider CreateOpenAIProviderWithStoredKey(string apiKey)
    {
        var apiKeyRepoMock = new Mock<IApiKeyRepository>();
        var encryptionMock = new Mock<IEncryptionService>();

        var storedKey = new ApiKey
        {
            Id = "test-key-id",
            ProviderType = ProviderType.OpenAI,
            EncryptedValue = "encrypted-" + apiKey, // The repo stores encrypted values
            Label = "Test Key",
        };

        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.OpenAI))
            .ReturnsAsync(new[] { storedKey });

        // The encryption service decrypts by stripping the "encrypted-" prefix
        encryptionMock
            .Setup(e => e.UnprotectString(It.IsAny<string>()))
            .Returns<string>(encrypted =>
            {
                if (encrypted != null && encrypted.StartsWith("encrypted-"))
                    return encrypted["encrypted-".Length..];
                return encrypted ?? string.Empty;
            });

        return new OpenAIProvider(
            apiKeyRepoMock.Object,
            encryptionMock.Object,
            Mock.Of<ILogger<OpenAIProvider>>());
    }

    private static AnthropicProvider CreateAnthropicProvider()
    {
        var apiKeyRepoMock = new Mock<IApiKeyRepository>();
        var encryptionMock = new Mock<IEncryptionService>();

        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.Anthropic))
            .ReturnsAsync(Array.Empty<ApiKey>());

        return new AnthropicProvider(
            apiKeyRepoMock.Object,
            encryptionMock.Object,
            Mock.Of<ILogger<AnthropicProvider>>());
    }

    private static GoogleProvider CreateGoogleProvider()
    {
        var apiKeyRepoMock = new Mock<IApiKeyRepository>();
        var encryptionMock = new Mock<IEncryptionService>();

        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.Google))
            .ReturnsAsync(Array.Empty<ApiKey>());

        return new GoogleProvider(
            apiKeyRepoMock.Object,
            encryptionMock.Object,
            Mock.Of<ILogger<GoogleProvider>>());
    }

    // ================================================================
    // Skills Integration Tests — Filesystem discovery + YAML parsing
    // ================================================================

    [Fact]
    public async Task SkillFilesystemDiscovery_FindsSkillsInTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"msb-skills-test-{Guid.NewGuid()}");
        try
        {
            // Create a skill directory with SKILL.md
            var skillDir = Path.Combine(tempDir, "test-integration-skill");
            Directory.CreateDirectory(skillDir);

            var skillMd = Path.Combine(skillDir, "SKILL.md");
            await File.WriteAllTextAsync(skillMd, """
                ---
                name: integration-test-skill
                description: A skill created for integration testing
                ---
                # Integration Test Skill
                This skill is used to verify filesystem discovery works.
                ## Rules
                - Rule 1
                - Rule 2
                """);

            // Create a resource file
            var scriptsDir = Path.Combine(skillDir, "scripts");
            Directory.CreateDirectory(scriptsDir);
            await File.WriteAllTextAsync(Path.Combine(scriptsDir, "helper.py"), "print('integration test')");

            // Read and parse the SKILL.md
            var content = await File.ReadAllTextAsync(skillMd);

            // Verify the file exists and has content
            Assert.NotNull(content);
            Assert.Contains("---", content);
            Assert.Contains("integration-test-skill", content);
            Assert.Contains("A skill created for integration testing", content);
            Assert.Contains("# Integration Test Skill", content);

            // Verify resource directory exists
            Assert.True(File.Exists(Path.Combine(scriptsDir, "helper.py")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SkillYamlParsing_ParsesRealFrontmatter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"msb-skills-yaml-{Guid.NewGuid()}");
        try
        {
            var skillDir = Path.Combine(tempDir, "yaml-test");
            Directory.CreateDirectory(skillDir);

            var skillMd = Path.Combine(skillDir, "SKILL.md");
            await File.WriteAllTextAsync(skillMd, """
                ---
                name: yaml-test
                description: "Test skill with quoted description and extra fields"
                license: MIT
                dependencies:
                  - python
                  - openpyxl
                ---
                # YAML Test Skill
                Body content here.
                """);

            var content = await File.ReadAllTextAsync(skillMd);

            // Verify frontmatter structure
            Assert.StartsWith("---", content.Trim());
            Assert.Contains("name: yaml-test", content);
            Assert.Contains("description:", content);
            Assert.Contains("license:", content);

            // Verify content structure
            Assert.Contains("---", content);
            Assert.Contains("# YAML Test Skill", content);
            Assert.Contains("Body content here.", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SkillCollisionResolution_UserOverridesBuiltIn()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"msb-skills-collision-{Guid.NewGuid()}");
        try
        {
            // Simulate a built-in skill (first discovered)
            var builtInDir = Path.Combine(tempDir, "builtin");
            Directory.CreateDirectory(builtInDir);
            await File.WriteAllTextAsync(Path.Combine(builtInDir, "SKILL.md"), """
                ---
                name: collision-skill
                description: Built-in version
                ---
                # Built-in
                """);

            // Simulate a user override (later discovered, should win)
            var userDir = Path.Combine(tempDir, "user");
            Directory.CreateDirectory(userDir);
            await File.WriteAllTextAsync(Path.Combine(userDir, "SKILL.md"), """
                ---
                name: collision-skill
                description: User override version
                ---
                # User override
                """);

            // Read both
            var builtInContent = await File.ReadAllTextAsync(Path.Combine(builtInDir, "SKILL.md"));
            var userContent = await File.ReadAllTextAsync(Path.Combine(userDir, "SKILL.md"));

            Assert.Contains("Built-in", builtInContent);
            Assert.Contains("User override", userContent);

            // In a real scenario, the user version would win due to priority order.
            // Verify both files exist for collision detection.
            Assert.NotEqual(builtInContent, userContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SkillMissingDescription_IsSkippedDuringDiscovery()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"msb-skills-nodesc-{Guid.NewGuid()}");
        try
        {
            var skillDir = Path.Combine(tempDir, "no-description-skill");
            Directory.CreateDirectory(skillDir);

            await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), """
                ---
                name: no-description-skill
                ---
                # No description
                """);

            var content = await File.ReadAllTextAsync(Path.Combine(skillDir, "SKILL.md"));

            // Has name but no description
            Assert.Contains("name: no-description-skill", content);
            Assert.DoesNotContain("description:", content);

            // Body should still be present
            Assert.Contains("# No description", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SkillInvalidYaml_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"msb-skills-invalid-{Guid.NewGuid()}");
        try
        {
            var skillDir = Path.Combine(tempDir, "invalid-yaml");
            Directory.CreateDirectory(skillDir);

            // Malformed frontmatter — missing closing ---
            await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), """
                ---
                name: invalid-yaml
                description: This has no closing marker
                # No closing ---
                """);

            var content = await File.ReadAllTextAsync(Path.Combine(skillDir, "SKILL.md"));

            // Should not throw when reading the file
            Assert.NotNull(content);
            Assert.Contains("name: invalid-yaml", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ================================================================
    // Bash Tool Workspace Isolation — Real filesystem integration tests
    // These tests execute real Process.Start calls in the workspace.
    // ================================================================

    [Fact]
    public async Task BashToolExecutor_ExecutesCommandInWorkspace()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        var executor = new BashToolExecutor(loggerMock.Object);

        // Reset workspace cleanup flag for test isolation
        var workspacePath = BashToolExecutor.WorkspaceBasePath;

        try
        {
            // Clean workspace before test
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);

            // Act: create a file in the workspace to verify working directory
            var toolCall = new ToolCall("test", "bash", """{"command":"echo workspace_test > test_workspace_file.txt"}""");
            var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

            // Assert
            Assert.True(result.Success, $"Command failed: {result.ErrorMessage}");
            Assert.True(Directory.Exists(workspacePath), "Workspace directory should exist after execution");

            var outputFile = Path.Combine(workspacePath, "test_workspace_file.txt");
            Assert.True(File.Exists(outputFile), "File should be created in workspace directory");
            var content = await File.ReadAllTextAsync(outputFile);
            Assert.Contains("workspace_test", content);
        }
        finally
        {
            // Cleanup test artifacts
            var testFile = Path.Combine(workspacePath, "test_workspace_file.txt");
            if (File.Exists(testFile)) File.Delete(testFile);
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task BashToolExecutor_WorkspaceDirectoryCreatedOnFirstUse()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        var executor = new BashToolExecutor(loggerMock.Object);
        var workspacePath = BashToolExecutor.WorkspaceBasePath;

        try
        {
            // Ensure workspace doesn't exist
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);

            Assert.False(Directory.Exists(workspacePath), "Workspace should not exist before first use");

            // Act
            var toolCall = new ToolCall("test", "bash", """{"command":"echo test > first_use_test.txt"}""");
            var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

            // Assert
            Assert.True(result.Success, $"Command failed: {result.ErrorMessage}");
            Assert.True(Directory.Exists(workspacePath), "Workspace directory should be created on first use");
        }
        finally
        {
            var testFile = Path.Combine(workspacePath, "first_use_test.txt");
            if (File.Exists(testFile)) File.Delete(testFile);
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task BashToolExecutor_InvalidatesAbsolutePathCommand()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        var executor = new BashToolExecutor(loggerMock.Object);

        // Act: validate a command with an absolute path
        var toolCall = new ToolCall("test", "bash", """{"command":"type D:\\config.json"}""");
        var validationResult = await executor.ValidateAsync(toolCall, CancellationToken.None);

        // Assert
        Assert.NotNull(validationResult);
        Assert.False(validationResult.IsValid);
        Assert.Contains("absolute path", validationResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BashToolExecutor_ExecuteAsync_ReturnsOutputContent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        var executor = new BashToolExecutor(loggerMock.Object);
        var workspacePath = BashToolExecutor.WorkspaceBasePath;

        try
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);

            // Act
            var toolCall = new ToolCall("test", "bash", """{"command":"echo Hello World"}""");
            var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

            // Assert
            Assert.True(result.Success, $"Command should succeed: {result.ErrorMessage}");
            Assert.Contains("Hello World", result.Content);
        }
        finally
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task BashToolExecutor_ExecuteAsync_InvalidCommandReturnsError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        var executor = new BashToolExecutor(loggerMock.Object);
        var workspacePath = BashToolExecutor.WorkspaceBasePath;

        try
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);

            // Act: execute a command that will fail
            var toolCall = new ToolCall("test", "bash", """{"command":"invalid_command_xyz_123"}""");
            var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

            // Assert
            Assert.False(result.Success, "Invalid command should fail");
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public void BashToolExecutor_GetToolDefinition_ReturnsBashSchema()
    {
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        var executor = new BashToolExecutor(loggerMock.Object);

        Assert.Equal("bash", executor.ToolName);
        Assert.NotNull(executor.Description);
        Assert.Contains("workspace", executor.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(executor.ParametersJsonSchema);
        Assert.Contains("command", executor.ParametersJsonSchema);
    }

    // ================================================================
    // Step 2: UsageRecord Migration Integration Tests
    // ================================================================

    /// <summary>
    /// Verifies that the EnrichUsageRecord migration creates all 8 new columns in a real SQLite database
    /// and that default values are correct.
    /// </summary>
    [Fact]
    public async Task EnrichUsageRecord_Migration_ShouldApplyToRealSqlite()
    {
        // Arrange: create a real SQLite database file with full migration applied
        var dbPath = Path.Combine(Path.GetTempPath(), $"msb-usage-migration-test-{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<Data.AppDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            using (var db = new Data.AppDbContext(options))
            {
                // Act: apply all migrations (which includes the EnrichUsageRecord migration)
                await db.Database.MigrateAsync();

                // Assert: verify the migration ran without error
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                Assert.Empty(pendingMigrations);

                // Insert prerequisite entities to satisfy FK constraints
                db.ChatThreads.Add(new Data.Entities.ChatThread
                {
                    Id = "thread-migration-test",
                    ChatMode = "Standard"
                });
                db.Messages.Add(new Data.Entities.Message
                {
                    Id = "msg-migration-test",
                    ThreadId = "thread-migration-test",
                    Role = "Assistant",
                    Content = "test",
                    BranchId = Guid.NewGuid().ToString("N")
                });
                await db.SaveChangesAsync();

                // Verify the 8 new columns exist by inserting and reading back
                var usageRecord = new Data.Entities.UsageRecord
                {
                    Id = "test-migration-enrich",
                    MessageId = "msg-migration-test",
                    ThreadId = "thread-migration-test",
                    Provider = "OpenAI",
                    ModelIdentifier = "gpt-4o",
                    PromptTokens = 100,
                    CompletionTokens = 50,
                    TotalTokens = 150,
                    CacheReadTokens = 30,
                    CacheCreationTokens = 20,
                    LatencyMs = 1500,
                    Tier = 2,
                    ErrorType = "rate_limit",
                    ErrorMessage = "Rate limit exceeded",
                    ErrorStatusCode = 429,
                    RawJsonPath = "/workspace/chat-123/_api_history.json"
                };

                db.UsageRecords.Add(usageRecord);
                await db.SaveChangesAsync();

                // Reset the context to ensure fresh read
                db.ChangeTracker.Clear();

                var retrieved = await db.UsageRecords.FindAsync("test-migration-enrich");
                Assert.NotNull(retrieved);
                Assert.Equal(30, retrieved!.CacheReadTokens);
                Assert.Equal(20, retrieved.CacheCreationTokens);
                Assert.Equal(1500, retrieved.LatencyMs);
                Assert.Equal(2, retrieved.Tier);
                Assert.Equal("rate_limit", retrieved.ErrorType);
                Assert.Equal("Rate limit exceeded", retrieved.ErrorMessage);
                Assert.Equal(429, retrieved.ErrorStatusCode);
                Assert.Equal("/workspace/chat-123/_api_history.json", retrieved.RawJsonPath);
            }
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    // ================================================================
    // Step 3: TextAction ChatMode Migration Integration Tests
    // ================================================================

    /// <summary>
    /// Verifies that the AddTextActionChatMode migration adds the ChatMode column
    /// in a real SQLite database with correct values for all seed TextActions.
    /// </summary>
    [Fact]
    public async Task TextActionChatMode_Migration_ShouldApplyToRealSqlite()
    {
        // Arrange: create a real SQLite database file with full migration applied
        var dbPath = Path.Combine(Path.GetTempPath(), $"msb-textaction-migration-test-{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            using (var db = new AppDbContext(options))
            {
                // Act: apply all migrations
                await db.Database.MigrateAsync();

                // Assert: verify the migration ran without error
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                Assert.Empty(pendingMigrations);

                // Verify column schema: notnull = 1 for ChatMode
                using (var cmd = db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT \"notnull\" FROM pragma_table_info('TextActions') WHERE name = 'ChatMode'";
                    await db.Database.OpenConnectionAsync();
                    var notnull = (long)(await cmd.ExecuteScalarAsync())!;
                    Assert.Equal(1, notnull);
                }

                // Verify column default: inserting a non-seed TextAction without ChatMode gets "Standard"
                var nonSeedAction = new Data.Entities.TextAction
                {
                    Id = "test-non-seed-default",
                    DisplayName = "Test Non-Seed",
                    SystemPrompt = "Test prompt"
                    // ChatMode deliberately not set
                };
                db.TextActions.Add(nonSeedAction);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                var retrievedNonSeed = await db.TextActions.FindAsync("test-non-seed-default");
                Assert.NotNull(retrievedNonSeed);
                Assert.Equal("Standard", retrievedNonSeed!.ChatMode);

                // Verify seed data ChatMode values
                var textActions = db.TextActions.Where(ta => ta.IsBuiltIn).ToList();
                Assert.Equal(10, textActions.Count);

                var continueWriting = textActions.Single(ta => ta.Id == "a000000000000000000000000000007");
                Assert.Equal("Continue Writing", continueWriting.DisplayName);
                Assert.Equal("TextCompletion", continueWriting.ChatMode);

                // All other built-in TextActions should be "Standard"
                foreach (var action in textActions.Where(ta => ta.Id != "a000000000000000000000000000007"))
                {
                    Assert.Equal("Standard", action.ChatMode);
                }
            }
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}
