using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Step 1 — Persona: select a starter persona or create one from scratch.
/// </summary>
public partial class OnboardingWizardViewModel
{
    public ObservableCollection<StarterPersonaCard> StarterPersonas { get; }

    [ObservableProperty]
    private StarterPersonaCard? _selectedStarterPersona;

    [ObservableProperty]
    private string _personaDisplayName = string.Empty;

    [ObservableProperty]
    private string _personaSystemPrompt = string.Empty;

    [ObservableProperty]
    private string _personaChatMode = "Standard";

    [ObservableProperty]
    private bool _isCreatingFromScratch;

    public IReadOnlyList<string> ChatModeOptions { get; } = ["Standard", "TextCompletion"];

    partial void OnSelectedStarterPersonaChanged(StarterPersonaCard? value)
    {
        if (value is null || _isCreatingFromScratch) return;
        PersonaDisplayName = value.DisplayName;
        PersonaSystemPrompt = value.SystemPrompt;
    }

    [RelayCommand]
    private void SelectPersona(StarterPersonaCard? card)
    {
        if (card is null) return;

        // Deselect all, select the clicked one
        foreach (var p in StarterPersonas)
            p.IsSelected = p.Id == card.Id;

        SelectedStarterPersona = card;
        IsCreatingFromScratch = false;
        PersonaDisplayName = card.DisplayName;
        PersonaSystemPrompt = card.SystemPrompt;
    }

    [RelayCommand]
    private async Task SavePersonaAsync()
    {
        if (string.IsNullOrWhiteSpace(PersonaDisplayName))
        {
            _confirmationService.Confirm("Display name is required.", "Validation");
            return;
        }

        try
        {
            var persona = new Persona
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = PersonaDisplayName,
                SystemPrompt = PersonaSystemPrompt,
                DefaultChatMode = PersonaChatMode,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await _personaRepo.CreateAsync(persona);
            _logger.LogInformation("Onboarding: created persona '{Name}'", persona.DisplayName);

            await PersistStepCompletedAsync(1);
            Step2Completed = true;
            CurrentStep = 2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save persona during onboarding");
            _confirmationService.Confirm($"Could not save persona: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void CreateFromScratch()
    {
        IsCreatingFromScratch = true;
        SelectedStarterPersona = null;
        PersonaDisplayName = string.Empty;
        PersonaSystemPrompt = string.Empty;
    }
}
