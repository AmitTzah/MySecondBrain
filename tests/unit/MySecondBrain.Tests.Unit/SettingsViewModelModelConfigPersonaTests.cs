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

public class SettingsViewModelModelConfigPersonaTests : SettingsViewModelTestBase
{
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
}
