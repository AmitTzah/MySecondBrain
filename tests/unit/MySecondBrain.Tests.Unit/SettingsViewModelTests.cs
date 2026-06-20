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
        Assert.Empty(_sut.ApiKeyInputValue); // Key not pre-filled
        Assert.True(_sut.IsTestSuccess);
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
        Assert.Equal("API key copied to clipboard.", _sut.StatusMessage);
    }

    [Fact]
    public void CopyKeyCommand_Exception_ShowsErrorMessage()
    {
        _encryptionServiceMock
            .Setup(e => e.UnprotectString(It.IsAny<string>()))
            .Throws(new Exception("Decryption failed"));

        var item = new ApiKeyDisplayItem
        {
            EncryptedValue = "bad-data",
        };

        _sut.CopyKeyCommand.Execute(item);

        Assert.Contains("Failed", _sut.StatusMessage);
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
}
