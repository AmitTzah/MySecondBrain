namespace MySecondBrain.Core.Models;

/// <summary>
/// Metadata describing a skill's identity and origin.
/// Skills are in-memory instructions (not persisted to SQLite) —
/// re-discovered each launch from embedded resources and filesystem paths.
/// </summary>
public record SkillMetadata(
    string Name,
    string Description,
    string Source,
    string Location
);

/// <summary>
/// The parsed content of a skill, including its body and resource references.
/// </summary>
public record SkillContent(
    string Name,
    string Body,
    IReadOnlyList<string> Resources
);

/// <summary>
/// Declared dependencies a skill may require at activation time.
/// </summary>
public record SkillDependencies(
    IReadOnlyList<string>? Tools,
    IReadOnlyList<string>? Packages,
    IReadOnlyList<string>? System
);

/// <summary>
/// Result returned after attempting to activate a skill.
/// </summary>
public record SkillActivationResult(
    bool Success,
    string? Content,
    string? ErrorMessage
);
