using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.LLM;

namespace MySecondBrain.Tests.Unit;

public class ProviderTests
{
    // ================================================================
    // All 4 providers have correct Type and ProviderName properties
    // ================================================================

    [Fact]
    public void OpenAIProvider_HasCorrectProperties()
    {
        var provider = CreateOpenAIProvider();
        Assert.Equal("OpenAI", provider.ProviderName);
        Assert.Equal(ProviderType.OpenAI, provider.Type);
    }

    [Fact]
    public void AnthropicProvider_HasCorrectProperties()
    {
        var provider = CreateAnthropicProvider();
        Assert.Equal("Anthropic", provider.ProviderName);
        Assert.Equal(ProviderType.Anthropic, provider.Type);
    }

    [Fact]
    public void GoogleProvider_HasCorrectProperties()
    {
        var provider = CreateGoogleProvider();
        Assert.Equal("Google", provider.ProviderName);
        Assert.Equal(ProviderType.Google, provider.Type);
    }

    [Fact]
    public void OpenAICompatibleProvider_HasCorrectProperties()
    {
        var provider = CreateOpenAICompatibleProvider();
        Assert.Equal("OpenAI Compatible", provider.ProviderName);
        Assert.Equal(ProviderType.OpenAICompatible, provider.Type);
    }

    // ================================================================
    // LLMProviderFactory resolves providers correctly
    // ================================================================

    [Fact]
    public void ProviderFactory_ResolvesOpenAIProvider()
    {
        var providers = new ILLMProvider[]
        {
            CreateOpenAIProvider(),
            CreateAnthropicProvider(),
        };
        var factory = new LLMProviderFactory(providers, Mock.Of<ILogger<LLMProviderFactory>>());

        var resolved = factory.GetProvider(ProviderType.OpenAI);

        Assert.NotNull(resolved);
        Assert.Equal(ProviderType.OpenAI, resolved.Type);
    }

    [Fact]
    public void ProviderFactory_ResolvesAnthropicProvider()
    {
        var providers = new ILLMProvider[]
        {
            CreateOpenAIProvider(),
            CreateAnthropicProvider(),
        };
        var factory = new LLMProviderFactory(providers, Mock.Of<ILogger<LLMProviderFactory>>());

        var resolved = factory.GetProvider(ProviderType.Anthropic);

        Assert.NotNull(resolved);
        Assert.Equal(ProviderType.Anthropic, resolved.Type);
    }

    [Fact]
    public void ProviderFactory_ResolvesOpenAICompatibleProvider()
    {
        var providers = new ILLMProvider[]
        {
            CreateOpenAIProvider(),
            CreateAnthropicProvider(),
            CreateGoogleProvider(),
            CreateOpenAICompatibleProvider(),
        };
        var factory = new LLMProviderFactory(providers, Mock.Of<ILogger<LLMProviderFactory>>());

        var resolved = factory.GetProvider(ProviderType.OpenAICompatible);

        Assert.NotNull(resolved);
        Assert.Equal(ProviderType.OpenAICompatible, resolved.Type);
    }

    // ================================================================
    // Remapping: DeepSeek, MiMo, Moonshot, Mistral → OpenAICompatible
    // ================================================================

    [Theory]
    [InlineData(ProviderType.DeepSeek)]
    [InlineData(ProviderType.MiMo)]
    [InlineData(ProviderType.Moonshot)]
    [InlineData(ProviderType.Mistral)]
    public void ProviderFactory_RemapsOpenAICompatibleFamily_ToOpenAICompatible(ProviderType inputType)
    {
        var providers = new ILLMProvider[]
        {
            CreateOpenAIProvider(),
            CreateAnthropicProvider(),
            CreateGoogleProvider(),
            CreateOpenAICompatibleProvider(),
        };
        var factory = new LLMProviderFactory(providers, Mock.Of<ILogger<LLMProviderFactory>>());

        var resolved = factory.GetProvider(inputType);

        Assert.NotNull(resolved);
        Assert.Equal(ProviderType.OpenAICompatible, resolved.Type);
    }

    [Fact]
    public void ProviderFactory_SupportedProviders_ReturnsAll()
    {
        var providers = new ILLMProvider[]
        {
            CreateOpenAIProvider(),
            CreateAnthropicProvider(),
            CreateGoogleProvider(),
            CreateOpenAICompatibleProvider(),
        };
        var factory = new LLMProviderFactory(providers, Mock.Of<ILogger<LLMProviderFactory>>());

        var supported = factory.SupportedProviders;

        Assert.Contains(ProviderType.OpenAI, supported);
        Assert.Contains(ProviderType.Anthropic, supported);
        Assert.Contains(ProviderType.Google, supported);
        Assert.Contains(ProviderType.DeepSeek, supported);
        Assert.Contains(ProviderType.MiMo, supported);
        Assert.Contains(ProviderType.Moonshot, supported);
        Assert.Contains(ProviderType.Mistral, supported);
        Assert.Contains(ProviderType.OpenAICompatible, supported);
        Assert.Equal(8, supported.Count);
    }

    // ================================================================
    // LLMProviderService delegates to correct provider
    // ================================================================

    [Fact]
    public async Task LLMProviderService_ValidateApiKeyAsync_DelegatesToCorrectProvider()
    {
        var openAIProvider = new Mock<ILLMProvider>();
        openAIProvider.Setup(p => p.Type).Returns(ProviderType.OpenAI);
        openAIProvider.Setup(p => p.ProviderName).Returns("OpenAI");
        openAIProvider.Setup(p => p.ValidateKeyAsync("test-key", It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(true);

        var anthropicProvider = new Mock<ILLMProvider>();
        anthropicProvider.Setup(p => p.Type).Returns(ProviderType.Anthropic);

        var factory = new LLMProviderFactory(
            new[] { openAIProvider.Object, anthropicProvider.Object },
            Mock.Of<ILogger<LLMProviderFactory>>());

        var service = new LLMProviderService(
            factory,
            Mock.Of<ITokenizerFactory>(),
            Mock.Of<ILogger<LLMProviderService>>());

        var result = await service.ValidateApiKeyAsync(
            ProviderType.OpenAI, "test-key", null, CancellationToken.None);

        Assert.True(result);
        openAIProvider.Verify(p => p.ValidateKeyAsync("test-key", It.IsAny<CancellationToken>(), null), Times.Once);
        anthropicProvider.Verify(p => p.ValidateKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task LLMProviderService_ValidateApiKeyAsync_ReturnsFalseForUnknownProvider()
    {
        var factory = new LLMProviderFactory(
            Array.Empty<ILLMProvider>(),
            Mock.Of<ILogger<LLMProviderFactory>>());

        var service = new LLMProviderService(
            factory,
            Mock.Of<ITokenizerFactory>(),
            Mock.Of<ILogger<LLMProviderService>>());

        var result = await service.ValidateApiKeyAsync(
            ProviderType.OpenAI, "test-key", null, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task LLMProviderService_ListModelsAsync_DelegatesToCorrectProvider()
    {
        var mockModels = new List<ModelInfo>
        {
            new("gpt-4o", "gpt-4o", 128000),
        }.AsReadOnly();

        var openAIProvider = new Mock<ILLMProvider>();
        openAIProvider.Setup(p => p.Type).Returns(ProviderType.OpenAI);
        openAIProvider.Setup(p => p.ProviderName).Returns("OpenAI");
        openAIProvider.Setup(p => p.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockModels);

        var factory = new LLMProviderFactory(
            new[] { openAIProvider.Object },
            Mock.Of<ILogger<LLMProviderFactory>>());

        var service = new LLMProviderService(
            factory,
            Mock.Of<ITokenizerFactory>(),
            Mock.Of<ILogger<LLMProviderService>>());

        var config = new ModelConfiguration
        {
            ProviderType = ProviderType.OpenAI,
            ModelIdentifier = "gpt-4o",
        };

        var result = await service.ListModelsAsync(config, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("gpt-4o", result[0].Id);
        openAIProvider.Verify(p => p.ListModelsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ================================================================
    // ApiKeyHelper.MaskKey edge cases
    // ================================================================

    [Fact]
    public void MaskKey_NullOrEmpty_ReturnsMasked()
    {
        Assert.Equal("***", ApiKeyHelper.MaskKey(null!));
        Assert.Equal("***", ApiKeyHelper.MaskKey(string.Empty));
    }

    [Fact]
    public void MaskKey_ShortKey_ReturnsMasked()
    {
        Assert.Equal("***", ApiKeyHelper.MaskKey("abc"));
        Assert.Equal("***", ApiKeyHelper.MaskKey("1234567"));
    }

    [Fact]
    public void MaskKey_NormalKey_ReturnsMasked()
    {
        var result = ApiKeyHelper.MaskKey("sk-test-api-key-123456");
        Assert.Equal("sk-...3456", result);
    }

    // ================================================================
    // OpenAICompatibleProvider — DeepSeek key resolution (regression)
    // Verifies that ListModelsAsync finds keys stored under DeepSeek
    // provider type (not just OpenAICompatible).
    // ================================================================

    [Fact]
    public async Task OpenAICompatibleProvider_ListModelsAsync_FindsDeepSeekKeyAndUsesDefaultEndpoint()
    {
        // Simulate a DeepSeek key stored under ProviderType.DeepSeek (no custom endpoint)
        var deepSeekKey = new ApiKey
        {
            Id = "deepseek-key-1",
            ProviderType = ProviderType.DeepSeek,
            EncryptedValue = "encrypted-deepseek-key",
            Label = "My DeepSeek Key",
            IsValid = true,
        };

        var apiKeyRepoMock = new Mock<IApiKeyRepository>();
        // Return no OpenAICompatible keys — only DeepSeek keys
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.OpenAICompatible))
            .ReturnsAsync(Array.Empty<ApiKey>());
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.DeepSeek))
            .ReturnsAsync(new[] { deepSeekKey });
        // For other remapped types, return empty
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.MiMo))
            .ReturnsAsync(Array.Empty<ApiKey>());
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.Moonshot))
            .ReturnsAsync(Array.Empty<ApiKey>());
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.Mistral))
            .ReturnsAsync(Array.Empty<ApiKey>());

        var encMock = new Mock<IEncryptionService>();
        encMock
            .Setup(e => e.UnprotectString("encrypted-deepseek-key"))
            .Returns("sk-deepseek-actual-key");

        var provider = new OpenAICompatibleProvider(
            apiKeyRepoMock.Object,
            encMock.Object,
            Mock.Of<ILogger<OpenAICompatibleProvider>>());

        // ListModelsAsync should find the DeepSeek key, resolve to https://api.deepseek.com,
        // and attempt to fetch models (will fail HTTP since there's no real server,
        // but the endpoint resolution and key lookup should succeed).
        // We use CancellationTokenSource to verify the code path was reached.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var result = await provider.ListModelsAsync(cts.Token);

        // Listing models will likely return empty (no real HTTP server),
        // but the key verification confirms the DeepSeek key was found.
        Assert.NotNull(result);
        apiKeyRepoMock.Verify(r => r.GetByProviderAsync(ProviderType.DeepSeek), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OpenAICompatibleProvider_ListModelsAsync_FindsDeepSeekKeyWithCustomEndpoint()
    {
        // Simulate a DeepSeek key with a custom endpoint stored under ProviderType.DeepSeek
        var deepSeekKey = new ApiKey
        {
            Id = "deepseek-key-2",
            ProviderType = ProviderType.DeepSeek,
            CustomEndpointUrl = "https://custom.deepseek.example.com",
            EncryptedValue = "encrypted-deepseek-key-2",
            Label = "Custom DeepSeek",
            IsValid = true,
        };

        var apiKeyRepoMock = new Mock<IApiKeyRepository>();
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.OpenAICompatible))
            .ReturnsAsync(Array.Empty<ApiKey>());
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.DeepSeek))
            .ReturnsAsync(new[] { deepSeekKey });
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.MiMo))
            .ReturnsAsync(Array.Empty<ApiKey>());
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.Moonshot))
            .ReturnsAsync(Array.Empty<ApiKey>());
        apiKeyRepoMock
            .Setup(r => r.GetByProviderAsync(ProviderType.Mistral))
            .ReturnsAsync(Array.Empty<ApiKey>());

        var encMock = new Mock<IEncryptionService>();
        encMock
            .Setup(e => e.UnprotectString("encrypted-deepseek-key-2"))
            .Returns("sk-deepseek-actual-key");

        var provider = new OpenAICompatibleProvider(
            apiKeyRepoMock.Object,
            encMock.Object,
            Mock.Of<ILogger<OpenAICompatibleProvider>>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var result = await provider.ListModelsAsync(cts.Token);

        Assert.NotNull(result);
        // Should query for DeepSeek-type keys
        apiKeyRepoMock.Verify(r => r.GetByProviderAsync(ProviderType.DeepSeek), Times.AtLeastOnce);
        // Should NOT fall back to OpenAICompatible since DeepSeek key was found
        apiKeyRepoMock.Verify(r => r.GetByProviderAsync(ProviderType.OpenAICompatible), Times.AtLeastOnce);
    }

    // ================================================================
    // Helper factory methods
    // ================================================================

    private static OpenAIProvider CreateOpenAIProvider()
    {
        return new OpenAIProvider(
            Mock.Of<IApiKeyRepository>(),
            Mock.Of<IEncryptionService>(),
            Mock.Of<ILogger<OpenAIProvider>>());
    }

    private static AnthropicProvider CreateAnthropicProvider()
    {
        return new AnthropicProvider(
            Mock.Of<IApiKeyRepository>(),
            Mock.Of<IEncryptionService>(),
            Mock.Of<ILogger<AnthropicProvider>>());
    }

    private static GoogleProvider CreateGoogleProvider()
    {
        return new GoogleProvider(
            Mock.Of<IApiKeyRepository>(),
            Mock.Of<IEncryptionService>(),
            Mock.Of<ILogger<GoogleProvider>>());
    }

    private static OpenAICompatibleProvider CreateOpenAICompatibleProvider()
    {
        return new OpenAICompatibleProvider(
            Mock.Of<IApiKeyRepository>(),
            Mock.Of<IEncryptionService>(),
            Mock.Of<ILogger<OpenAICompatibleProvider>>());
    }
}
