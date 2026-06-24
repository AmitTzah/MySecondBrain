using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class SkillLoadToolExecutor : IToolExecutor
{
    private readonly ISkillLoader _skillLoader;
    private readonly ILogger<SkillLoadToolExecutor> _logger;

    public SkillLoadToolExecutor(
        ISkillLoader skillLoader,
        ILogger<SkillLoadToolExecutor> logger)
    {
        _skillLoader = skillLoader;
        _logger = logger;
    }

    public string ToolName => "skill_load";

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
