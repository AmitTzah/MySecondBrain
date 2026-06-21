using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class SettingsViewModelTests
{
    private readonly Mock<ISettingsRepository> _settingsRepoMock;
    private readonly Mock<IThemeProvider> _themeProviderMock;
    private readonly Mock<IApiKeyRepository> _apiKeyRepoMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<ILLMProviderService> _llmProviderServiceMock;
    private readonly Mock<IClipboardService> _clipboardServiceMock;
    private readonly Mock<IConfirmationService> _confirmationServiceMock;
    private readonly Mock<IModelConfigurationRepository> _modelConfigRepoMock;
    private readonly Mock<IPersonaRepository> _personaRepoMock;
    private readonly Mock<ILogger<SettingsViewModel>> _loggerMock;
    private readonly SettingsViewModel _sut;

    public SettingsViewModelTests()
    {
        _settingsRepoMock = new Mock<ISettingsRepository>();
        _themeProviderMock = new Mock<IThemeProvider>();
        _apiKeyRepoMock = new Mock<IApiKeyRepository>();
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _llmProviderServiceMock = new Mock<ILLMProviderService>();
        _clipboardServiceMock = new Mock<IClipboardService>();
        _confirmationServiceMock = new Mock<IConfirmationService>();
        _modelConfigRepoMock = new Mock<IModelConfigurationRepository>();
        _personaRepoMock = new Mock<IPersonaRepository>();
        _loggerMock = new Mock<ILogger<SettingsViewModel>>();

        // Default: confirmations are accepted
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _sut = new SettingsViewModel(
            _settingsRepoMock.Object,
            _themeProviderMock.Object,
            _apiKeyRepoMock.Object,
            _encryptionServiceMock.Object,
            _llmProviderServiceMock.Object,
            _clipboardServiceMock.Object,
            _confirmationServiceMock.Object,
            _modelConfigRepoMock.Object,
            _personaRepoMock.Object,
            _loggerMock.Object);
    }

    // ================================================================
    // MaskedValue computation
    // ================================================================

    [Fact]
    public void MaskPlaintext_ShortKey_ReturnsMask()
    {
        var result = ApiKeyDisplayItem.MaskPlaintext("abc");
        Assert.Equal("***", result);
    }

    [Fact]
    public void MaskPlaintext_NullKey_ReturnsMask()
    {
        var result = ApiKeyDisplayItem.MaskPlaintext(null!);
        Assert.Equal("***", result);
    }

    [Fact]
    public void MaskPlaintext_EmptyKey_ReturnsMask()
    {
        var result = ApiKeyDisplayItem.MaskPlaintext(string.Empty);
        Assert.Equal("***", result);
    }

    [Fact]
    public void MaskPlaintext_TypicalKey_ReturnsMaskedFormat()
    {
        // First 3 + "..." + last 4 = "sk-...f456"
        var result = ApiKeyDisplayItem.MaskPlaintext("sk-abc123def456");
        Assert.Equal("sk-...f456", result);
    }

    [Fact]
    public void MaskPlaintext_LongKey_ReturnsCorrectMask()
    {
        var result = ApiKeyDisplayItem.MaskPlaintext("sk-proj-openai-key-abcdefghijklmnop");
        Assert.Equal("sk-...mnop", result);
    }

    // ================================================================
    // ApiKeyDisplayItem.MaskedValue
    // ================================================================

    [Fact]
    public void MaskedValue_EmptyEncrypted_ReturnsPlaceholder()
    {
        var item = new ApiKeyDisplayItem { EncryptedValue = string.Empty };
        Assert.Equal("••••••••", item.MaskedValue);
    }

    [Fact]
    public void MaskedValue_ShortEncrypted_ReturnsPlaceholder()
    {
        var item = new ApiKeyDisplayItem { EncryptedValue = "short" };
        Assert.Equal("••••••••", item.MaskedValue);
    }

    // ================================================================
    // SettingsCategory enum / default
    // ================================================================

    [Fact]
    public void DefaultCategory_IsProviders()
    {
        Assert.Equal(SettingsCategory.Providers, _sut.SelectedSettingsCategory);
    }

    [Fact]
    public void CategoryItems_ContainsAllCategories()
    {
        Assert.Equal(15, _sut.CategoryItems.Count);
        Assert.Contains(_sut.CategoryItems, c => c.Category == SettingsCategory.Providers);
        Assert.Contains(_sut.CategoryItems, c => c.Category == SettingsCategory.Diagnostics);
    }

    // ================================================================
    // AddApiKeyCommand
    // ================================================================

    [Fact]
    public void AddApiKeyCommand_SetsEditModeAndClearsForm()
    {
        // Pre-set some values to verify they get cleared
        _sut.ApiKeyInputValue = "old-key";
        _sut.DisplayNameInputValue = "Old Name";
        _sut.TestResultMessage = "Some result";

        _sut.AddApiKeyCommand.Execute(null);

        Assert.True(_sut.IsEditingKey);
        Assert.Null(_sut.EditingApiKey);
        Assert.Equal(ProviderType.OpenAI, _sut.SelectedProviderType);
        Assert.Empty(_sut.ApiKeyInputValue);
        Assert.Empty(_sut.DisplayNameInputValue);
        Assert.Empty(_sut.CustomProviderNameValue);
        Assert.Empty(_sut.CustomEndpointUrlValue);
        Assert.Empty(_sut.TestResultMessage);
        Assert.False(_sut.IsTestSuccess);
        Assert.False(_sut.IsOpenAiCompatibleSelected);
    }

    // ================================================================
    // TestApiKeyCommand
    // ================================================================

    [Fact]
    public async Task TestApiKeyCommand_ValidKey_SetsSuccess()
    {
        _sut.SelectedProviderType = ProviderType.OpenAI;
        _sut.ApiKeyInputValue = "sk-valid-key-12345";

        _llmProviderServiceMock
            .Setup(s => s.ValidateApiKeyAsync(ProviderType.OpenAI, "sk-valid-key-12345", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.TestApiKeyCommand.ExecuteAsync(null);

        Assert.True(_sut.IsTestSuccess);
        Assert.Contains("validated", _sut.TestResultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(_sut.IsTesting);
    }

    [Fact]
    public async Task TestApiKeyCommand_InvalidKey_SetsError()
    {
        _sut.SelectedProviderType = ProviderType.Anthropic;
        _sut.ApiKeyInputValue = "sk-invalid-key";

        _llmProviderServiceMock
            .Setup(s => s.ValidateApiKeyAsync(ProviderType.Anthropic, "sk-invalid-key", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.TestApiKeyCommand.ExecuteAsync(null);

        Assert.False(_sut.IsTestSuccess);
        Assert.Contains("failed", _sut.TestResultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(_sut.IsTesting);
    }

    [Fact]
    public async Task TestApiKeyCommand_EmptyKey_ShowsPrompt()
    {
        _sut.ApiKeyInputValue = string.Empty;

        await _sut.TestApiKeyCommand.ExecuteAsync(null);

        Assert.False(_sut.IsTestSuccess);
        Assert.Contains("enter", _sut.TestResultMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestApiKeyCommand_OpenAiCompatible_UsesEndpointUrl()
    {
        _sut.SelectedProviderType = ProviderType.OpenAICompatible;
        _sut.ApiKeyInputValue = "test-key";
        _sut.CustomEndpointUrlValue = "https://custom.example.com";

        _llmProviderServiceMock
            .Setup(s => s.ValidateApiKeyAsync(
                ProviderType.OpenAICompatible, "test-key", "https://custom.example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.TestApiKeyCommand.ExecuteAsync(null);

        Assert.True(_sut.IsTestSuccess);
    }

    [Fact]
    public async Task TestApiKeyCommand_Exception_CapturesError()
    {
        _sut.ApiKeyInputValue = "some-key";

        _llmProviderServiceMock
            .Setup(s => s.ValidateApiKeyAsync(It.IsAny<ProviderType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        await _sut.TestApiKeyCommand.ExecuteAsync(null);

        Assert.False(_sut.IsTestSuccess);
        Assert.Contains("Network error", _sut.TestResultMessage);
        Assert.False(_sut.IsTesting);
    }

    // ================================================================
    // SaveApiKeyCommand
    // ================================================================

    [Fact]
    public async Task SaveApiKeyCommand_NewKey_CallsProtectAndCreate()
    {
        _sut.ApiKeyInputValue = "sk-new-key";
        _sut.SelectedProviderType = ProviderType.Google;
        _sut.DisplayNameInputValue = "My Google Key";
        _sut.IsTestSuccess = true;

        _encryptionServiceMock
            .Setup(e => e.ProtectString("sk-new-key"))
            .Returns("encrypted:sk-new-key");

        _apiKeyRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<ApiKey>()))
            .ReturnsAsync((ApiKey k) => k);

        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        await _sut.SaveApiKeyCommand.ExecuteAsync(null);

        _encryptionServiceMock.Verify(e => e.ProtectString("sk-new-key"), Times.Once);
        _apiKeyRepoMock.Verify(r => r.CreateAsync(It.Is<ApiKey>(k =>
            k.ProviderType == ProviderType.Google &&
            k.Label == "My Google Key" &&
            k.EncryptedValue == "encrypted:sk-new-key" &&
            k.IsValid == true)), Times.Once);

        Assert.False(_sut.IsEditingKey);
        Assert.Equal("API key saved successfully.", _sut.StatusMessage);
    }

    [Fact]
    public async Task SaveApiKeyCommand_EmptyKey_DoesNotSave()
    {
        _sut.ApiKeyInputValue = string.Empty;

        await _sut.SaveApiKeyCommand.ExecuteAsync(null);

        _encryptionServiceMock.Verify(e => e.ProtectString(It.IsAny<string>()), Times.Never);
        _apiKeyRepoMock.Verify(r => r.CreateAsync(It.IsAny<ApiKey>()), Times.Never);
        Assert.Contains("empty", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveApiKeyCommand_ExistingKey_CallsUpdate()
    {
        var existingKey = new ApiKey
        {
            Id = "existing-id",
            ProviderType = ProviderType.OpenAI,
            EncryptedValue = "old-encrypted",
            Label = "Old Label",
        };

        _sut.EditingApiKey = existingKey;
        _sut.ApiKeyInputValue = "updated-key";
        _sut.DisplayNameInputValue = "Updated Label";
        _sut.IsTestSuccess = true;

        _encryptionServiceMock
            .Setup(e => e.ProtectString("updated-key"))
            .Returns("encrypted:updated-key");

        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        await _sut.SaveApiKeyCommand.ExecuteAsync(null);

        _apiKeyRepoMock.Verify(r => r.UpdateAsync(It.Is<ApiKey>(k =>
            k.Id == "existing-id" &&
            k.ProviderType == ProviderType.OpenAI &&
            k.Label == "Updated Label" &&
            k.EncryptedValue == "encrypted:updated-key" &&
            k.IsValid == true)), Times.Once);

        Assert.False(_sut.IsEditingKey);
    }

    [Fact]
    public async Task SaveApiKeyCommand_UpdateAsyncThrows_ShowsError()
    {
        _sut.EditingApiKey = new ApiKey
        {
            Id = "fail-id", ProviderType = ProviderType.OpenAI, EncryptedValue = "old",
        };
        _sut.ApiKeyInputValue = "new-key";

        _encryptionServiceMock
            .Setup(e => e.ProtectString("new-key"))
            .Returns("encrypted-new");

        _apiKeyRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<ApiKey>()))
            .ThrowsAsync(new InvalidOperationException("Update failed"));

        await _sut.SaveApiKeyCommand.ExecuteAsync(null);

        Assert.Contains("Update failed", _sut.StatusMessage);
    }

    [Fact]
    public async Task SaveApiKeyCommand_Exception_ShowsError()
    {
        _sut.ApiKeyInputValue = "some-key";

        _encryptionServiceMock
            .Setup(e => e.ProtectString(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Encryption failed"));

        await _sut.SaveApiKeyCommand.ExecuteAsync(null);

        Assert.Contains("Encryption failed", _sut.StatusMessage);
    }

    // ================================================================
    // DeleteApiKeyCommand
    // ================================================================

    [Fact]
    public async Task DeleteApiKeyCommand_NullItem_DoesNothing()
    {
        await _sut.DeleteApiKeyCommand.ExecuteAsync(null);

        _apiKeyRepoMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteApiKeyCommand_ValidItem_DeletesAndRefreshes()
    {
        _apiKeyRepoMock
            .Setup(r => r.DeleteAsync("key-to-delete"))
            .Returns(Task.CompletedTask);

        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        var item = new ApiKeyDisplayItem { Id = "key-to-delete" };

        await _sut.DeleteApiKeyCommand.ExecuteAsync(item);

        _apiKeyRepoMock.Verify(r => r.DeleteAsync("key-to-delete"), Times.Once);
        _apiKeyRepoMock.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
        Assert.Equal("API key deleted.", _sut.StatusMessage);
    }

    [Fact]
    public async Task DeleteApiKeyCommand_Exception_ShowsError()
    {
        _apiKeyRepoMock
            .Setup(r => r.DeleteAsync("failing-id"))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var item = new ApiKeyDisplayItem { Id = "failing-id" };

        await _sut.DeleteApiKeyCommand.ExecuteAsync(item);

        Assert.Contains("Failed", _sut.StatusMessage);
    }

    // ================================================================
    // CancelEditCommand
    // ================================================================

    [Fact]
    public void CancelEditCommand_ClearsForm()
    {
        _sut.IsEditingKey = true;
        _sut.ApiKeyInputValue = "some-value";
        _sut.DisplayNameInputValue = "Name";

        _sut.CancelEditCommand.Execute(null);

        Assert.False(_sut.IsEditingKey);
        Assert.Null(_sut.EditingApiKey);
        Assert.Empty(_sut.ApiKeyInputValue);
        Assert.Empty(_sut.DisplayNameInputValue);
    }

    // ================================================================
    // SelectedProviderType changes
    // ================================================================

    [Fact]
    public void SelectedProviderType_OpenAI_SetsIsOpenAiCompatibleFalse()
    {
        _sut.SelectedProviderType = ProviderType.OpenAI;
        Assert.False(_sut.IsOpenAiCompatibleSelected);
    }

    [Fact]
    public void SelectedProviderType_OpenAICompatible_SetsIsOpenAiCompatibleTrue()
    {
        _sut.SelectedProviderType = ProviderType.OpenAICompatible;
        Assert.True(_sut.IsOpenAiCompatibleSelected);
    }

    [Fact]
    public void SelectedProviderType_Change_ClearsTestResults()
    {
        _sut.IsTestSuccess = true;
        _sut.TestResultMessage = "Some result";

        _sut.SelectedProviderType = ProviderType.Anthropic;

        Assert.Empty(_sut.TestResultMessage);
        Assert.False(_sut.IsTestSuccess);
    }

    // ================================================================
    // AllProviderTypes list
    // ================================================================

    [Fact]
    public void AllProviderTypes_ContainsAllEight()
    {
        Assert.Equal(8, _sut.AllProviderTypes.Count);
        Assert.Contains(ProviderType.OpenAICompatible, _sut.AllProviderTypes);
    }

    // ================================================================
    // EditApiKeyCommand
    // ================================================================

    [Fact]
    public async Task EditApiKeyCommand_NullItem_DoesNothing()
    {
        await _sut.EditApiKeyCommand.ExecuteAsync(null);
        Assert.False(_sut.IsEditingKey);
    }

    [Fact]
    public async Task EditApiKeyCommand_GetByIdAsyncThrows_ShowsError()
    {
        _apiKeyRepoMock
            .Setup(r => r.GetByIdAsync("error-id"))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var item = new ApiKeyDisplayItem { Id = "error-id" };
        await _sut.EditApiKeyCommand.ExecuteAsync(item);

        Assert.Contains("Failed", _sut.StatusMessage);
        Assert.False(_sut.IsEditingKey);
    }

    [Fact]
    public async Task EditApiKeyCommand_ValidItem_SetsEditMode()
    {
        var existingKey = new ApiKey
        {
            Id = "key-1",
            ProviderType = ProviderType.Anthropic,
            EncryptedValue = "encrypted-value",
            Label = "My Key",
            CustomProviderName = null,
            CustomEndpointUrl = null,
            IsValid = true,
        };

        _apiKeyRepoMock
            .Setup(r => r.GetByIdAsync("key-1"))
            .ReturnsAsync(existingKey);

        _encryptionServiceMock
            .Setup(e => e.UnprotectString("encrypted-value"))
            .Returns("decrypted-key");

        var displayItem = new ApiKeyDisplayItem
        {
            Id = "key-1",
            EncryptedValue = "encrypted-value",
        };

        await _sut.EditApiKeyCommand.ExecuteAsync(displayItem);

        Assert.True(_sut.IsEditingKey);
        Assert.NotNull(_sut.EditingApiKey);
        Assert.Equal("key-1", _sut.EditingApiKey.Id);
        Assert.Equal(ProviderType.Anthropic, _sut.SelectedProviderType);
        Assert.Equal("My Key", _sut.DisplayNameInputValue);
        Assert.Equal("decrypted-key", _sut.ApiKeyInputValue); // Key decrypted and pre-filled
        Assert.True(_sut.IsTestSuccess);
        _encryptionServiceMock.Verify(e => e.UnprotectString("encrypted-value"), Times.Once);
    }

    [Fact]
    public async Task EditApiKeyCommand_NullEncryptedValue_LeavesInputEmpty()
    {
        var existingKey = new ApiKey
        {
            Id = "key-2",
            ProviderType = ProviderType.OpenAI,
            EncryptedValue = null!,
            IsValid = false,
        };

        _apiKeyRepoMock
            .Setup(r => r.GetByIdAsync("key-2"))
            .ReturnsAsync(existingKey);

        var displayItem = new ApiKeyDisplayItem
        {
            Id = "key-2",
            EncryptedValue = null!,
        };

        await _sut.EditApiKeyCommand.ExecuteAsync(displayItem);

        Assert.True(_sut.IsEditingKey);
        Assert.Empty(_sut.ApiKeyInputValue);
        _encryptionServiceMock.Verify(e => e.UnprotectString(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EditApiKeyCommand_DecryptionFailure_LeavesInputEmpty()
    {
        var existingKey = new ApiKey
        {
            Id = "key-3",
            ProviderType = ProviderType.Anthropic,
            EncryptedValue = "corrupted-value",
            IsValid = true,
        };

        _apiKeyRepoMock
            .Setup(r => r.GetByIdAsync("key-3"))
            .ReturnsAsync(existingKey);

        _encryptionServiceMock
            .Setup(e => e.UnprotectString("corrupted-value"))
            .Throws(new InvalidOperationException("DPAPI decryption failed"));

        var displayItem = new ApiKeyDisplayItem
        {
            Id = "key-3",
            EncryptedValue = "corrupted-value",
        };

        await _sut.EditApiKeyCommand.ExecuteAsync(displayItem);

        Assert.True(_sut.IsEditingKey);
        Assert.Empty(_sut.ApiKeyInputValue); // Falls back to empty on failure
        _encryptionServiceMock.Verify(e => e.UnprotectString("corrupted-value"), Times.Once);
    }

    [Fact]
    public async Task EditApiKeyCommand_NotFound_ShowsError()
    {
        _apiKeyRepoMock
            .Setup(r => r.GetByIdAsync("missing-id"))
            .ReturnsAsync((ApiKey?)null);

        var displayItem = new ApiKeyDisplayItem { Id = "missing-id" };
        await _sut.EditApiKeyCommand.ExecuteAsync(displayItem);

        Assert.False(_sut.IsEditingKey);
        Assert.Contains("not found", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // CopyKeyCommand
    // ================================================================

    [Fact]
    public void CopyKeyCommand_NullItem_DoesNothing()
    {
        _sut.CopyKeyCommand.Execute(null);
        _encryptionServiceMock.Verify(e => e.UnprotectString(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CopyKeyCommand_NullEncryptedValue_DoesNothing()
    {
        _sut.CopyKeyCommand.Execute(new ApiKeyDisplayItem { EncryptedValue = null! });
        _encryptionServiceMock.Verify(e => e.UnprotectString(It.IsAny<string>()), Times.Never);
        _clipboardServiceMock.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CopyKeyCommand_EmptyEncryptedValue_DoesNothing()
    {
        _sut.CopyKeyCommand.Execute(new ApiKeyDisplayItem { EncryptedValue = string.Empty });
        _encryptionServiceMock.Verify(e => e.UnprotectString(It.IsAny<string>()), Times.Never);
        _clipboardServiceMock.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CopyKeyCommand_ValidItem_DecryptsKeyAndCopiesToClipboard()
    {
        _encryptionServiceMock
            .Setup(e => e.UnprotectString("encrypted-value"))
            .Returns("decrypted-key");

        var item = new ApiKeyDisplayItem
        {
            EncryptedValue = "encrypted-value",
        };

        _sut.CopyKeyCommand.Execute(item);

        _encryptionServiceMock.Verify(e => e.UnprotectString("encrypted-value"), Times.Once);
        _clipboardServiceMock.Verify(c => c.SetText("decrypted-key"), Times.Once);
    }

    [Fact]
    public void CopyKeyCommand_Exception_DoesNotThrow()
    {
        _encryptionServiceMock
            .Setup(e => e.UnprotectString(It.IsAny<string>()))
            .Throws(new Exception("Decryption failed"));

        var item = new ApiKeyDisplayItem
        {
            EncryptedValue = "bad-data",
        };

        // Should not throw — the catch block handles it gracefully
        var exception = Record.Exception(() => _sut.CopyKeyCommand.Execute(item));
        Assert.Null(exception);

        _clipboardServiceMock.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
    }

    // ================================================================
    // InitializeAsync
    // ================================================================

    [Fact]
    public async Task InitializeAsync_GetAllAsyncThrows_SetsStatusMessage()
    {
        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        await _sut.InitializeCommand.ExecuteAsync(null);

        Assert.Contains("Failed", _sut.StatusMessage);
        Assert.Empty(_sut.ApiKeys);
    }

    [Fact]
    public async Task InitializeAsync_LoadsApiKeys()
    {
        var keys = new List<ApiKey>
        {
            new()
            {
                Id = "1", ProviderType = ProviderType.OpenAI,
                EncryptedValue = "enc1", Label = "Key 1", IsValid = true,
            },
            new()
            {
                Id = "2", ProviderType = ProviderType.Anthropic,
                EncryptedValue = "enc2", Label = "Key 2", IsValid = false,
            },
        };

        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(keys);

        await _sut.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(2, _sut.ApiKeys.Count);
        Assert.Equal("Key 1", _sut.ApiKeys[0].DisplayName);
        Assert.Equal("Key 2", _sut.ApiKeys[1].DisplayName);
        Assert.True(_sut.ApiKeys[0].IsValid);
        Assert.False(_sut.ApiKeys[1].IsValid);
    }

    // ================================================================
    // SelectedSettingsCategory changes
    // ================================================================

    [Fact]
    public void SelectedSettingsCategory_Change_ClearsStatusMessage()
    {
        _sut.StatusMessage = "Some status";

        _sut.SelectedSettingsCategory = SettingsCategory.Profiles;

        Assert.Empty(_sut.StatusMessage);
    }

    // ================================================================
    // AddModelConfigCommand
    // ================================================================

    [Fact]
    public async Task AddModelConfigCommand_InitializesFormWithDefaults()
    {
        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        await _sut.AddModelConfigCommand.ExecuteAsync(null);

        Assert.True(_sut.IsEditingModelConfig);
        Assert.NotNull(_sut.EditingModelConfig);
        Assert.Equal(1.0, _sut.EditingModelConfig.Temperature);
        Assert.Equal(4096, _sut.EditingModelConfig.MaxOutputTokens);
        Assert.Equal(128000, _sut.EditingModelConfig.MaxContextWindow);
        Assert.Equal("SlidingWindow", _sut.EditingModelConfig.ContextOverflowStrategy);
        Assert.Equal(ProviderType.OpenAI, _sut.SelectedModelConfigProvider);
        Assert.Empty(_sut.AvailableModels);
    }

    // ================================================================
    // SaveModelConfigCommand
    // ================================================================

    [Fact]
    public async Task SaveModelConfigCommand_NullConfig_ShowsError()
    {
        _sut.EditingModelConfig = null;

        await _sut.SaveModelConfigCommand.ExecuteAsync(null);

        Assert.Contains("no model configuration", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveModelConfigCommand_NewConfig_CallsCreateAsync()
    {
        // Must call AddModelConfigAsync first to set _isNewModelConfig = true
        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        await _sut.AddModelConfigCommand.ExecuteAsync(null);

        _sut.EditingModelConfig = new ModelConfiguration
        {
            DisplayName = "GPT-4o Test",
            ModelIdentifier = "gpt-4o",
            Temperature = 1.0,
            ContextOverflowStrategy = "SlidingWindow",
        };

        _modelConfigRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<ModelConfiguration>()))
            .ReturnsAsync((ModelConfiguration c) => c);

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        await _sut.SaveModelConfigCommand.ExecuteAsync(null);

        _modelConfigRepoMock.Verify(r => r.CreateAsync(It.Is<ModelConfiguration>(c =>
            c.DisplayName == "GPT-4o Test" &&
            c.ModelIdentifier == "gpt-4o" &&
            c.ContextOverflowStrategy == "SlidingWindow")), Times.Once);

        Assert.False(_sut.IsEditingModelConfig);
        Assert.Contains("saved", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveModelConfigCommand_MissingName_DoesNotSave()
    {
        _sut.EditingModelConfig = new ModelConfiguration
        {
            DisplayName = string.Empty,
            ModelIdentifier = "gpt-4o",
        };

        await _sut.SaveModelConfigCommand.ExecuteAsync(null);

        _modelConfigRepoMock.Verify(r => r.CreateAsync(It.IsAny<ModelConfiguration>()), Times.Never);
        Assert.Contains("display name", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveModelConfigCommand_MissingModelIdentifier_DoesNotSave()
    {
        _sut.EditingModelConfig = new ModelConfiguration
        {
            DisplayName = "My Config",
            ModelIdentifier = string.Empty,
        };

        await _sut.SaveModelConfigCommand.ExecuteAsync(null);

        _modelConfigRepoMock.Verify(r => r.CreateAsync(It.IsAny<ModelConfiguration>()), Times.Never);
        Assert.Contains("model identifier", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveModelConfigCommand_ClampsTemperature()
    {
        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        await _sut.AddModelConfigCommand.ExecuteAsync(null);

        _sut.EditingModelConfig = new ModelConfiguration
        {
            DisplayName = "Test",
            ModelIdentifier = "test-model",
            Temperature = 999.0, // Out of range
        };

        _modelConfigRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<ModelConfiguration>()))
            .ReturnsAsync((ModelConfiguration c) => c);

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        await _sut.SaveModelConfigCommand.ExecuteAsync(null);

        _modelConfigRepoMock.Verify(r => r.CreateAsync(It.Is<ModelConfiguration>(c =>
            Math.Abs(c.Temperature - 2.0) < 0.001)), Times.Once);
    }

    // ================================================================
    // DuplicateModelConfigCommand
    // ================================================================

    [Fact]
    public async Task DuplicateModelConfigCommand_NullItem_DoesNothing()
    {
        await _sut.DuplicateModelConfigCommand.ExecuteAsync(null);

        _modelConfigRepoMock.Verify(r => r.CreateAsync(It.IsAny<ModelConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateModelConfigCommand_AppendsCopySuffix()
    {
        var source = new ModelConfiguration
        {
            Id = "source-1",
            DisplayName = "GPT-4o",
            ProviderType = ProviderType.OpenAI,
            ModelIdentifier = "gpt-4o",
            Temperature = 0.7,
            MaxOutputTokens = 4096,
            MaxContextWindow = 128000,
            ThinkingEnabled = false,
            ContextOverflowStrategy = "SlidingWindow",
        };

        _modelConfigRepoMock
            .Setup(r => r.GetByIdAsync("source-1"))
            .ReturnsAsync(source);

        _modelConfigRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<ModelConfiguration>()))
            .ReturnsAsync((ModelConfiguration c) => c);

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        var displayItem = new ModelConfigurationDisplayItem { Id = "source-1" };

        await _sut.DuplicateModelConfigCommand.ExecuteAsync(displayItem);

        _modelConfigRepoMock.Verify(r => r.CreateAsync(It.Is<ModelConfiguration>(c =>
            c.DisplayName == "GPT-4o (Copy)" &&
            c.ProviderType == ProviderType.OpenAI &&
            c.ModelIdentifier == "gpt-4o" &&
            Math.Abs(c.Temperature - 0.7) < 0.001)), Times.Once);
    }

    // ================================================================
    // DeleteModelConfigCommand
    // ================================================================

    [Fact]
    public async Task DeleteModelConfigCommand_NullItem_DoesNothing()
    {
        await _sut.DeleteModelConfigCommand.ExecuteAsync(null);

        _modelConfigRepoMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteModelConfigCommand_ValidItem_Deletes()
    {
        _modelConfigRepoMock
            .Setup(r => r.DeleteAsync("config-to-delete"))
            .Returns(Task.CompletedTask);

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        var item = new ModelConfigurationDisplayItem { Id = "config-to-delete", DisplayName = "Test Config" };

        await _sut.DeleteModelConfigCommand.ExecuteAsync(item);

        _modelConfigRepoMock.Verify(r => r.DeleteAsync("config-to-delete"), Times.Once);
        Assert.Equal("Model configuration deleted.", _sut.StatusMessage);
    }

    [Fact]
    public async Task DeleteModelConfigCommand_ReferencedByPersona_ShowsWarning()
    {
        // Add a persona that references this model config
        _sut.Personas =
        [
            new PersonaDisplayItem
            {
                Id = "persona-1",
                DisplayName = "Test Persona",
                DefaultModelConfigId = "config-to-delete",
            },
        ];

        _modelConfigRepoMock
            .Setup(r => r.DeleteAsync("config-to-delete"))
            .Returns(Task.CompletedTask);

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        var item = new ModelConfigurationDisplayItem { Id = "config-to-delete", DisplayName = "Test Config" };

        await _sut.DeleteModelConfigCommand.ExecuteAsync(item);

        // Should have shown confirmation with persona names
        _confirmationServiceMock.Verify(c => c.Confirm(
            It.Is<string>(s => s.Contains("Test Persona")),
            It.IsAny<string>()), Times.Once);

        _modelConfigRepoMock.Verify(r => r.DeleteAsync("config-to-delete"), Times.Once);
    }

    // ================================================================
    // AddPersonaCommand
    // ================================================================

    [Fact]
    public void AddPersonaCommand_InitializesWithStandardChatMode()
    {
        _sut.AddPersonaCommand.Execute(null);

        Assert.True(_sut.IsEditingPersona);
        Assert.NotNull(_sut.EditingPersona);
        Assert.Equal("Standard", _sut.EditingPersona.DefaultChatMode);
    }

    // ================================================================
    // SavePersonaCommand
    // ================================================================

    [Fact]
    public async Task SavePersonaCommand_NullPersona_ShowsError()
    {
        _sut.EditingPersona = null;

        await _sut.SavePersonaCommand.ExecuteAsync(null);

        Assert.Contains("no persona", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SavePersonaCommand_NewPersona_CallsCreateAsync()
    {
        _sut.AddPersonaCommand.Execute(null);

        _sut.EditingPersona = new Persona
        {
            DisplayName = "Test Persona",
            SystemPrompt = "You are a test assistant.",
            DefaultChatMode = "Standard",
        };

        _personaRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<Persona>()))
            .ReturnsAsync((Persona p) => p);

        _personaRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Persona>());

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        await _sut.SavePersonaCommand.ExecuteAsync(null);

        _personaRepoMock.Verify(r => r.CreateAsync(It.Is<Persona>(p =>
            p.DisplayName == "Test Persona" &&
            p.SystemPrompt == "You are a test assistant." &&
            p.DefaultChatMode == "Standard")), Times.Once);

        Assert.False(_sut.IsEditingPersona);
        Assert.Contains("saved", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SavePersonaCommand_MissingName_DoesNotSave()
    {
        _sut.EditingPersona = new Persona
        {
            DisplayName = string.Empty,
        };

        await _sut.SavePersonaCommand.ExecuteAsync(null);

        _personaRepoMock.Verify(r => r.CreateAsync(It.IsAny<Persona>()), Times.Never);
        Assert.Contains("display name", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SavePersonaCommand_DefaultChatModeIsStandard()
    {
        _sut.AddPersonaCommand.Execute(null);

        _sut.EditingPersona = new Persona
        {
            DisplayName = "Default Mode Persona",
            SystemPrompt = "Test",
            DefaultChatMode = "Standard",
        };

        _personaRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<Persona>()))
            .ReturnsAsync((Persona p) => p);

        _personaRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Persona>());

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        await _sut.SavePersonaCommand.ExecuteAsync(null);

        _personaRepoMock.Verify(r => r.CreateAsync(It.Is<Persona>(p =>
            p.DefaultChatMode == "Standard")), Times.Once);
    }

    // ================================================================
    // DeletePersonaCommand
    // ================================================================

    [Fact]
    public async Task DeletePersonaCommand_NullItem_DoesNothing()
    {
        await _sut.DeletePersonaCommand.ExecuteAsync(null);

        _personaRepoMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeletePersonaCommand_ValidItem_Deletes()
    {
        _personaRepoMock
            .Setup(r => r.DeleteAsync("persona-to-delete"))
            .Returns(Task.CompletedTask);

        _personaRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Persona>());

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        var item = new PersonaDisplayItem { Id = "persona-to-delete", DisplayName = "Test Persona" };

        await _sut.DeletePersonaCommand.ExecuteAsync(item);

        _personaRepoMock.Verify(r => r.DeleteAsync("persona-to-delete"), Times.Once);
        Assert.Equal("Persona deleted.", _sut.StatusMessage);
    }

    // ================================================================
    // FetchModelsCommand
    // ================================================================

    [Fact]
    public async Task FetchModelsCommand_NoApiKeySelected_ShowsMessage()
    {
        _sut.SelectedModelConfigApiKey = null;

        await _sut.FetchModelsCommand.ExecuteAsync(null);

        Assert.Contains("Select an API key", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchModelsCommand_WithApiKey_PopulatesAvailableModels()
    {
        var apiKey = new ApiKey
        {
            Id = "key-1",
            ProviderType = ProviderType.OpenAI,
            EncryptedValue = "encrypted",
        };

        _sut.SelectedModelConfigApiKey = new ApiKeyDisplayItem
        {
            Id = "key-1",
            ProviderType = ProviderType.OpenAI,
        };

        _apiKeyRepoMock
            .Setup(r => r.GetByIdAsync("key-1"))
            .ReturnsAsync(apiKey);

        _llmProviderServiceMock
            .Setup(s => s.ListModelsAsync(It.IsAny<ModelConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModelInfo>
            {
                new("gpt-4o", "GPT-4o", 128000),
                new("gpt-4o-mini", "GPT-4o Mini", 128000),
            });

        await _sut.FetchModelsCommand.ExecuteAsync(null);

        Assert.Equal(2, _sut.AvailableModels.Count);
        Assert.Contains("gpt-4o", _sut.AvailableModels);
        Assert.Contains("gpt-4o-mini", _sut.AvailableModels);
    }

    // ================================================================
    // ContextOverflowStrategyOptions
    // ================================================================

    [Fact]
    public void ContextOverflowStrategyOptions_ContainsSlidingWindow()
    {
        Assert.Contains("SlidingWindow", _sut.ContextOverflowStrategyOptions);
        Assert.Contains("HardStop", _sut.ContextOverflowStrategyOptions);
        Assert.Contains("AutoSummarize", _sut.ContextOverflowStrategyOptions);
    }

    // ================================================================
    // ModelConfigurationDisplayItem
    // ================================================================

    [Fact]
    public void ModelConfigurationDisplayItem_Summary_FormatsCorrectly()
    {
        var item = new ModelConfigurationDisplayItem
        {
            DisplayName = "GPT-4o Config",
            ProviderLabel = "OpenAI",
            ModelIdentifier = "gpt-4o",
        };

        Assert.Contains("GPT-4o Config", item.Summary);
        Assert.Contains("OpenAI", item.Summary);
        Assert.Contains("gpt-4o", item.Summary);
    }

    [Fact]
    public void ModelConfigurationDisplayItem_Summary_EmptyParts_DoesNotTrailingDash()
    {
        var item = new ModelConfigurationDisplayItem
        {
            DisplayName = "My Config",
            ProviderLabel = string.Empty,
            ModelIdentifier = string.Empty,
        };

        Assert.Equal("My Config", item.Summary);
    }

    // ================================================================
    // Temperature clamping - lower bound
    // ================================================================

    [Fact]
    public async Task SaveModelConfigCommand_ClampsTemperatureLowerBound()
    {
        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        await _sut.AddModelConfigCommand.ExecuteAsync(null);

        _sut.EditingModelConfig!.DisplayName = "Test Config";
        _sut.EditingModelConfig!.ModelIdentifier = "test-model";
        _sut.EditingModelConfig!.Temperature = -5.0; // Below minimum

        _modelConfigRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<ModelConfiguration>()))
            .ReturnsAsync((ModelConfiguration c) => c);

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        await _sut.SaveModelConfigCommand.ExecuteAsync(null);

        _modelConfigRepoMock.Verify(r => r.CreateAsync(It.Is<ModelConfiguration>(c =>
            Math.Abs(c.Temperature - 0.0) < 0.001)), Times.Once);
    }

    // ================================================================
    // DeleteModelConfigCommand - confirmation declined
    // ================================================================

    [Fact]
    public async Task DeleteModelConfigCommand_ConfirmationDeclined_DoesNotDelete()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var item = new ModelConfigurationDisplayItem { Id = "config-1", DisplayName = "Test" };

        await _sut.DeleteModelConfigCommand.ExecuteAsync(item);

        _modelConfigRepoMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    // ================================================================
    // DeleteModelConfigCommand - InvalidOperationException caught
    // ================================================================

    [Fact]
    public async Task DeleteModelConfigCommand_InvalidOperationException_ShowsMessage()
    {
        _modelConfigRepoMock
            .Setup(r => r.DeleteAsync("config-ex"))
            .ThrowsAsync(new InvalidOperationException("Referenced by personas"));

        var item = new ModelConfigurationDisplayItem { Id = "config-ex", DisplayName = "Test" };

        await _sut.DeleteModelConfigCommand.ExecuteAsync(item);

        Assert.Contains("Referenced by personas", _sut.StatusMessage);
    }

    // ================================================================
    // DeletePersonaCommand - confirmation declined
    // ================================================================

    [Fact]
    public async Task DeletePersonaCommand_ConfirmationDeclined_DoesNotDelete()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var item = new PersonaDisplayItem { Id = "persona-1", DisplayName = "Test" };

        await _sut.DeletePersonaCommand.ExecuteAsync(item);

        _personaRepoMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }
}
