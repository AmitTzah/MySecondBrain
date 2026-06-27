using System.IO;
using System.Text.Json;
using System.Threading;

namespace MySecondBrain.Services.LLM;

/// <summary>
/// Writes per-call API request/response JSON to a global _api_history.json file
/// so users can inspect raw API exchanges via the 📡 API History button.
/// </summary>
public static class ApiHistoryHelper
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Returns the path to the global API history file.
    /// </summary>
    public static string GetHistoryPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "MySecondBrain", "_api_history.json");
    }

    /// <summary>
    /// Appends a single API call entry to the history file.
    /// Creates the file with an empty array if it doesn't exist.
    /// Thread-safe via <see cref="SemaphoreSlim"/> — concurrent writes are serialized.
    /// </summary>
    public static async Task AppendEntryAsync(
        string historyPath,
        string url,
        string requestBody,
        string responseBody,
        string finishReason,
        CancellationToken ct)
    {
        try
        {
            var dir = Path.GetDirectoryName(historyPath);
            if (dir is not null) Directory.CreateDirectory(dir);

            await Lock.WaitAsync(ct);
            try
            {
                // Read existing entries as JsonElement for schema consistency
                List<JsonElement> entries;
                if (File.Exists(historyPath))
                {
                    var existing = await File.ReadAllTextAsync(historyPath, ct);
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<JsonElement>(existing);
                        entries = parsed.ValueKind == JsonValueKind.Array
                            ? [.. parsed.EnumerateArray()]
                            : [];
                    }
                    catch
                    {
                        entries = [];
                    }
                }
                else
                {
                    entries = [];
                }

                // Build new entry as an expandable object
                var entry = new
                {
                    timestamp = DateTimeOffset.UtcNow.ToString("O"),
                    url,
                    request = requestBody,
                    response = responseBody,
                    finish_reason = finishReason,
                };
                var entryEl = JsonSerializer.SerializeToElement(entry, JsonOptions);

                // Append and write
                entries.Add(entryEl);
                var json = JsonSerializer.Serialize(entries, JsonOptions);
                await File.WriteAllTextAsync(historyPath, json, ct);
            }
            finally
            {
                Lock.Release();
            }
        }
        catch
        {
            // Swallow all errors — API history is best-effort
        }
    }
}
