using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat.Import;

public class ChatGPTImporter : IChatImporter
{
    private readonly ILogger<ChatGPTImporter> _logger;

    public ChatGPTImporter(ILogger<ChatGPTImporter> logger)
    {
        _logger = logger;
    }

    public string FormatName => "ChatGPT";

    public string[] SupportedFileExtensions => new[] { ".json" };

    public Task<ImportResult> ImportAsync(string filePath, CancellationToken ct) =>
        Task.FromResult<ImportResult>(default!);

    public Task<ImportValidationResult> ValidateAsync(string filePath, CancellationToken ct) =>
        Task.FromResult<ImportValidationResult>(default!);
}
