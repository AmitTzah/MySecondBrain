using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Skills;

/// <summary>
/// Discovers, catalogs, loads, and tracks activation state for Agent Skills.
/// Skills are in-memory instructions discovered from exactly 2 locations:
/// embedded resources (built-in in DLL) and the user skills directory
/// (%LOCALAPPDATA%/MySecondBrain/skills/).
/// Re-discovered each launch, not persisted to SQLite.
/// </summary>
public sealed class AgentSkillService : ISkillService
{
    private readonly ILogger<AgentSkillService> _logger;
    private readonly ConcurrentDictionary<string, bool> _activationState = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SkillMetadata> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SkillDependencies?> _dependencies = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex FrontMatterRegex = new(
        @"^---\s*\n(.*?)\n---",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex NameRegex = new(
        @"^name:\s*(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex DescriptionRegex = new(
        @"^description:\s*(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public AgentSkillService(ILogger<AgentSkillService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discovery — scans 2 locations (embedded resources + user skills directory)
    /// and returns metadata for every skill found. User skills override built-in
    /// skills with the same name.
    /// </summary>
    public Task<IReadOnlyList<SkillMetadata>> DiscoverAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _catalog.Clear();
        _dependencies.Clear();

        // Priority order: embedded resources (lowest), user skills directory (highest).
        // User skills override built-in skills with the same name.
        var discovered = new List<(SkillMetadata meta, bool isOverride)>();

        // 1. Embedded resources in MySecondBrain.UI.dll (built-in skills)
        DiscoverEmbeddedSkills(discovered);

        // 2. %LOCALAPPDATA%/MySecondBrain/skills/ (user skills — override built-in)
        var localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "skills");
        DiscoverFilesystemSkills(localAppData, "user", discovered);

        // Cross-client paths (.agents/, .claude/) are no longer scanned per 2026-06-25 vision update.
        // Skills discovered from only 2 locations: embedded (built-in) + user skills directory.

        // Collision resolution: user skills override built-in skills with the same name
        var resolved = new Dictionary<string, SkillMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var (meta, isOverride) in discovered)
        {
            if (resolved.TryGetValue(meta.Name, out var existing))
            {
                _logger.LogWarning(
                    "Skill collision: '{Name}' from '{NewSource}' ({NewLocation}) overrides '{OldSource}' ({OldLocation})",
                    meta.Name, meta.Source, meta.Location, existing.Source, existing.Location);
            }
            resolved[meta.Name] = meta;
        }

        foreach (var kvp in resolved)
        {
            _catalog.TryAdd(kvp.Key, kvp.Value);
        }

        var result = resolved.Values.ToList().AsReadOnly();
        _logger.LogInformation("Discovered {Count} skills", result.Count);
        return Task.FromResult((IReadOnlyList<SkillMetadata>)result);
    }

    /// <summary>
    /// Get metadata for the skill catalog.
    /// </summary>
    public IReadOnlyList<SkillMetadata> GetCatalog()
    {
        return _catalog.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Load full skill content. Strips YAML frontmatter and returns body plus resource references.
    /// </summary>
    public async Task<SkillContent> LoadAsync(string skillName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_catalog.TryGetValue(skillName, out var metadata))
        {
            throw new ArgumentException($"Skill '{skillName}' not found in catalog.", nameof(skillName));
        }

        var body = await ReadSkillBodyAsync(metadata, ct);
        var resources = await ListResourcesForSkillAsync(metadata, ct);

        return new SkillContent(skillName, body, resources);
    }

    /// <summary>
    /// List bundled resources for a skill.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListResourcesAsync(string skillName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_catalog.TryGetValue(skillName, out var metadata))
        {
            return Array.Empty<string>();
        }

        return await ListResourcesForSkillAsync(metadata, ct);
    }

    /// <summary>
    /// Check if a skill has been activated in the current session.
    /// </summary>
    public bool IsActivated(string skillName)
    {
        return _activationState.TryGetValue(skillName, out var activated) && activated;
    }

    /// <summary>
    /// Mark a skill as activated.
    /// </summary>
    public void MarkActivated(string skillName)
    {
        _activationState[skillName] = true;
    }

    /// <summary>
    /// Reset activation tracking for a new chat session.
    /// </summary>
    public void ResetActivationTracking()
    {
        _activationState.Clear();
    }

    /// <summary>
    /// Get skill dependency requirements.
    /// </summary>
    public SkillDependencies? GetDependencies(string skillName)
    {
        _dependencies.TryGetValue(skillName, out var deps);
        return deps;
    }

    // ================================================================
    // Embedded resource discovery
    // ================================================================

    private void DiscoverEmbeddedSkills(List<(SkillMetadata meta, bool isOverride)> discovered)
    {
        Assembly? uiAssembly = null;
        try
        {
            uiAssembly = Assembly.Load("MySecondBrain.UI");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load MySecondBrain.UI assembly for embedded skill discovery");
            return;
        }

        var resourceNames = uiAssembly.GetManifestResourceNames();
        var skillPrefix = "MySecondBrain.UI.Skills.anthropic.";

        // Group by skill directory — each SKILL.md is under Skills/anthropic/{skillName}/SKILL.md
        var skillDirectories = resourceNames
            .Where(r => r.StartsWith(skillPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(r =>
            {
                // MySecondBrain.UI.Skills.anthropic.xlsx.SKILL.md → xlsx
                var relative = r.Substring(skillPrefix.Length);
                var parts = relative.Split('.');
                return parts.Length >= 2 ? parts[0] : null;
            })
            .Where(d => d != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        foreach (var skillDir in skillDirectories)
        {
            var skillResourcePath = $"{skillPrefix}{skillDir}.SKILL.md";
            if (!resourceNames.Contains(skillResourcePath, StringComparer.OrdinalIgnoreCase))
                continue;

            try
            {
                using var stream = uiAssembly.GetManifestResourceStream(skillResourcePath);
                if (stream == null) continue;

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                var (name, description) = ParseFrontMatter(content);
                if (string.IsNullOrWhiteSpace(description))
                {
                    _logger.LogWarning("Skipping embedded skill '{Dir}': missing or empty description", skillDir);
                    continue;
                }

                var skillName = !string.IsNullOrWhiteSpace(name) ? name.Trim() : skillDir;
                if (!skillName.Equals(skillDir, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Embedded skill name '{Name}' doesn't match directory '{Dir}'",
                        skillName, skillDir);
                    // Lenient: load anyway
                }

                var location = $"res://MySecondBrain.UI/{skillResourcePath}";
                discovered.Add((
                    new SkillMetadata(skillName, description.Trim('"'), "built-in", location),
                    false));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse embedded skill '{Dir}'", skillDir);
            }
        }
    }

    // ================================================================
    // Filesystem skill discovery
    // ================================================================

    private void DiscoverFilesystemSkills(
        string basePath,
        string source,
        List<(SkillMetadata meta, bool isOverride)> discovered)
    {
        if (!Directory.Exists(basePath))
            return;

        try
        {
            var directories = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);

            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith('.') ||
                    string.Equals(dirName, "node_modules", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dirName, ".git", StringComparison.OrdinalIgnoreCase))
                    continue;

                var skillMdPath = Path.Combine(dir, "SKILL.md");
                if (!File.Exists(skillMdPath))
                    continue;

                try
                {
                    var content = File.ReadAllText(skillMdPath);
                    var (name, description) = ParseFrontMatter(content);

                    if (string.IsNullOrWhiteSpace(description))
                    {
                        _logger.LogWarning("Skipping filesystem skill '{Dir}': missing or empty description", dir);
                        continue;
                    }

                    var skillName = !string.IsNullOrWhiteSpace(name) ? name.Trim() : dirName;
                    if (!skillName.Equals(dirName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Filesystem skill name '{Name}' doesn't match directory '{Dir}'",
                            skillName, dirName);
                    }

                    discovered.Add((
                        new SkillMetadata(skillName, description.Trim('"'), source, skillMdPath),
                        true));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse filesystem skill at '{Path}'", skillMdPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan filesystem skills path '{Path}'", basePath);
        }
    }

    // ================================================================
    // Reading skill body
    // ================================================================

    private async Task<string> ReadSkillBodyAsync(SkillMetadata metadata, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (metadata.Location.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            // Embedded resource: res://MySecondBrain.UI/MySecondBrain.UI.Skills.anthropic.xlsx.SKILL.md
            var parts = metadata.Location.Split(new[] { "//" }, 2, StringSplitOptions.None);
            if (parts.Length < 2)
                return string.Empty;

            var resourcePath = parts[1]; // assemblyName/resourcePath

            var slashIdx = resourcePath.IndexOf('/');
            if (slashIdx < 0)
                return string.Empty;

            var assemblyName = resourcePath.Substring(0, slashIdx);
            var resourceName = resourcePath.Substring(slashIdx + 1);

            try
            {
                var assembly = Assembly.Load(assemblyName);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return string.Empty;

                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync(ct);
                return StripFrontMatter(content);
            }
            catch
            {
                return string.Empty;
            }
        }

        // Filesystem path
        if (!File.Exists(metadata.Location))
            return string.Empty;

        var body = await File.ReadAllTextAsync(metadata.Location, ct);
        return StripFrontMatter(body);
    }

    // ================================================================
    // Listing skill resources
    // ================================================================

    private async Task<IReadOnlyList<string>> ListResourcesForSkillAsync(
        SkillMetadata metadata, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (metadata.Location.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            var parts = metadata.Location.Split(new[] { "//" }, 2, StringSplitOptions.None);
            if (parts.Length < 2)
                return Array.Empty<string>();

            var resourcePath = parts[1];
            var slashIdx = resourcePath.IndexOf('/');
            if (slashIdx < 0) return Array.Empty<string>();

            var assemblyName = resourcePath.Substring(0, slashIdx);
            var resourceName = resourcePath.Substring(slashIdx + 1);

            try
            {
                var assembly = Assembly.Load(assemblyName);
                var allResources = assembly.GetManifestResourceNames();

                // The resource name looks like: MySecondBrain.UI.Skills.anthropic.xlsx.SKILL.md
                // We need to find the prefix for this skill.
                // e.g., "MySecondBrain.UI.Skills.anthropic.xlsx." 
                var skillPrefix = resourceName.Substring(0, resourceName.LastIndexOf('.') + 1);

                var files = allResources
                    .Where(r => r.StartsWith(skillPrefix, StringComparison.OrdinalIgnoreCase) &&
                                !r.EndsWith(".SKILL.md", StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.Substring(skillPrefix.Length).Replace('.', Path.DirectorySeparatorChar))
                    .OrderBy(f => f)
                    .ToList();

                return files.AsReadOnly();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        // Filesystem: list files under the skill directory (recursive, exclude SKILL.md)
        var directory = Path.GetDirectoryName(metadata.Location);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Array.Empty<string>();

        try
        {
            var files = await Task.Run(() =>
            {
                return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                    .Where(f => !string.Equals(Path.GetFileName(f), "SKILL.md", StringComparison.OrdinalIgnoreCase))
                    .Select(f => Path.GetRelativePath(directory, f))
                    .OrderBy(f => f)
                    .ToList();
            }, ct);

            return files.AsReadOnly();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // ================================================================
    // YAML frontmatter parsing
    // ================================================================

    internal static (string? name, string? description) ParseFrontMatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (null, null);

        var match = FrontMatterRegex.Match(content);
        if (!match.Success)
            return (null, null);

        var frontMatter = match.Groups[1].Value;

        var nameMatch = NameRegex.Match(frontMatter);
        var descMatch = DescriptionRegex.Match(frontMatter);

        var name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : null;
        var description = descMatch.Success ? descMatch.Groups[1].Value.Trim().Trim('"') : null;

        return (name, description);
    }

    private static string StripFrontMatter(string content)
    {
        return FrontMatterRegex.Replace(content, "").TrimStart('\n', '\r');
    }
}
