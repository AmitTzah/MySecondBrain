using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class AnthropicProvider : ILLMProvider
{
    private const string BaseUrl = "https://api.anthropic.com/v1/models";
    private const string AnthropicVersion = "2023-06-01";
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<AnthropicProvider> _logger;

    public AnthropicProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<AnthropicProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "Anthropic";

    public ProviderType Type => ProviderType.Anthropic;

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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);

            using var response = await http.SendAsync(request, ct);
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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);

            // Look up API key from repository for authenticated listing
            var apiKeys = await _apiKeyRepo.GetByProviderAsync(ProviderType.Anthropic);
            var firstKey = apiKeys?.FirstOrDefault();
            if (firstKey != null)
            {
                var decrypted = _encryptionService.UnprotectString(firstKey.EncryptedValue);
                if (!string.IsNullOrEmpty(decrypted))
                {
                    request.Headers.Add("x-api-key", decrypted);
                    request.Headers.Add("anthropic-version", AnthropicVersion);
                }
            }

            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var models = new List<ModelInfo>();

            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString() ?? string.Empty;
                    models.Add(new ModelInfo(id, id, 200000));
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
