using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat.Import;

public class ClaudeImporter : IChatImporter
{
    private readonly ILogger<ClaudeImporter> _logger;

    public ClaudeImporter(ILogger<ClaudeImporter> logger)
    {
        _logger = logger;
    }

    public string FormatName => "Claude";

    public string[] SupportedFileExtensions => new[] { ".json" };

    public Task<ImportResult> ImportAsync(string filePath, CancellationToken ct) =>
        Task.FromResult<ImportResult>(default!);

    public Task<ImportValidationResult> ValidateAsync(string filePath, CancellationToken ct) =>
        Task.FromResult<ImportValidationResult>(default!);
}
