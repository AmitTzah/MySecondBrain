using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Encryption;

/// <summary>
/// Manages locking/unlocking chat content using AES-256-GCM encryption.
/// Uses <see cref="IChatEncryptionService"/> for the core crypto operations
/// and coordinates with repositories to persist encrypted state.
/// </summary>
public class LockedChatService
{
    private readonly IChatEncryptionService _encryption;
    private readonly IChatThreadRepository _threadRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<LockedChatService> _logger;

    public LockedChatService(
        IChatEncryptionService encryption,
        IChatThreadRepository threadRepo,
        IMessageRepository messageRepo,
        ILogger<LockedChatService> logger)
    {
        _encryption = encryption;
        _threadRepo = threadRepo;
        _messageRepo = messageRepo;
        _logger = logger;
    }

    public bool IsLocked(ChatThread thread) => thread.IsLocked;

    /// <summary>
    /// Locks a chat by encrypting all message content with the given password.
    /// Generates a unique per-chat salt and stores it on the thread.
    /// </summary>
    public async Task LockChatAsync(string threadId, string password, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var thread = await _threadRepo.GetByIdAsync(threadId)
            ?? throw new InvalidOperationException("Thread not found");

        if (thread.IsLocked)
        {
            _logger.LogWarning("Chat '{ThreadId}' is already locked", threadId);
            return;
        }

        var salt = _encryption.GenerateSalt();
        var messages = await _messageRepo.GetActiveBranchAsync(threadId);

        foreach (var msg in messages)
        {
            ct.ThrowIfCancellationRequested();
            var plaintext = Encoding.UTF8.GetBytes(msg.Content ?? string.Empty);
            var ciphertext = _encryption.Encrypt(plaintext, password, salt);
            msg.Content = Convert.ToBase64String(ciphertext);
            await _messageRepo.UpdateAsync(msg);
        }

        thread.IsLocked = true;
        thread.LockSalt = Convert.ToBase64String(salt);
        await _threadRepo.UpdateAsync(thread);

        _logger.LogInformation("Chat '{ThreadId}' locked ({MessageCount} messages encrypted)", threadId, messages.Count);
    }

    /// <summary>
    /// Unlocks a chat by decrypting all message content with the given password.
    /// Validates the password on the first message before decrypting all.
    /// </summary>
    public async Task UnlockChatAsync(string threadId, string password, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var thread = await _threadRepo.GetByIdAsync(threadId)
            ?? throw new InvalidOperationException("Thread not found");

        if (!thread.IsLocked)
        {
            _logger.LogWarning("Chat '{ThreadId}' is not locked", threadId);
            return;
        }

        if (string.IsNullOrEmpty(thread.LockSalt))
            throw new InvalidOperationException("Thread has no lock salt — data may be corrupted");

        var salt = Convert.FromBase64String(thread.LockSalt);
        var messages = await _messageRepo.GetActiveBranchAsync(threadId);

        if (messages.Count == 0)
        {
            // No messages to decrypt — just mark as unlocked
            thread.IsLocked = false;
            thread.LockSalt = null;
            await _threadRepo.UpdateAsync(thread);
            return;
        }

        // Validate password on the first message before decrypting all
        try
        {
            var firstContent = messages[0].Content
                ?? throw new InvalidOperationException("Message content is null — data may be corrupted.");
            var firstCiphertext = Convert.FromBase64String(firstContent);
            _encryption.Decrypt(firstCiphertext, password, salt);
        }
        catch (CryptographicException)
        {
            throw new UnauthorizedAccessException("Incorrect password.");
        }

        foreach (var msg in messages)
        {
            ct.ThrowIfCancellationRequested();

            var content = msg.Content
                ?? throw new InvalidOperationException("Message content is null — data may be corrupted.");
            var ciphertext = Convert.FromBase64String(content);
            var plaintext = _encryption.Decrypt(ciphertext, password, salt);
            msg.Content = Encoding.UTF8.GetString(plaintext);
            await _messageRepo.UpdateAsync(msg);
        }

        thread.IsLocked = false;
        thread.LockSalt = null;
        await _threadRepo.UpdateAsync(thread);

        _logger.LogInformation("Chat '{ThreadId}' unlocked ({MessageCount} messages decrypted)", threadId, messages.Count);
    }
}
