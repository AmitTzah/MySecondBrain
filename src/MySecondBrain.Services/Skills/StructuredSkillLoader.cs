using System.Text;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Skills;

/// <summary>
/// Handles the skill_load tool invocation. Wraps skill content in structured
/// XML tags and provides the skill_load tool schema for the API tools array.
/// </summary>
public sealed class StructuredSkillLoader : ISkillLoader
{
    private readonly ISkillService _skillService;
    private readonly ILogger<StructuredSkillLoader> _logger;

    public StructuredSkillLoader(
        ISkillService skillService,
        ILogger<StructuredSkillLoader> logger)
    {
        _skillService = skillService ?? throw new ArgumentNullException(nameof(skillService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Activate a skill: check deduplication, load content, wrap in XML.
    /// </summary>
    public async Task<SkillActivationResult> ActivateSkillAsync(string skillName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return new SkillActivationResult(false, null, "Skill name cannot be empty.");
        }

        // Check if the skill exists in the catalog
        if (!_skillService.GetCatalog().Any(s =>
                s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase)))
        {
            return new SkillActivationResult(false, null,
                $"Skill '{skillName}' not found. Use a valid skill name from the available skills catalog.");
        }

        // Deduplication: skip if already activated in this session
        if (_skillService.IsActivated(skillName))
        {
            _logger.LogInformation("Skill '{Name}' already activated in this session - skipping re-injection", skillName);
            return new SkillActivationResult(false, null,
                $"Skill '{skillName}' is already loaded in the current session.");
        }

        try
        {
            // Load skill content (tier 2 activation)
            var content = await _skillService.LoadAsync(skillName, ct);

            // Build the XML-wrapped output
            var sb = new StringBuilder();
            sb.AppendLine($"<skill_content name=\"{EscapeXml(skillName)}\">");
            sb.AppendLine(content.Body);
            sb.AppendLine("<skill_resources>");

            foreach (var resource in content.Resources)
            {
                sb.AppendLine($"  <file>{EscapeXml(resource)}</file>");
            }

            sb.AppendLine("</skill_resources>");
            sb.Append("</skill_content>");

            // Mark as activated
            _skillService.MarkActivated(skillName);

            _logger.LogInformation("Skill '{Name}' activated successfully with {ResourceCount} resources",
                skillName, content.Resources.Count);

            return new SkillActivationResult(true, sb.ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate skill '{Name}'", skillName);
            return new SkillActivationResult(false, null,
                $"Failed to load skill '{skillName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Get the skill_load tool definition with enum constraint populated from enabled skill names.
    /// </summary>
    public ToolDefinition GetToolDefinition(IReadOnlyList<string> enabledSkillNames)
    {
        if (enabledSkillNames == null || enabledSkillNames.Count == 0)
        {
            // Return the schema without enum constraint if no skills are enabled
            var emptySchema = """
            {
              "type": "object",
              "properties": {
                "skill": {
                  "type": "string",
                  "description": "Name of the skill to load"
                }
              },
              "required": ["skill"]
            }
            """;
            return new ToolDefinition(
                "skill_load",
                "Load a skill's full instructions when a task needs specialized domain knowledge. No skills are currently enabled.",
                emptySchema);
        }

        var enumValues = string.Join(",\n      ", enabledSkillNames.Select(n => $"\"{EscapeJson(n)}\""));

        var schema = $$"""
        {
          "type": "object",
          "properties": {
            "skill": {
              "type": "string",
              "enum": [
                {{enumValues}}
              ],
              "description": "Name of the skill to load"
            }
          },
          "required": ["skill"]
        }
        """;

        return new ToolDefinition(
            "skill_load",
            "Load a skill's full instructions when a task needs specialized domain knowledge. Skills are listed in the <available_skills> section of the system prompt.",
            schema);
    }

    /// <summary>
    /// Check if a skill name is valid (exists in the discovered catalog).
    /// </summary>
    public bool IsValidSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        return _skillService.GetCatalog().Any(s =>
            s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&" + "amp;")
            .Replace("<", "&" + "lt;")
            .Replace(">", "&" + "gt;")
            .Replace("\"", "&" + "quot;")
            .Replace("'", "&" + "apos;");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
