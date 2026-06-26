using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Handles branch navigation, tree visualization, and message search.
/// </summary>
public class ChatBranchService
{
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<ChatBranchService> _logger;

    public ChatBranchService(
        IMessageRepository messageRepo,
        ILogger<ChatBranchService> logger)
    {
        _messageRepo = messageRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Message>> GetActiveBranchMessagesAsync(string threadId)
    {
        return await _messageRepo.GetActiveBranchAsync(threadId);
    }

    public async Task SetActiveBranchAsync(string messageId, string branchId)
    {
        await _messageRepo.SetActiveBranch(messageId, branchId);
    }

    public async Task<int> GetBranchCountAsync(string threadId)
    {
        return await _messageRepo.GetBranchCountAsync(threadId);
    }

    public async Task<ChatTree> GetChatTreeAsync(string threadId)
    {
        var allMessages = await _messageRepo.GetAllBranchesForThreadAsync(threadId);

        var nodes = allMessages.Select(m => new ChatTreeNode(
            m.Id,
            m.ParentMessageId,
            m.BranchId ?? string.Empty,
            m.IsActiveBranch,
            m.Role,
            m.Content.Length > 100 ? m.Content[..100] : m.Content
        )).ToList();

        return new ChatTree(threadId, nodes);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchMessagesAsync(string query, int maxResults)
    {
        var messages = await _messageRepo.SearchAsync(query, maxResults);
        return messages.Select(m => new SearchResult(
            m.Id,
            m.ThreadId,
            string.Empty, // thread title will be resolved by caller if needed
            m.Content.Length > 150 ? m.Content[..150] : m.Content,
            m.Role,
            m.CreatedAt
        )).ToList();
    }
}
