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

public abstract class SettingsViewModelTestBase
{
    protected readonly Mock<ISettingsRepository> _settingsRepoMock;
    protected readonly Mock<IThemeProvider> _themeProviderMock;
    protected readonly Mock<IApiKeyRepository> _apiKeyRepoMock;
    protected readonly Mock<IEncryptionService> _encryptionServiceMock;
    protected readonly Mock<ILLMProviderService> _llmProviderServiceMock;
    protected readonly Mock<IClipboardService> _clipboardServiceMock;
    protected readonly Mock<IConfirmationService> _confirmationServiceMock;
    protected readonly Mock<IModelConfigurationRepository> _modelConfigRepoMock;
    protected readonly Mock<IPersonaRepository> _personaRepoMock;
    protected readonly Mock<IUpdateChecker> _updateCheckerMock;
    protected readonly Mock<ILogger<SettingsViewModel>> _loggerMock;
    protected readonly Mock<IWikiService> _wikiServiceMock;
    protected readonly Mock<IBackupProvider> _backupProviderMock;
    protected readonly Mock<ITextActionRepository> _textActionRepoMock;
    protected readonly Mock<AppDbContext> _dbContextMock;
    protected readonly SettingsViewModel _sut;

    public SettingsViewModelTestBase()
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

    /// <summary>
    /// Creates a fresh SettingsViewModel instance (simulating recreation after navigation).
    /// </summary>
    protected SettingsViewModel CreateFreshViewModel()
    {
        return new SettingsViewModel(
            _settingsRepoMock.Object,
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
}
