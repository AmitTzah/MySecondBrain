using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class ImageSearchToolExecutor : IToolExecutor
{
    private readonly IEnumerable<ISearchProvider> _searchProviders;
    private readonly ILogger<ImageSearchToolExecutor> _logger;

    public ImageSearchToolExecutor(
        IEnumerable<ISearchProvider> searchProviders,
        ILogger<ImageSearchToolExecutor> logger)
    {
        _searchProviders = searchProviders;
        _logger = logger;
    }

    public string ToolName => "image_search";

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
