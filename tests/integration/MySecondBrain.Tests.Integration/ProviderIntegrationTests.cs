using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.LLM;

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
}
