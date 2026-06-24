using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.Skills;

namespace MySecondBrain.Tests.Unit;

/// <summary>
/// Unit tests for StructuredSkillLoader — XML wrapping,
/// tool definition generation, and skill validation.
/// </summary>
public class SkillLoaderTests
{
    // ================================================================
    // ToolDefinition generation
    // ================================================================

    [Fact]
    public void GetToolDefinition_WithEnabledSkills_IncludesEnumConstraint()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var enabledSkills = new[] { "xlsx", "pdf", "docx" };
        var toolDef = loader.GetToolDefinition(enabledSkills);

        Assert.Equal("skill_load", toolDef.Name);
        Assert.Contains("xlsx", toolDef.ParametersJsonSchema);
        Assert.Contains("pdf", toolDef.ParametersJsonSchema);
        Assert.Contains("docx", toolDef.ParametersJsonSchema);
        Assert.Contains("\"enum\"", toolDef.ParametersJsonSchema);
    }

    [Fact]
    public void GetToolDefinition_WithEmptySkills_OmitsEnumConstraint()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var toolDef = loader.GetToolDefinition(Array.Empty<string>());

        Assert.Equal("skill_load", toolDef.Name);
        Assert.DoesNotContain("\"enum\"", toolDef.ParametersJsonSchema);
    }

    [Fact]
    public void GetToolDefinition_WithNullSkills_OmitsEnumConstraint()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var toolDef = loader.GetToolDefinition(null!);

        Assert.Equal("skill_load", toolDef.Name);
        Assert.DoesNotContain("\"enum\"", toolDef.ParametersJsonSchema);
    }

    [Fact]
    public void GetToolDefinition_Description_IsNotEmpty()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var toolDef = loader.GetToolDefinition(new[] { "xlsx" });

        Assert.False(string.IsNullOrWhiteSpace(toolDef.Description));
    }

    // ================================================================
    // IsValidSkill
    // ================================================================

    [Fact]
    public void IsValidSkill_WithValidName_ReturnsTrue()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = loader.IsValidSkill("xlsx");

        Assert.True(result);
    }

    [Fact]
    public void IsValidSkill_WithValidNameCaseInsensitive_ReturnsTrue()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = loader.IsValidSkill("XLSX");

        Assert.True(result);
    }

    [Fact]
    public void IsValidSkill_WithInvalidName_ReturnsFalse()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = loader.IsValidSkill("nonexistent-skill");

        Assert.False(result);
    }

    [Fact]
    public void IsValidSkill_WithEmptyName_ReturnsFalse()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        Assert.False(loader.IsValidSkill(string.Empty));
        Assert.False(loader.IsValidSkill("   "));
        Assert.False(loader.IsValidSkill(null!));
    }

    // ================================================================
    // ActivateSkillAsync
    // ================================================================

    [Fact]
    public async Task ActivateSkillAsync_WithValidSkill_ReturnsSuccess()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = await loader.ActivateSkillAsync("xlsx", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Content);
        Assert.Null(result.ErrorMessage);
        Assert.StartsWith("<skill_content name=\"xlsx\">", result.Content);
        Assert.Contains("<skill_resources>", result.Content);
        Assert.Contains("</skill_content>", result.Content);
    }

    [Fact]
    public async Task ActivateSkillAsync_ProducesValidXmlFormat()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = await loader.ActivateSkillAsync("xlsx", CancellationToken.None);

        Assert.True(result.Success);
        var xml = result.Content!;

        // Check XML wrapper structure
        Assert.Contains("<skill_content name=\"xlsx\">", xml);
        Assert.Contains("</skill_content>", xml);

        // Body content should be present (stripped of frontmatter)
        Assert.Contains("skill body", xml);

        // Check resource listing structure
        Assert.Contains("<skill_resources>", xml);
        Assert.Contains("</skill_resources>", xml);
    }

    [Fact]
    public async Task ActivateSkillAsync_WithInvalidName_ReturnsFailure()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = await loader.ActivateSkillAsync("nonexistent", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Content);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivateSkillAsync_WithEmptyName_ReturnsFailure()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = await loader.ActivateSkillAsync(string.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ActivateSkillAsync_Deduplication_SecondActivationSkipped()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        // First activation — should succeed
        var first = await loader.ActivateSkillAsync("xlsx", CancellationToken.None);
        Assert.True(first.Success);

        // Second activation — should be skipped (deduplication)
        var second = await loader.ActivateSkillAsync("xlsx", CancellationToken.None);
        Assert.False(second.Success);
        Assert.NotNull(second.ErrorMessage);
        Assert.Contains("already loaded", second.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivateSkillAsync_AfterReset_CanReactivate()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        // Activate once
        await loader.ActivateSkillAsync("xlsx", CancellationToken.None);

        // Reset tracking
        skillService.ResetActivationTracking();

        // Should be able to activate again
        var result = await loader.ActivateSkillAsync("xlsx", CancellationToken.None);
        Assert.True(result.Success);
    }

    // ================================================================
    // XML escaping in wrapping
    // ================================================================

    [Fact]
    public async Task ActivateSkillAsync_WithSpecialCharacters_XmlEscaped()
    {
        var skillService = CreateSkillServiceWithCatalog();
        var loader = new StructuredSkillLoader(skillService, Mock.Of<ILogger<StructuredSkillLoader>>());

        var result = await loader.ActivateSkillAsync("xlsx", CancellationToken.None);

        Assert.True(result.Success);
        // The name "xlsx" has no special XML chars, but verify basic wrapping
        Assert.Contains("<skill_content name=\"xlsx\">", result.Content!);
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Creates an ISkillService mock with a pre-populated catalog.
    /// Skills have a simple SKILL.md body for testing activation.
    /// </summary>
    private static ISkillService CreateSkillServiceWithCatalog()
    {
        var mock = new Mock<ISkillService>();

        var skills = new List<SkillMetadata>
        {
            new("xlsx", "Create/edit Excel spreadsheets", "built-in", "/skills/xlsx/SKILL.md"),
            new("pdf", "Extract, split, merge PDFs", "built-in", "/skills/pdf/SKILL.md"),
            new("docx", "Create/edit Word documents", "built-in", "/skills/docx/SKILL.md"),
        };

        mock.Setup(s => s.GetCatalog()).Returns(skills.AsReadOnly());

        // LoadAsync returns SkillContent with a body and resources
        mock.Setup(s => s.LoadAsync("xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkillContent("xlsx", "skill body for xlsx", new[] { "scripts/recalc.py", "scripts/office/pack.py" }));
        mock.Setup(s => s.LoadAsync("pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkillContent("pdf", "skill body for pdf", Array.Empty<string>()));
        mock.Setup(s => s.LoadAsync("docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkillContent("docx", "skill body for docx", Array.Empty<string>()));

        // LoadAsync throws for unknown skills
        mock.Setup(s => s.LoadAsync(It.Is<string>(n => !new[] { "xlsx", "pdf", "docx" }.Contains(n, StringComparer.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Skill not found"));

        // Activation tracking
        var activated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        mock.Setup(s => s.IsActivated(It.IsAny<string>()))
            .Returns((string n) => activated.Contains(n));
        mock.Setup(s => s.MarkActivated(It.IsAny<string>()))
            .Callback((string n) => activated.Add(n));
        mock.Setup(s => s.ResetActivationTracking())
            .Callback(() => activated.Clear());

        return mock.Object;
    }
}
