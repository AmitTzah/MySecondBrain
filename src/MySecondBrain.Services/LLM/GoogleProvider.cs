using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class GoogleProvider : ILLMProvider
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<GoogleProvider> _logger;

    public GoogleProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<GoogleProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "Google";

    public ProviderType Type => ProviderType.Google;

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct) =>
        Task.FromResult<ChatResponse>(default!);

#pragma warning disable CS1998, CS8425
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken ct)
#pragma warning restore CS1998, CS8425
    {
        yield break;
    }

    public async Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}?key={apiKey}";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            using var response = await http.GetAsync(url, ct);
            var masked = ApiKeyHelper.MaskKey(apiKey);
            _logger.LogDebug("Key validation for {Provider}: HTTP {Status} (key: {Key})", ProviderName, (int)response.StatusCode, masked);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error during key validation for {Provider}", ProviderName);
            return false;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout during key validation for {Provider}", ProviderName);
            return false;
        }
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct)
    {
        try
        {
            // Build URL with API key from repository
            var apiKeys = await _apiKeyRepo.GetByProviderAsync(ProviderType.Google);
            var firstKey = apiKeys?.FirstOrDefault();
            string url;
            if (firstKey != null)
            {
                var decrypted = _encryptionService.UnprotectString(firstKey.EncryptedValue);
                if (!string.IsNullOrEmpty(decrypted))
                {
                    url = $"{BaseUrl}?key={decrypted}";
                }
                else
                {
                    url = BaseUrl;
                }
            }
            else
            {
                url = BaseUrl;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var response = await http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var models = new List<ModelInfo>();

            if (doc.RootElement.TryGetProperty("models", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString() ?? string.Empty;
                    // Extract model ID after "models/"
                    var id = name.StartsWith("models/") ? name["models/".Length..] : name;
                    var displayName = item.TryGetProperty("displayName", out var dn)
                        ? dn.GetString() ?? id
                        : id;
                    models.Add(new ModelInfo(id, displayName, 128000));
                }
            }

            _logger.LogDebug("ListModels for {Provider}: found {Count} models", ProviderName, models.Count);
            return models.AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error during model listing for {Provider}", ProviderName);
            return Array.Empty<ModelInfo>();
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout during model listing for {Provider}", ProviderName);
            return Array.Empty<ModelInfo>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parse error during model listing for {Provider}", ProviderName);
            return Array.Empty<ModelInfo>();
        }
    }

}
