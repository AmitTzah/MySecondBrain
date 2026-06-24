using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Coordinates system prompt construction by bridging between toolbar toggle state
/// (enabled tools, enabled skills) and the static <see cref="SystemPromptBuilder"/>.
/// </summary>
public class SystemPromptCoordinator
{
    private readonly ISkillService _skillService;

    public SystemPromptCoordinator(ISkillService skillService)
    {
        _skillService = skillService;
    }

    // ================================================================
    // Additive system prompt assembly
    // ================================================================

    /// <summary>
    /// Build the additive system prompt for the current chat state.
    /// Uses the active persona's system message, enabled tool/skill toggles,
    /// skill catalog, workspace path, and bash availability.
    /// Returns null when everything is disabled (plain chat with no capabilities).
    /// </summary>
    public string? GetSystemPrompt(
        string? personaSystemMessage,
        IReadOnlySet<string> enabledToolNames,
        IReadOnlySet<string> enabledSkillNames,
        string workspacePath)
    {
        return SystemPromptBuilder.BuildSystemPrompt(
            personaSystemMessage,
            enabledToolNames,
            enabledSkillNames,
            _skillService.GetCatalog(),
            workspacePath,
            SystemPromptBuilder.DetectBashAvailable());
    }

    /// <summary>
    /// Build the filtered list of tool names for the API tools array.
    /// ask_user_input is always present. skill_load only when ≥1 skill enabled.
    /// Returns empty array when everything is disabled.
    /// </summary>
    public static IReadOnlyList<string> GetFilteredToolNames(
        IReadOnlySet<string> enabledToolNames,
        int enabledSkillCount)
    {
        return SystemPromptBuilder.BuildFilteredToolNames(
            enabledToolNames,
            enabledSkillCount);
    }

    /// <summary>
    /// Build the skill catalog XML block for the system prompt.
    /// Only includes skills that are currently enabled for this chat.
    /// Returns empty string when no skills are enabled.
    /// </summary>
    public string GetSkillCatalogXml(IReadOnlySet<string> enabledSkillNames)
    {
        return SystemPromptBuilder.BuildSkillCatalogXml(
            _skillService.GetCatalog(),
            enabledSkillNames);
    }

    /// <summary>
    /// Resolves {{variables}} in a system prompt template.
    /// Delegates to <see cref="SystemPromptBuilder.ResolveSystemPromptVariables"/>.
    /// </summary>
    public static string ResolveSystemPrompt(string template)
    {
        return SystemPromptBuilder.ResolveSystemPromptVariables(template);
    }
}
