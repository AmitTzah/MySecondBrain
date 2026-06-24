using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

/// <summary>
/// Handles the skill_load tool invocation. Wraps skill content in structured
/// XML tags for context management and provides the skill_load tool schema
/// for the API tools array.
/// </summary>
public interface ISkillLoader
{
    /// <summary>
    /// Load a skill via its name and return structured wrapped content.
    /// Reads the SKILL.md through <see cref="ISkillService"/>, strips YAML frontmatter,
    /// wraps the body in <c>skill_content</c> tags with <c>skill_resources</c> listing.
    /// Returns a failed result (Success=false) if the skill is already activated
    /// in the current session (deduplication), or if the skill name is invalid.
    /// </summary>
    Task<SkillActivationResult> ActivateSkillAsync(string skillName, CancellationToken ct);

    /// <summary>
    /// Get the skill_load tool definition for the API tools array.
    /// The <paramref name="enabledSkillNames"/> list is used to build
    /// the enum of valid skill options in the tool's parameter schema.
    /// </summary>
    ToolDefinition GetToolDefinition(IReadOnlyList<string> enabledSkillNames);

    /// <summary>
    /// Check if a skill name is valid (exists in the discovered catalog).
    /// </summary>
    bool IsValidSkill(string skillName);
}
