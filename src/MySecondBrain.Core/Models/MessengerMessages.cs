using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MySecondBrain.Core.Models;

/// <summary>
/// Sent when a generation completes in any chat tab.
/// The ChatThreadViewModel subscribes to this to set the green-dot alert
/// on inactive tabs.
/// </summary>
public sealed class GenerationCompletedMessage : ValueChangedMessage<string>
{
    public GenerationCompletedMessage(string threadId) : base(threadId) { }
}

/// <summary>
/// Sent when a new chat thread is created (e.g., for sidebar integration).
/// </summary>
public sealed class ChatThreadCreatedMessage : ValueChangedMessage<ChatThread>
{
    public ChatThreadCreatedMessage(ChatThread thread) : base(thread) { }
}

/// <summary>
/// Sent when a chat thread is deleted (e.g., for sidebar cleanup).
/// </summary>
public sealed class ChatThreadDeletedMessage : ValueChangedMessage<string>
{
    public ChatThreadDeletedMessage(string threadId) : base(threadId) { }
}

/// <summary>
/// Sent when a chat thread's title changes (e.g., after auto-titling).
/// </summary>
public sealed class ChatThreadTitleChangedMessage : ValueChangedMessage<string>
{
    public ChatThreadTitleChangedMessage(string threadId) : base(threadId) { }
}
