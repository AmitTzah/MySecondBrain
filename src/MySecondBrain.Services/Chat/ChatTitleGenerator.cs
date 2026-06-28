using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Generates a concise, AI-powered title for a chat conversation based on
/// the first user message and assistant response. Falls back to the first
/// 50 characters of the user message if the LLM call fails or returns
/// an invalid title.
/// </summary>
public class ChatTitleGenerator
{
    private readonly ILLMProviderService _llmService;
    private readonly ILogger<ChatTitleGenerator> _logger;

    public ChatTitleGenerator(
        ILLMProviderService llmService,
        ILogger<ChatTitleGenerator> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a 3-7 word title for a conversation.
    /// </summary>
    /// <param name="userMessage">The first user message content.</param>
    /// <param name="assistantResponse">The assistant's response content.</param>
    /// <param name="persona">The persona used for this conversation.</param>
    /// <param name="config">The model configuration to use for title generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A concise title (3-7 words), or a fallback string derived from the
    /// first 50 characters of the user message if generation fails.
    /// </returns>
    public async Task<string> GenerateTitleAsync(
        string userMessage,
        string assistantResponse,
        Persona persona,
        ModelConfiguration config,
        CancellationToken ct)
    {
        try
        {
            var prompt = $"Generate a concise 3-7 word title for this conversation.\n\nUser: {userMessage}\n\nAssistant: {assistantResponse}\n\nTitle:";

            // Use a lightweight non-streaming call for title generation (no history needed)
            var response = await _llmService.ChatAsync(
                new ChatThread { PersonaId = persona.Id, ModelConfigId = config.Id },
                prompt,
                persona,
                config,
                null,
                ct);

            // Guard: ChatAsync may return null if the provider is not yet implemented
            if (response is null)
                return FallbackTitle(userMessage);

            var title = response.Content.Trim().Trim('"', '\'', '“', '”', '‛', '„');

            if (string.IsNullOrEmpty(title) || title.Length > 100)
            {
                return FallbackTitle(userMessage);
            }

            return title;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Chat title generation failed, falling back to first 50 chars");
            return FallbackTitle(userMessage);
        }
    }

    /// <summary>
    /// Falls back to the first 50 characters of the user message as the title.
    /// </summary>
    private static string FallbackTitle(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return "New Chat";

        return userMessage.Length <= 50
            ? userMessage
            : userMessage[..50];
    }
}
