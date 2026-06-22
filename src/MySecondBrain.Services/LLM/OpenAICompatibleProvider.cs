using System.Net.Http;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class OpenAICompatibleProvider : ILLMProvider
{
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<OpenAICompatibleProvider> _logger;

    public OpenAICompatibleProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<OpenAICompatibleProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "OpenAI Compatible";

    public ProviderType Type => ProviderType.OpenAICompatible;

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct) =>
        Task.FromResult<ChatResponse>(default!);

#pragma warning disable CS1998, CS8425
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken ct)
#pragma warning restore CS1998, CS8425
    {
        yield break;
    }

    /// <summary>
    /// B6 spec: "No auto-fetch — user always enters manually". Returns empty list.
    /// </summary>
    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ModelInfo>>(Array.Empty<ModelInfo>());

    /// <summary>
    /// Validates API key using the stored CustomEndpointUrl from the repository.
    /// Falls back to http://localhost:1234 if no stored endpoint exists.
    /// Well-known endpoint resolution is handled by the caller (SettingsViewModel)
    /// which passes the correct endpointUrl parameter via the 3-param overload.
    /// </summary>
    public async Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct)
    {
        var apiKeys = await _apiKeyRepo.GetByProviderAsync(ProviderType.OpenAICompatible);
        var firstKey = apiKeys?.FirstOrDefault();
        var endpointUrl = firstKey?.CustomEndpointUrl ?? "http://localhost:1234";
        return await ValidateKeyInternalAsync(apiKey, endpointUrl, ct);
    }

    /// <summary>
    /// Validates API key against the specified endpoint URL.
    /// When endpointUrl is provided, it takes precedence over stored values.
    /// </summary>
    public async Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct, string? endpointUrl)
    {
        if (!string.IsNullOrEmpty(endpointUrl))
        {
            return await ValidateKeyInternalAsync(apiKey, endpointUrl, ct);
        }

        // Fall back to stored endpoint if no explicit URL provided
        return await ValidateKeyAsync(apiKey, ct);
    }

    private async Task<bool> ValidateKeyInternalAsync(string apiKey, string endpointUrl, CancellationToken ct)
    {
        try
        {
            var url = $"{endpointUrl.TrimEnd('/')}/models";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            using var response = await http.SendAsync(request, ct);
            var masked = ApiKeyHelper.MaskKey(apiKey);
            _logger.LogDebug("Key validation for {Provider}: HTTP {Status} (key: {Key}, url: {Url})",
                ProviderName, (int)response.StatusCode, masked, url);

            // Accept any non-401/403 response — local servers may return 200, 404, etc.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return false;
            }

            return true;
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
}
