using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class OpenAIProvider : ILLMProvider
{
    private const string BaseUrl = "https://api.openai.com/v1/models";
    private const string ChatCompletionsPath = "/chat/completions";
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<OpenAIProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "OpenAI";

    public ProviderType Type => ProviderType.OpenAI;

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct) =>
        Task.FromResult<ChatResponse>(default!);

    /// <summary>
    /// Real streaming implementation using OpenAI's Chat Completions SSE API.
    /// Resolves the API key from the repository, POSTs to /v1/chat/completions
    /// with stream=true, and yields StreamChunk for each content delta.
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // ── 1. Resolve API key ──────────────────────────────────────
        var apiKey = await ResolveApiKeyAsync(request.ModelConfig);
        _logger.LogInformation(
            "{Provider}: API key {KeyStatus} for config {Config} (model={Model})",
            ProviderName, string.IsNullOrEmpty(apiKey) ? "MISSING" : "resolved",
            request.ModelConfig.DisplayName, request.ModelConfig.ModelIdentifier);

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("No API key available for {Provider} — cannot send chat request", ProviderName);
            yield return new StreamChunk(null, null, null, "error", null, true);
            yield break;
        }

        // ── 2. Build HTTP request ──────────────────────────────────
        var endpointBase = (request.ModelConfig.EndpointUrl ?? "https://api.openai.com/v1").TrimEnd('/');
        var url = $"{endpointBase}{ChatCompletionsPath}";
        _logger.LogInformation("{Provider}: sending stream to {Url}", ProviderName, url);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

        var requestBodyJson = BuildRequestBody(request);
        httpRequest.Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        var historyPath = ApiHistoryHelper.GetHistoryPath();

        _logger.LogDebug("Sending chat stream to {Provider} at {Url} (model={Model}, messages={MsgCount})",
            ProviderName, url, request.ModelConfig.ModelIdentifier, request.Messages.Count);

        // ── 3. Send with streaming response ────────────────────────
        HttpResponseMessage? response = null;
        StreamChunk? errorChunk = null;
        var responseContent = new StringBuilder();

        try
        {
            response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending chat request to {Provider} (status={Status})",
                ProviderName, ex.StatusCode);
            await ApiHistoryHelper.AppendEntryAsync(historyPath, url, requestBodyJson, $"HTTP {(int?)ex.StatusCode ?? 0}: {ex.Message}", "error", ct);
            errorChunk = new StreamChunk(
                null, null, null,
                ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "auth_error" : "network_error",
                null, true);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Chat request to {Provider} timed out or was cancelled", ProviderName);
            await ApiHistoryHelper.AppendEntryAsync(historyPath, url, requestBodyJson, "timeout", "timeout", ct);
            errorChunk = new StreamChunk(null, null, null, "timeout", null, true);
        }

        if (errorChunk is not null || response is null)
        {
            yield return errorChunk ?? new StreamChunk(null, null, null, "error", null, true);
            yield break;
        }

        // ── 4. Read SSE stream ─────────────────────────────────────
        using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(responseStream);

        var fullResponse = new StringBuilder();
        var chunkCount = 0;

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);

            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6);
            if (data == "[DONE]")
            {
                // Final marker from OpenAI — yield a terminal chunk
                await ApiHistoryHelper.AppendEntryAsync(historyPath, url, requestBodyJson, fullResponse.ToString(), "stop", ct);
                _logger.LogInformation(
                    "{Provider}: stream complete — {ChunkCount} chunks, {TotalLen} chars",
                    ProviderName, chunkCount, fullResponse.Length);
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

            chunkCount++;
            if (chunk.ContentDelta is not null)
                fullResponse.Append(chunk.ContentDelta);

            yield return chunk;
        }
    }

    public async Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

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
            var apiKeys = await _apiKeyRepo.GetByProviderAsync(ProviderType.OpenAI);
            var firstKey = apiKeys?.FirstOrDefault();
            if (firstKey != null)
            {
                var decrypted = _encryptionService.UnprotectString(firstKey.EncryptedValue);
                if (!string.IsNullOrEmpty(decrypted))
                {
                    request.Headers.Add("Authorization", $"Bearer {decrypted}");
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
                    models.Add(new ModelInfo(id, id, 128000));
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

    // ═══════════════════════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves the decrypted API key for the given model configuration.
    /// Tries ModelConfig.ApiKeyId first, then falls back to the first key
    /// registered for the OpenAI provider type.
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
        var keys = await _apiKeyRepo.GetByProviderAsync(ProviderType.OpenAI);
        var firstKey = keys?.FirstOrDefault();
        if (firstKey is not null)
        {
            var decrypted = _encryptionService.UnprotectString(firstKey.EncryptedValue);
            if (!string.IsNullOrEmpty(decrypted))
                return decrypted;
        }

        return null;
    }

    /// <summary>
    /// Builds the JSON request body for the OpenAI Chat Completions API.
    /// </summary>
    private static string BuildRequestBody(ChatRequest request)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("model", request.ModelConfig.ModelIdentifier);
        writer.WriteBoolean("stream", true);

        // Write messages array
        writer.WriteStartArray("messages");
        foreach (var msg in request.Messages)
        {
            writer.WriteStartObject();
            writer.WriteString("role", msg.Role);
            writer.WriteString("content", msg.Content);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // Optional parameters
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
