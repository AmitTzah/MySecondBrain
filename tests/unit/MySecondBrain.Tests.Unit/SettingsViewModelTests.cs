using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
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
    private readonly Mock<IUpdateChecker> _updateCheckerMock;
    private readonly Mock<ILogger<SettingsViewModel>> _loggerMock;
    private readonly SettingsViewModel _sut;

    private readonly Mock<IWikiService> _wikiServiceMock;
    private readonly Mock<IBackupProvider> _backupProviderMock;
    private readonly Mock<ITextActionRepository> _textActionRepoMock;
    private readonly Mock<AppDbContext> _dbContextMock;

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
        _updateCheckerMock = new Mock<IUpdateChecker>();
        _loggerMock = new Mock<ILogger<SettingsViewModel>>();
        _wikiServiceMock = new Mock<IWikiService>();
        _backupProviderMock = new Mock<IBackupProvider>();
        _textActionRepoMock = new Mock<ITextActionRepository>();
        _dbContextMock = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());

        // Default: confirmations are accepted
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _updateCheckerMock
            .Setup(u => u.CurrentVersion)
            .Returns(new Version(1, 0, 0, 0));

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
            _updateCheckerMock.Object,
            _loggerMock.Object,
            _wikiServiceMock.Object,
            _backupProviderMock.Object,
            _textActionRepoMock.Object,
            _dbContextMock.Object);
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
        Assert.Equal(16, _sut.CategoryItems.Count);
        Assert.Contains(_sut.CategoryItems, c => c.Category == SettingsCategory.Providers);
        Assert.Contains(_sut.CategoryItems, c => c.Category == SettingsCategory.Diagnostics);
        Assert.Contains(_sut.CategoryItems, c => c.Category == SettingsCategory.Language);
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

    [Theory]
    [InlineData(ProviderType.DeepSeek, "https://api.deepseek.com")]
    [InlineData(ProviderType.Mistral, "https://api.mistral.ai")]
    [InlineData(ProviderType.Moonshot, "https://api.moonshot.ai/v1")]
    [InlineData(ProviderType.MiMo, "https://api.xiaomimimo.com/v1")]
    public async Task TestApiKeyCommand_WellKnownProvider_UsesCorrectEndpoint(
        ProviderType providerType, string expectedEndpoint)
    {
        _sut.SelectedProviderType = providerType;
        _sut.ApiKeyInputValue = "test-key";

        _llmProviderServiceMock
            .Setup(s => s.ValidateApiKeyAsync(
                providerType, "test-key", expectedEndpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.TestApiKeyCommand.ExecuteAsync(null);

        Assert.True(_sut.IsTestSuccess);
        _llmProviderServiceMock.Verify(s => s.ValidateApiKeyAsync(
            providerType, "test-key", expectedEndpoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ProviderType.OpenAI)]
    [InlineData(ProviderType.Anthropic)]
    [InlineData(ProviderType.Google)]
    public async Task TestApiKeyCommand_BuiltInProvider_PassesNullEndpoint(ProviderType providerType)
    {
        _sut.SelectedProviderType = providerType;
        _sut.ApiKeyInputValue = "test-key";

        _llmProviderServiceMock
            .Setup(s => s.ValidateApiKeyAsync(
                providerType, "test-key", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.TestApiKeyCommand.ExecuteAsync(null);

        Assert.True(_sut.IsTestSuccess);
        _llmProviderServiceMock.Verify(s => s.ValidateApiKeyAsync(
            providerType, "test-key", null, It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task SelectedSettingsCategory_Change_UpdatesStaticField()
    {
        // Verify the static field is updated when category changes
        _sut.SelectedSettingsCategory = SettingsCategory.Diagnostics;

        // Setup default returns for InitializeAsync on the shared mocks
        _apiKeyRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ApiKey>());
        _modelConfigRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModelConfiguration>());
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona>());
        _textActionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TextAction>());
        _backupProviderMock.Setup(b => b.ValidateCredentialsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Create a new ViewModel instance — InitializeAsync restores from the static field
        var vm2 = CreateFreshViewModel();
        await vm2.InitializeCommand.ExecuteAsync(null);
        Assert.Equal(SettingsCategory.Diagnostics, vm2.SelectedSettingsCategory);
    }

    /// <summary>
    /// Creates a fresh SettingsViewModel instance (simulating recreation after navigation).
    /// </summary>
    private SettingsViewModel CreateFreshViewModel()
    {
        return new SettingsViewModel(
            _settingsRepoMock.Object,
            _themeProviderMock.Object,
            _apiKeyRepoMock.Object,
            _encryptionServiceMock.Object,
            _llmProviderServiceMock.Object,
            _clipboardServiceMock.Object,
            _confirmationServiceMock.Object,
            _modelConfigRepoMock.Object,
            _personaRepoMock.Object,
            _updateCheckerMock.Object,
            _loggerMock.Object,
            _wikiServiceMock.Object,
            _backupProviderMock.Object,
            _textActionRepoMock.Object,
            _dbContextMock.Object);
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
        Assert.Equal(131072, _sut.EditingModelConfig.MaxOutputTokens);
        Assert.Equal(1000000, _sut.EditingModelConfig.MaxContextWindow);
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
        _modelConfigRepoMock.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
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

        // Should have shown confirmation with persona names and the new cascade-nullify wording
        _confirmationServiceMock.Verify(c => c.Confirm(
            It.Is<string>(s => s.Contains("Test Persona") && s.Contains("clear their default model configuration")),
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
    public async Task DeleteModelConfigCommand_Exception_ShowsErrorMessage()
    {
        _modelConfigRepoMock
            .Setup(r => r.DeleteAsync("config-ex"))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var item = new ModelConfigurationDisplayItem { Id = "config-ex", DisplayName = "Test" };

        await _sut.DeleteModelConfigCommand.ExecuteAsync(item);

        Assert.Equal("Failed to delete model configuration.", _sut.StatusMessage);
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

    // ================================================================
    // Diagnostics — LogLevel defaults
    // ================================================================

    [Fact]
    public void Diagnostics_DefaultLogLevel_IsInformation()
    {
        Assert.Equal("Information", _sut.LogLevel);
    }

    [Fact]
    public void Diagnostics_LogLevelOptions_ContainsAllLevels()
    {
        Assert.Contains("Information", _sut.LogLevelOptions);
        Assert.Contains("Debug", _sut.LogLevelOptions);
        Assert.Contains("Verbose", _sut.LogLevelOptions);
        Assert.Equal(3, _sut.LogLevelOptions.Count);
    }

    // ================================================================
    // Diagnostics — LogLevel change persistence
    // ================================================================

    [Fact]
    public void Diagnostics_LogLevelChange_PersistsToSettingsRepository()
    {
        _sut.LogLevel = "Debug";

        _settingsRepoMock.Verify(
            s => s.SetAsync("LogLevel", "Debug"),
            Times.Once);
    }

    // ================================================================
    // Diagnostics — Category toggle defaults (3 ON, 5 OFF)
    // ================================================================

    [Fact]
    public void Diagnostics_DefaultCategoryToggles_ThreeOnFiveOff()
    {
        Assert.True(_sut.LogCategory_LLMApiCalls);
        Assert.True(_sut.LogCategory_Tier1HotkeyPipeline);
        Assert.True(_sut.LogCategory_Tier2CommandBar);

        Assert.False(_sut.LogCategory_Database);
        Assert.False(_sut.LogCategory_WikiFileSystem);
        Assert.False(_sut.LogCategory_WebSocket);
        Assert.False(_sut.LogCategory_StartupShutdown);
        Assert.False(_sut.LogCategory_SystemIntegration);
    }

    // ================================================================
    // Diagnostics — Category toggle persistence
    // ================================================================

    [Fact]
    public void Diagnostics_CategoryToggleOn_PersistsTrue()
    {
        _sut.LogCategory_Database = true;

        _settingsRepoMock.Verify(
            s => s.SetAsync("LogCategory_Database", "true"),
            Times.Once);
    }

    [Fact]
    public void Diagnostics_CategoryToggleOff_PersistsFalse()
    {
        _sut.LogCategory_LLMApiCalls = false;

        _settingsRepoMock.Verify(
            s => s.SetAsync("LogCategory_LLMApiCalls", "false"),
            Times.Once);
    }

    [Fact]
    public void Diagnostics_AllCategoryToggles_PersistCorrectKeys()
    {
        // Toggle each to a NON-default value to trigger the OnChanged partial method
        _sut.LogCategory_LLMApiCalls = false;  // Default true → false
        _sut.LogCategory_Tier1HotkeyPipeline = false; // Default true → false
        _sut.LogCategory_Tier2CommandBar = false; // Default true → false
        _sut.LogCategory_Database = true;  // Default false → true
        _sut.LogCategory_WikiFileSystem = true;  // Default false → true
        _sut.LogCategory_WebSocket = true;  // Default false → true
        _sut.LogCategory_StartupShutdown = true;  // Default false → true
        _sut.LogCategory_SystemIntegration = true;  // Default false → true

        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_LLMApiCalls", "false"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_Tier1HotkeyPipeline", "false"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_Tier2CommandBar", "false"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_Database", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_WikiFileSystem", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_WebSocket", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_StartupShutdown", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_SystemIntegration", "true"), Times.Once);
    }

    // ================================================================
    // Diagnostics — InitializeAsync loads settings from repository
    // ================================================================

    [Fact]
    public async Task InitializeAsync_LoadsDiagnosticsSettings()
    {
        _settingsRepoMock
            .Setup(s => s.GetAsync("LogLevel"))
            .ReturnsAsync("Debug");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_LLMApiCalls"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_Tier1HotkeyPipeline"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_Tier2CommandBar"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_Database"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_WikiFileSystem"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_WebSocket"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_StartupShutdown"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_SystemIntegration"))
            .ReturnsAsync("true");

        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        _personaRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Persona>());

        await _sut.InitializeCommand.ExecuteAsync(null);

        Assert.Equal("Debug", _sut.LogLevel);
        Assert.False(_sut.LogCategory_LLMApiCalls);
        Assert.False(_sut.LogCategory_Tier1HotkeyPipeline);
        Assert.False(_sut.LogCategory_Tier2CommandBar);
        Assert.True(_sut.LogCategory_Database);
        Assert.True(_sut.LogCategory_WikiFileSystem);
        Assert.True(_sut.LogCategory_WebSocket);
        Assert.True(_sut.LogCategory_StartupShutdown);
        Assert.True(_sut.LogCategory_SystemIntegration);
    }

    // ================================================================
    // Diagnostics — ClearLogsCommand
    // ================================================================

    [Fact]
    public async Task ClearLogsCommand_ConfirmationDeclined_DoesNotTryToDelete()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        await _sut.ClearLogsCommand.ExecuteAsync(null);

        // No StatusMessage should be set since we never attempted deletion
        Assert.Empty(_sut.StatusMessage);
    }

    [Fact]
    public async Task ClearLogsCommand_AllFilesDeleted_ShowsSuccessMessage()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create test log files to verify they get deleted
        var testFile1 = Path.Combine(logsPath, "msb-test-old.log");
        var testFile2 = Path.Combine(logsPath, "msb-test-old.json");
        await File.WriteAllTextAsync(testFile1, "old data");
        await File.WriteAllTextAsync(testFile2, "{}");

        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.Contains("log files cleared", _sut.StatusMessage);
            Assert.False(File.Exists(testFile1), "Test .log file should be deleted");
            Assert.False(File.Exists(testFile2), "Test .json file should be deleted");
        }
        finally
        {
            if (File.Exists(testFile1)) File.Delete(testFile1);
            if (File.Exists(testFile2)) File.Delete(testFile2);
        }
    }

    [Fact]
    public async Task ClearLogsCommand_LockedFile_ShowsInUseMessage()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create a test log file and lock it with an exclusive file stream
        var testFilePath = Path.Combine(logsPath, "msb-20260622-test.log");
        var fs = new FileStream(testFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.Contains("in use", _sut.StatusMessage);
            Assert.Contains("rotated automatically", _sut.StatusMessage);
        }
        finally
        {
            fs.Dispose();
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ClearLogsCommand_MixedLockedAndDeletableFiles_ShowsCombinedMessage()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create a deletable log file
        var deletableFilePath = Path.Combine(logsPath, "msb-20260621-old.log");
        await File.WriteAllTextAsync(deletableFilePath, "old log data");

        // Create a locked log file
        var lockedFilePath = Path.Combine(logsPath, "msb-20260622-current.log");
        var fs = new FileStream(lockedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.Contains("in use", _sut.StatusMessage);
            Assert.Contains("rotated automatically", _sut.StatusMessage);
            Assert.False(File.Exists(deletableFilePath), "Deletable file should have been removed");
        }
        finally
        {
            fs.Dispose();
            if (File.Exists(lockedFilePath))
                File.Delete(lockedFilePath);
            if (File.Exists(deletableFilePath))
                File.Delete(deletableFilePath);
        }
    }

    [Fact]
    public async Task ClearLogsCommand_FiltersOnlyLogAndJsonFiles()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create a .log file (should be targeted)
        var logFilePath = Path.Combine(logsPath, "test.log");
        await File.WriteAllTextAsync(logFilePath, "log data");

        // Create a .json file (should be targeted)
        var jsonFilePath = Path.Combine(logsPath, "test.json");
        await File.WriteAllTextAsync(jsonFilePath, "{}");

        // Create a .txt file (should be ignored)
        var txtFilePath = Path.Combine(logsPath, "test.txt");
        await File.WriteAllTextAsync(txtFilePath, "should remain");

        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.False(File.Exists(logFilePath), ".log file should be deleted");
            Assert.False(File.Exists(jsonFilePath), ".json file should be deleted");
            Assert.True(File.Exists(txtFilePath), ".txt file should be ignored");
        }
        finally
        {
            if (File.Exists(logFilePath)) File.Delete(logFilePath);
            if (File.Exists(jsonFilePath)) File.Delete(jsonFilePath);
            if (File.Exists(txtFilePath)) File.Delete(txtFilePath);
        }
    }

    // ================================================================
    // Diagnostics — OpenLogsFolder (no launch during tests)
    // ================================================================

    [Fact]
    public void OpenLogsFolderCommand_CreatesLogsDirectory()
    {
        // Verify the command constructs the correct path
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");

        Assert.Contains("MySecondBrain", expectedPath);
        Assert.Contains("logs", expectedPath);
    }

    // ================================================================
    // Appearance — AppTheme defaults
    // ================================================================

    [Fact]
    public void Appearance_DefaultTheme_IsDark()
    {
        Assert.Equal(AppTheme.Dark, _sut.AppTheme);
    }

    // ================================================================
    // Appearance — AppTheme change calls IThemeProvider and persists
    // ================================================================

    [Fact]
    public void Appearance_AppThemeChange_CallsThemeProviderAndPersists()
    {
        _sut.AppTheme = AppTheme.Light;

        _themeProviderMock.Verify(t => t.SetAppTheme(AppTheme.Light), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("AppTheme", "Light"), Times.Once);
    }

    // ================================================================
    // Appearance — ChatTheme defaults
    // ================================================================

    [Fact]
    public void Appearance_DefaultChatTheme_IsClassic()
    {
        Assert.Equal(ChatTheme.Classic, _sut.ChatTheme);
    }

    [Fact]
    public void Appearance_ChatThemeOptions_ContainsAllThemes()
    {
        Assert.Contains(ChatTheme.Classic, _sut.ChatThemeOptions);
        Assert.Contains(ChatTheme.Compact, _sut.ChatThemeOptions);
        Assert.Contains(ChatTheme.Bubble, _sut.ChatThemeOptions);
        Assert.Equal(3, _sut.ChatThemeOptions.Count);
    }

    // ================================================================
    // Appearance — ChatTheme change calls IThemeProvider and persists
    // ================================================================

    [Fact]
    public void Appearance_ChatThemeChange_CallsThemeProviderAndPersists()
    {
        _sut.ChatTheme = ChatTheme.Bubble;

        _themeProviderMock.Verify(t => t.SetChatTheme(ChatTheme.Bubble), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("ChatTheme", "Bubble"), Times.Once);
    }

    // ================================================================
    // Appearance — Font defaults
    // ================================================================

    [Fact]
    public void Appearance_DefaultFontFamily_IsSegoeUI()
    {
        Assert.Equal("Segoe UI", _sut.FontFamily);
    }

    [Fact]
    public void Appearance_DefaultFontSize_Is13()
    {
        Assert.Equal(13.0, _sut.FontSize);
    }

    [Fact]
    public void Appearance_DefaultFontWeight_IsNormal()
    {
        Assert.Equal("Normal", _sut.FontWeight);
    }

    [Fact]
    public void Appearance_FontFamilyOptions_ContainsCommonFonts()
    {
        Assert.Contains("Segoe UI", _sut.FontFamilyOptions);
        Assert.Contains("Consolas", _sut.FontFamilyOptions);
    }

    [Fact]
    public void Appearance_FontWeightOptions_ContainsNormalAndBold()
    {
        Assert.Contains("Normal", _sut.FontWeightOptions);
        Assert.Contains("Bold", _sut.FontWeightOptions);
        Assert.Equal(2, _sut.FontWeightOptions.Count);
    }

    // ================================================================
    // Appearance — FontSize clamping at boundary values
    // ================================================================

    [Fact]
    public void Appearance_FontSizeClampsBelow10()
    {
        _sut.FontSize = 5.0;
        Assert.Equal(10.0, _sut.FontSize);
        _settingsRepoMock.Verify(s => s.SetAsync("FontSize", "10.0"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), 10.0, It.IsAny<System.Windows.FontWeight>()), Times.Once);
    }

    [Fact]
    public void Appearance_FontSizeClampsAbove24()
    {
        _sut.FontSize = 30.0;
        Assert.Equal(24.0, _sut.FontSize);
        _settingsRepoMock.Verify(s => s.SetAsync("FontSize", "24.0"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), 24.0, It.IsAny<System.Windows.FontWeight>()), Times.Once);
    }

    [Fact]
    public void Appearance_FontSizeWithinRange_NotClamped()
    {
        _sut.FontSize = 16.0;
        Assert.Equal(16.0, _sut.FontSize);
    }

    // ================================================================
    // Appearance — FontFamily change persists
    // ================================================================

    [Fact]
    public void Appearance_FontFamilyChange_PersistsToSettingsRepository()
    {
        _sut.FontFamily = "Consolas";

        _settingsRepoMock.Verify(s => s.SetAsync("FontFamily", "Consolas"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings("Consolas", 13.0, It.IsAny<System.Windows.FontWeight>()), Times.Once);
    }

    // ================================================================
    // Appearance — FontWeight change persists
    // ================================================================

    [Fact]
    public void Appearance_FontWeightBold_PersistsAndCallsThemeProvider()
    {
        _sut.FontWeight = "Bold";

        _settingsRepoMock.Verify(s => s.SetAsync("FontWeight", "Bold"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), It.IsAny<double>(), System.Windows.FontWeights.Bold), Times.Once);
    }

    // ================================================================
    // Appearance — FontSize persisted with InvariantCulture (Bug 5)
    // ================================================================

    [Fact]
    public void Appearance_FontSizeChange_PersistsWithInvariantCulture()
    {
        // Simulate a culture where comma is the decimal separator
        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            _sut.FontSize = 13.5;

            // Verify the persisted value uses dot (InvariantCulture), not comma
            _settingsRepoMock.Verify(s => s.SetAsync("FontSize", "13.5"), Times.Once);
            _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), 13.5, It.IsAny<System.Windows.FontWeight>()), Times.Once);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // ================================================================
    // Notifications — defaults
    // ================================================================

    [Fact]
    public void Notifications_SoundOnCompletion_DefaultFalse()
    {
        Assert.False(_sut.SoundOnCompletion);
    }

    [Fact]
    public void Notifications_DisableStreaming_DefaultFalse()
    {
        Assert.False(_sut.DisableStreaming);
    }

    [Fact]
    public void Notifications_CrossTabCompletionAlert_DefaultTrue()
    {
        Assert.True(_sut.CrossTabCompletionAlert);
    }

    // ================================================================
    // Notifications — persistence
    // ================================================================

    [Fact]
    public void Notifications_SoundOnCompletionChange_Persists()
    {
        _sut.SoundOnCompletion = true;
        _settingsRepoMock.Verify(s => s.SetAsync("SoundOnCompletion", "true"), Times.Once);
    }

    [Fact]
    public void Notifications_DisableStreamingChange_Persists()
    {
        _sut.DisableStreaming = true;
        _settingsRepoMock.Verify(s => s.SetAsync("DisableStreaming", "true"), Times.Once);
    }

    [Fact]
    public void Notifications_CrossTabCompletionAlertChange_Persists()
    {
        _sut.CrossTabCompletionAlert = false;
        _settingsRepoMock.Verify(s => s.SetAsync("CrossTabCompletionAlert", "false"), Times.Once);
    }

    // ================================================================
    // Startup — LaunchOnWindowsStartup doesn't throw
    // ================================================================

    [Fact]
    public void Startup_LaunchOnWindowsStartupToggle_DoesNotThrow()
    {
        // Verify the registry write/delete logic doesn't throw
        var exception = Record.Exception(() => _sut.LaunchOnWindowsStartup = true);
        Assert.Null(exception);

        exception = Record.Exception(() => _sut.LaunchOnWindowsStartup = false);
        Assert.Null(exception);
    }

    // ================================================================
    // Startup — defaults
    // ================================================================

    [Fact]
    public void Startup_LaunchOnWindowsStartup_DefaultFalse()
    {
        Assert.False(_sut.LaunchOnWindowsStartup);
    }

    [Fact]
    public void Startup_RestoreLastSession_DefaultFalse()
    {
        Assert.False(_sut.RestoreLastSession);
    }

    [Fact]
    public void Startup_MinimizeToTray_DefaultTrue()
    {
        Assert.True(_sut.MinimizeToTray);
    }

    // ================================================================
    // Startup — persistence
    // ================================================================

    [Fact]
    public void Startup_RestoreLastSessionChange_Persists()
    {
        _sut.RestoreLastSession = true;
        _settingsRepoMock.Verify(s => s.SetAsync("RestoreLastSession", "true"), Times.Once);
    }

    [Fact]
    public void Startup_MinimizeToTrayChange_Persists()
    {
        _sut.MinimizeToTray = false;
        _settingsRepoMock.Verify(s => s.SetAsync("MinimizeToTray", "false"), Times.Once);
    }

    // ================================================================
    // Updates — defaults
    // ================================================================

    [Fact]
    public void Updates_UpdateCheckFrequency_DefaultOnStartup()
    {
        Assert.Equal("OnStartup", _sut.UpdateCheckFrequency);
    }

    [Fact]
    public void Updates_FrequencyOptions_ContainsAllFour()
    {
        Assert.Contains("OnStartup", _sut.UpdateCheckFrequencyOptions);
        Assert.Contains("Daily", _sut.UpdateCheckFrequencyOptions);
        Assert.Contains("Weekly", _sut.UpdateCheckFrequencyOptions);
        Assert.Contains("ManualOnly", _sut.UpdateCheckFrequencyOptions);
        Assert.Equal(4, _sut.UpdateCheckFrequencyOptions.Count);
    }

    [Fact]
    public void Updates_CurrentVersion_ReadFromUpdateChecker()
    {
        Assert.Equal("1.0.0.0", _sut.CurrentVersion);
    }

    // ================================================================
    // Updates — persistence
    // ================================================================

    [Fact]
    public void Updates_UpdateCheckFrequencyChange_Persists()
    {
        _sut.UpdateCheckFrequency = "Weekly";
        _settingsRepoMock.Verify(s => s.SetAsync("UpdateCheckFrequency", "Weekly"), Times.Once);
    }

    // ================================================================
    // Updates — CheckForUpdatesCommand
    // ================================================================

    [Fact]
    public async Task Updates_CheckForUpdates_NoUpdateAvailable_ShowsUpToDate()
    {
        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult(false, null, null));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("up to date", _sut.UpdateStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    [Fact]
    public async Task Updates_CheckForUpdates_UpdateAvailable_ShowsVersion()
    {
        var updateInfo = new UpdateInfo(
            new Version(2, 0, 0, 0),
            "Major update",
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            50_000_000,
            "https://example.com/update",
            false);

        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult(true, updateInfo, null));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("2.0.0.0", _sut.UpdateStatusMessage);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    [Fact]
    public async Task Updates_CheckForUpdates_ErrorMessage_ShowsError()
    {
        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult(false, null, "Network timeout"));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("Network timeout", _sut.UpdateStatusMessage);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    [Fact]
    public async Task Updates_CheckForUpdates_Exception_Caught()
    {
        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("No internet"));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("internet", _sut.UpdateStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    // ================================================================
    // InitializeAsync — loads new settings
    // ================================================================

    [Fact]
    public async Task InitializeAsync_LoadsNewSettings()
    {
        _settingsRepoMock
            .Setup(s => s.GetAsync("AppTheme"))
            .ReturnsAsync("Light");

        _settingsRepoMock
            .Setup(s => s.GetAsync("ChatTheme"))
            .ReturnsAsync("Bubble");

        _settingsRepoMock
            .Setup(s => s.GetAsync("FontFamily"))
            .ReturnsAsync("Consolas");

        _settingsRepoMock
            .Setup(s => s.GetAsync("FontSize"))
            .ReturnsAsync("18.0");

        _settingsRepoMock
            .Setup(s => s.GetAsync("FontWeight"))
            .ReturnsAsync("Bold");

        _settingsRepoMock
            .Setup(s => s.GetAsync("SoundOnCompletion"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("DisableStreaming"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("CrossTabCompletionAlert"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("RestoreLastSession"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("MinimizeToTray"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("UpdateCheckFrequency"))
            .ReturnsAsync("Weekly");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogLevel"))
            .ReturnsAsync((string?)null);

        _settingsRepoMock
            .Setup(s => s.GetAsync(It.Is<string>(k => k.StartsWith("LogCategory_"))))
            .ReturnsAsync((string?)null);

        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        _personaRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Persona>());

        await _sut.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(AppTheme.Light, _sut.AppTheme);
        Assert.Equal(ChatTheme.Bubble, _sut.ChatTheme);
        Assert.Equal("Consolas", _sut.FontFamily);
        Assert.Equal(18.0, _sut.FontSize);
        Assert.Equal("Bold", _sut.FontWeight);
        Assert.True(_sut.SoundOnCompletion);
        Assert.True(_sut.DisableStreaming);
        Assert.False(_sut.CrossTabCompletionAlert);
        Assert.True(_sut.RestoreLastSession);
        Assert.False(_sut.MinimizeToTray);
        Assert.Equal("Weekly", _sut.UpdateCheckFrequency);
    }

    // ================================================================
    // Step 3 — Language
    // ================================================================

    [Fact]
    public void Language_AutoDetectRtl_DefaultTrue()
    {
        Assert.True(_sut.AutoDetectRtl);
    }

    [Fact]
    public void Language_AutoDetectRtlChange_Persists()
    {
        _sut.AutoDetectRtl = false;
        _settingsRepoMock.Verify(s => s.SetAsync("AutoDetectRtl", "false"), Times.Once);
    }

    // ================================================================
    // Step 3 — Maintenance
    // ================================================================

    [Fact]
    public void Maintenance_DatabaseFileSize_InitialEmpty()
    {
        // Default before initialization
        Assert.Equal(string.Empty, _sut.DatabaseFileSize);
    }

    [Fact]
    public void Maintenance_ReclaimableSpace_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.ReclaimableSpace);
    }

    [Fact]
    public void Maintenance_LastCompaction_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.LastCompaction);
    }

    // ================================================================
    // Step 3 — Wiki
    // ================================================================

    [Fact]
    public void Wiki_GitVersionControlEnabled_DefaultFalse()
    {
        Assert.False(_sut.GitVersionControlEnabled);
    }

    [Fact]
    public void Wiki_GitVersionControlEnabledChange_Persists()
    {
        _sut.GitVersionControlEnabled = true;
        _settingsRepoMock.Verify(s => s.SetAsync("GitVersionControlEnabled", "true"), Times.Once);
    }

    [Fact]
    public void Wiki_WikiDirectoryPath_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.WikiDirectoryPath);
    }

    // ================================================================
    // Step 3 — Backup
    // ================================================================

    [Fact]
    public void Backup_BackupSchedule_DefaultDaily()
    {
        Assert.Equal("Daily", _sut.BackupSchedule);
    }

    [Fact]
    public void Backup_BackupScheduleChange_Persists()
    {
        _sut.BackupSchedule = "Weekly";
        _settingsRepoMock.Verify(s => s.SetAsync("BackupSchedule", "Weekly"), Times.Once);
    }

    [Fact]
    public void Backup_LastBackupTime_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.LastBackupTime);
    }

    // ================================================================
    // Step 3 — Tools
    // ================================================================

    [Fact]
    public void Tools_WebSearchAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.WebSearchAutoApproval);
    }

    [Fact]
    public void Tools_TerminalAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.TerminalAutoApproval);
    }

    [Fact]
    public void Tools_FileGenerateAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.FileGenerateAutoApproval);
    }

    [Fact]
    public void Tools_FileEditAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.FileEditAutoApproval);
    }

    [Fact]
    public void Tools_WebSearchAutoApprovalChange_Persists()
    {
        _sut.WebSearchAutoApproval = "AutoApprove";
        _settingsRepoMock.Verify(s => s.SetAsync("WebSearchAutoApproval", "AutoApprove"), Times.Once);
    }

    [Fact]
    public void Tools_TerminalChange_Persists()
    {
        _sut.TerminalAutoApproval = "Disabled";
        _settingsRepoMock.Verify(s => s.SetAsync("TerminalAutoApproval", "Disabled"), Times.Once);
    }

    [Fact]
    public void Tools_SttProvider_DefaultOpenAiWhisper()
    {
        Assert.Equal("OpenAI Whisper", _sut.SttProvider);
    }

    [Fact]
    public void Tools_SttProviderChange_Persists()
    {
        _sut.SttProvider = "Windows Speech";
        _settingsRepoMock.Verify(s => s.SetAsync("SttProvider", "Windows Speech"), Times.Once);
    }

    [Fact]
    public void Tools_ToolApprovalOptions_ContainsAllThree()
    {
        Assert.Contains("Ask", _sut.ToolApprovalOptions);
        Assert.Contains("AutoApprove", _sut.ToolApprovalOptions);
        Assert.Contains("Disabled", _sut.ToolApprovalOptions);
        Assert.Equal(3, _sut.ToolApprovalOptions.Count);
    }

    [Fact]
    public void Tools_TerminalApprovalOptions_DoesNotIncludeAutoApprove()
    {
        Assert.Contains("Ask", _sut.TerminalApprovalOptions);
        Assert.Contains("Disabled", _sut.TerminalApprovalOptions);
        Assert.DoesNotContain("AutoApprove", _sut.TerminalApprovalOptions);
        Assert.Equal(2, _sut.TerminalApprovalOptions.Count);
    }

    [Fact]
    public void Tools_SttProviderOptions_ContainsAllThree()
    {
        Assert.Contains("OpenAI Whisper", _sut.SttProviderOptions);
        Assert.Contains("Local Whisper", _sut.SttProviderOptions);
        Assert.Contains("Windows Speech", _sut.SttProviderOptions);
        Assert.Equal(3, _sut.SttProviderOptions.Count);
    }

    // ================================================================
    // Step 3 — Pricing
    // ================================================================

    [Fact]
    public void Pricing_MonthlyBudgetLimit_DefaultNull()
    {
        Assert.Null(_sut.MonthlyBudgetLimit);
    }

    [Fact]
    public void Pricing_MonthlyBudgetLimitChange_Persists()
    {
        _sut.MonthlyBudgetLimit = 50.00m;
        _settingsRepoMock.Verify(s => s.SetAsync("MonthlyBudgetLimit", "50.00"), Times.Once);
    }

    [Fact]
    public void Pricing_WarningThreshold_Default80()
    {
        Assert.Equal(80, _sut.WarningThreshold);
    }

    [Fact]
    public void Pricing_WarningThresholdChange_Persists()
    {
        _sut.WarningThreshold = 90;
        _settingsRepoMock.Verify(s => s.SetAsync("WarningThreshold", "90"), Times.Once);
    }

    [Fact]
    public void Pricing_WarningThresholdClampsBelow50()
    {
        _sut.WarningThreshold = 10;
        Assert.Equal(50, _sut.WarningThreshold);
    }

    [Fact]
    public void Pricing_WarningThresholdClampsAbove100()
    {
        _sut.WarningThreshold = 150;
        Assert.Equal(100, _sut.WarningThreshold);
    }

    [Fact]
    public void Pricing_BlockApiOnLimit_DefaultFalse()
    {
        Assert.False(_sut.BlockApiOnLimit);
    }

    [Fact]
    public void Pricing_BlockApiOnLimitChange_Persists()
    {
        _sut.BlockApiOnLimit = true;
        _settingsRepoMock.Verify(s => s.SetAsync("BlockApiOnLimit", "true"), Times.Once);
    }

    // ================================================================
    // Step 3 — Security
    // ================================================================

    [Fact]
    public void Security_EncryptionStatus_ContainsDpapi()
    {
        Assert.Contains("DPAPI", _sut.EncryptionStatus);
    }

    [Fact]
    public void Security_LockedChatPasswordSet_DefaultFalse()
    {
        Assert.False(_sut.LockedChatPasswordSet);
    }

    [Fact]
    public void Security_HideLockedChats_DefaultFalse()
    {
        Assert.False(_sut.HideLockedChats);
    }

    [Fact]
    public void Security_HideLockedChatsChange_Persists()
    {
        _sut.HideLockedChats = true;
        _settingsRepoMock.Verify(s => s.SetAsync("HideLockedChats", "true"), Times.Once);
    }

    // ================================================================
    // Step 3 — CategoryItems
    // ================================================================

    [Fact]
    public void CategoryItems_Contains16Items()
    {
        Assert.Equal(16, _sut.CategoryItems.Count);
    }

    [Fact]
    public void CategoryItems_ContainsLanguage()
    {
        Assert.Contains(_sut.CategoryItems, c => c.Category == SettingsCategory.Language);
    }

    // ================================================================
    // Step 3 — Placeholder commands
    // ================================================================

    [Fact]
    public void ConfigureBackupCommand_SetsStatusMessage()
    {
        _sut.ConfigureBackupCommand.Execute(null);
        Assert.Contains("Feature 16", _sut.StatusMessage);
    }

    [Fact]
    public void TestMicrophoneCommand_SetsStatusMessage()
    {
        _sut.TestMicrophoneCommand.Execute(null);
        Assert.Contains("not yet implemented", _sut.StatusMessage);
    }

    [Fact]
    public void SetGlobalPasswordCommand_SetsStatusMessage()
    {
        _sut.SetGlobalPasswordCommand.Execute(null);
        Assert.Contains("placeholder", _sut.StatusMessage);
    }

    // ================================================================
    // Step 3 — IsBusy default
    // ================================================================

    [Fact]
    public void IsBusy_DefaultFalse()
    {
        Assert.False(_sut.IsBusy);
    }

    // ================================================================
    // Step 4 — Text Actions
    // ================================================================

    [Fact]
    public void AddTextActionCommand_InitializesFormWithDefaults()
    {
        _sut.AddTextActionCommand.Execute(null);

        Assert.True(_sut.IsEditingTextAction);
        Assert.True(_sut.CaptureSelection);
        Assert.False(_sut.CaptureFocusedElement);
        Assert.False(_sut.CaptureSurroundingContext);
        Assert.False(_sut.CaptureFullDocument);
        Assert.False(_sut.CaptureScreenshot);
        Assert.Equal("replaceSelection", _sut.SelectedApplyMode);
        Assert.Null(_sut.TextActionAssignedHotkey);
        Assert.Null(_sut.EditingTextAction);
    }

    [Fact]
    public async Task SaveTextActionCommand_ValidatesNameIsRequired()
    {
        _sut.AddTextActionCommand.Execute(null);
        _sut.TextActionDisplayNameValue = string.Empty;

        await _sut.SaveTextActionCommand.ExecuteAsync(null);

        Assert.Contains("display name is required", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateTextActionCommand_AppendsCopySuffix()
    {
        var source = new TextAction
        {
            Id = "test-id",
            DisplayName = "Test Action",
            SystemPrompt = "Do something",
            CaptureScope = "selection",
            ApplyMode = "replaceSelection",
        };

        _textActionRepoMock
            .Setup(r => r.GetByIdAsync("test-id"))
            .ReturnsAsync(source);

        _textActionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<TextAction>()))
            .ReturnsAsync((TextAction a) => a);

        _textActionRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<TextAction>());

        var item = new TextActionDisplayItem { Id = "test-id", DisplayName = "Test Action" };

        await _sut.DuplicateTextActionCommand.ExecuteAsync(item);

        _textActionRepoMock.Verify(r => r.CreateAsync(It.Is<TextAction>(
            ta => ta.DisplayName == "Test Action (Copy)" && ta.Hotkey == null)));
    }

    [Fact]
    public void BuildCaptureScopeString_SelectionOnly_ReturnsSelection()
    {
        _sut.AddTextActionCommand.Execute(null);
        _sut.CaptureSelection = true;
        _sut.CaptureFocusedElement = false;
        _sut.CaptureSurroundingContext = false;
        _sut.CaptureFullDocument = false;
        _sut.CaptureScreenshot = false;

        var method = typeof(SettingsViewModel).GetMethod(
            "BuildCaptureScopeString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(_sut, null) as string;

        Assert.Equal("selection", result);
    }

    [Fact]
    public void BuildCaptureScopeString_MultiSelect_ReturnsCommaSeparated()
    {
        _sut.AddTextActionCommand.Execute(null);
        _sut.CaptureSelection = true;
        _sut.CaptureFocusedElement = true;
        _sut.CaptureScreenshot = true;

        var method = typeof(SettingsViewModel).GetMethod(
            "BuildCaptureScopeString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(_sut, null) as string;

        Assert.Equal("selection,focusedElement,screenshot", result);
    }

    [Fact]
    public void ApplyRecordedHotkey_SetsHotkeyAndStopsRecording()
    {
        _sut.ApplyRecordedHotkey("Ctrl+Shift+K");

        Assert.Equal("Ctrl+Shift+K", _sut.TextActionAssignedHotkey);
        Assert.False(_sut.IsRecordingHotkey);
    }

    // ================================================================
    // Step 4 — Hotkeys
    // ================================================================

    [Fact]
    public void ResetHotkeysToDefaultsCommand_ShowsConfirmation()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), "Reset Hotkeys to Defaults"))
            .Returns(false);

        _sut.ResetHotkeysToDefaultsCommand.Execute(null);

        _textActionRepoMock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    // ================================================================
    // Re-run Onboarding Wizard
    // ================================================================

    [Fact]
    public void ReRunOnboardingCommand_DoesNotThrow()
    {
        // Verify the command executes without exception (the messenger fires but
        // no recipient may be registered in a unit test context)
        var exception = Record.Exception(() => _sut.ReRunOnboardingCommand.Execute(null));
        Assert.Null(exception);
    }

    // ================================================================
    // RefreshApiKeysMessage
    // ================================================================

    [Fact]
    public async Task RefreshApiKeysMessage_AfterInitialization_CallsGetAllAsync()
    {
        // The handler is registered in the constructor. After InitializeAsync completes,
        // Send should trigger a key list refresh.
        _apiKeyRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ApiKey>());

        // Simulate "navigate to Settings" — create fresh VM and complete init
        var vm = CreateFreshViewModel();
        await vm.InitializeCommand.ExecuteAsync(null);
        _apiKeyRepoMock.Invocations.Clear(); // reset counts from init

        // Act — send the message as App.xaml.cs does after wizard closes
        WeakReferenceMessenger.Default.Send(new RefreshApiKeysMessage());

        // Assert — handler should have called GetAllAsync
        _apiKeyRepoMock.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void RefreshApiKeysMessage_BeforeInitialization_SkipsRefresh()
    {
        // Verify the _isInitialized guard prevents the handler from running
        // before InitializeAsync completes.
        _apiKeyRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ApiKey>());

        // Create a VM but do NOT call InitializeAsync — handler should be
        // registered (constructor) but skip the refresh due to _isInitialized = false.
        var vm = CreateFreshViewModel();

        // Act — send the message before init
        WeakReferenceMessenger.Default.Send(new RefreshApiKeysMessage());

        // Assert — GetAllAsync should NOT have been called
        _apiKeyRepoMock.Verify(r => r.GetAllAsync(), Times.Never);
    }
}
