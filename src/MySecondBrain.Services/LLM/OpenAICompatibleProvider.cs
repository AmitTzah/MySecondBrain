using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class OpenAICompatibleProvider : ILLMProvider
{
    private const string ChatCompletionsPath = "/chat/completions";
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

    /// <summary>
    /// Real streaming implementation for OpenAI-compatible APIs (DeepSeek, Mistral,
    /// Ollama, LM Studio, etc.). Resolves endpoint URL from ModelConfig or stored
    /// key, supports optional auth (local servers may not require API keys).
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // ── 1. Resolve endpoint URL ─────────────────────────────────
        var endpointBase = await ResolveEndpointUrlAsync(request.ModelConfig);
        if (string.IsNullOrEmpty(endpointBase))
        {
            _logger.LogWarning("No endpoint URL configured for {Provider} — cannot send chat request", ProviderName);
            yield return new StreamChunk(null, null, null, "error", null, true);
            yield break;
        }

        var url = $"{endpointBase.TrimEnd('/')}{ChatCompletionsPath}";

        // ── 2. Resolve API key (optional for local servers) ─────────
        var apiKey = await ResolveApiKeyAsync(request.ModelConfig);

        // ── 3. Build HTTP request ──────────────────────────────────
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        if (!string.IsNullOrEmpty(apiKey))
        {
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        var requestBodyJson = BuildRequestBody(request);
        httpRequest.Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending chat stream to {Provider} at {Url} (model={Model}, messages={MsgCount}, auth={HasAuth})",
            ProviderName, url, request.ModelConfig.ModelIdentifier, request.Messages.Count, !string.IsNullOrEmpty(apiKey));

        // ── 4. Send with streaming response ────────────────────────
        HttpResponseMessage? response = null;
        StreamChunk? errorChunk = null;

        try
        {
            response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending chat request to {Provider} at {Url} (status={Status})",
                ProviderName, url, ex.StatusCode);
            errorChunk = new StreamChunk(
                null, null, null,
                ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "auth_error" : "network_error",
                null, true);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Chat request to {Provider} timed out or was cancelled", ProviderName);
            errorChunk = new StreamChunk(null, null, null, "timeout", null, true);
        }

        if (errorChunk is not null || response is null)
        {
            yield return errorChunk ?? new StreamChunk(null, null, null, "error", null, true);
            yield break;
        }

        // ── 5. Read SSE stream ─────────────────────────────────────
        using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(responseStream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);

            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6);
            if (data == "[DONE]")
            {
                yield return new StreamChunk(null, null, null, "stop", null, true);
                break;
            }

            StreamChunk chunk;
            try
            {
                chunk = ParseSseData(data);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse SSE data chunk from {Provider}", ProviderName);
                continue;
            }

            yield return chunk;
        }
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

    // ═══════════════════════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves the endpoint base URL from ModelConfig.EndpointUrl or the
    /// stored API key's CustomEndpointUrl. Falls back to http://localhost:1234.
    /// </summary>
    private async Task<string?> ResolveEndpointUrlAsync(ModelConfiguration config)
    {
        // ModelConfig takes highest priority
        if (!string.IsNullOrEmpty(config.EndpointUrl))
            return config.EndpointUrl;

        // Check stored key's custom endpoint
        var keys = await _apiKeyRepo.GetByProviderAsync(ProviderType.OpenAICompatible);
        var firstKey = keys?.FirstOrDefault();
        if (!string.IsNullOrEmpty(firstKey?.CustomEndpointUrl))
            return firstKey.CustomEndpointUrl;

        // Fallback for local servers
        return "http://localhost:1234";
    }

    /// <summary>
    /// Resolves the decrypted API key. Returns null for local servers
    /// that don't require authentication.
    /// </summary>
    private async Task<string?> ResolveApiKeyAsync(ModelConfiguration config)
    {
        // Try specific key by ID
        if (!string.IsNullOrEmpty(config.ApiKeyId))
        {
            var key = await _apiKeyRepo.GetByIdAsync(config.ApiKeyId);
            if (key is not null)
            {
                var decrypted = _encryptionService.UnprotectString(key.EncryptedValue);
                if (!string.IsNullOrEmpty(decrypted))
                    return decrypted;
            }
        }

        // Fall back to first key by provider type
        var keys = await _apiKeyRepo.GetByProviderAsync(ProviderType.OpenAICompatible);
        var firstKey = keys?.FirstOrDefault();
        if (firstKey is not null)
        {
            var decrypted = _encryptionService.UnprotectString(firstKey.EncryptedValue);
            return decrypted; // May be empty for local servers
        }

        return null;
    }

    /// <summary>
    /// Builds the JSON request body for the OpenAI-compatible Chat Completions API.
    /// </summary>
    private static string BuildRequestBody(ChatRequest request)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("model", request.ModelConfig.ModelIdentifier);
        writer.WriteBoolean("stream", true);

        writer.WriteStartArray("messages");
        foreach (var msg in request.Messages)
        {
            writer.WriteStartObject();
            writer.WriteString("role", msg.Role);
            writer.WriteString("content", msg.Content);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        if (request.ModelConfig.Temperature >= 0)
            writer.WriteNumber("temperature", request.ModelConfig.Temperature);

        if (request.ModelConfig.MaxOutputTokens > 0)
            writer.WriteNumber("max_tokens", request.ModelConfig.MaxOutputTokens);

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Parses a single SSE data line into a StreamChunk.
    /// </summary>
    private static StreamChunk ParseSseData(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? contentDelta = null;
        string? finishReason = null;
        UsageInfo? usage = null;
        bool isFinal = false;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];

            if (choice.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content))
            {
                contentDelta = content.GetString();
            }

            if (choice.TryGetProperty("finish_reason", out var fr)
                && fr.ValueKind == JsonValueKind.String)
            {
                finishReason = fr.GetString();
                if (finishReason is not null)
                    isFinal = true;
            }
        }

        if (root.TryGetProperty("usage", out var usageEl))
        {
            var prompt = usageEl.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            var completion = usageEl.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0;
            var total = usageEl.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;
            usage = new UsageInfo(prompt, completion, total);
            isFinal = true;
        }

        return new StreamChunk(contentDelta, null, null, finishReason, usage, isFinal);
    }

}
