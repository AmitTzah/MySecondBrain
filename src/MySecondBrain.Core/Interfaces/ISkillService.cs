using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

/// <summary>
/// Central service for skill discovery, loading, and lifecycle management.
/// Implements the Agent Skills open standard.
/// Skills are in-memory instructions (not persisted to SQLite) —
/// re-discovered each launch from embedded resources and filesystem paths.
/// </summary>
public interface ISkillService
{
    /// <summary>
    /// Discovery — scans all configured locations (embedded resources, user skills,
    /// cross-client directories) and returns metadata for every skill found.
    /// Called at startup.
    /// </summary>
    Task<IReadOnlyList<SkillMetadata>> DiscoverAsync(CancellationToken ct);

    /// <summary>
    /// Get metadata for the skill catalog (used during system prompt construction).
    /// Returns the result of the last successful discovery scan.
    /// </summary>
    IReadOnlyList<SkillMetadata> GetCatalog();

    /// <summary>
    /// Load full skill content (tier 2 activation). Parses the SKILL.md file,
    /// strips YAML frontmatter, and returns structured content plus resource references.
    /// </summary>
    Task<SkillContent> LoadAsync(string skillName, CancellationToken ct);

    /// <summary>
    /// List bundled resources for a skill (scripts, reference files, assets).
    /// Returns relative paths within the skill's resource directory.
    /// </summary>
    Task<IReadOnlyList<string>> ListResourcesAsync(string skillName, CancellationToken ct);

    /// <summary>
    /// Check if a skill has been activated in the current session.
    /// Used for deduplication — skills should only be sent to the model once per chat.
    /// </summary>
    bool IsActivated(string skillName);

    /// <summary>
    /// Mark a skill as activated (for deduplication tracking).
    /// Called after successful skill_load tool execution.
    /// </summary>
    void MarkActivated(string skillName);

    /// <summary>
    /// Reset activation tracking for a new chat session.
    /// Allows skills to be re-loaded in a new conversation.
    /// </summary>
    void ResetActivationTracking();

    /// <summary>
    /// Get skill dependency requirements (tools, packages, system dependencies).
    /// Returns null if the skill has no declared dependencies.
    /// </summary>
    SkillDependencies? GetDependencies(string skillName);
}
