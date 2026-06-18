using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IChatImporter
{
    string FormatName { get; }
    string[] SupportedFileExtensions { get; }

    Task<ImportResult> ImportAsync(string filePath, CancellationToken ct);

    Task<ImportValidationResult> ValidateAsync(string filePath, CancellationToken ct);
}
