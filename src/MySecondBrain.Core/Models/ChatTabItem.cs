using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.Core.Models;

/// <summary>
/// Wraps a <see cref="ChatThread"/> with per-tab UI state for the tab control.
/// Each tab has its own message collection, streaming state, textbox content,
/// cursor/scroll position, persona, model config, and a completion alert indicator.
/// </summary>
public partial class ChatTabItem : ObservableObject
{
    public ChatThread Thread { get; }

    /// <summary>Messages loaded for this tab's active branch.</summary>
    [ObservableProperty]
    private ObservableCollection<Message> _messages = [];

    /// <summary>True while a response is being generated in this tab.</summary>
    [ObservableProperty]
    private bool _isStreaming;

    /// <summary>Current text in the message input box for this tab.</summary>
    [ObservableProperty]
    private string _textboxContent = string.Empty;

    /// <summary>Cursor position within the textbox (restored on tab switch).</summary>
    [ObservableProperty]
    private int _cursorPosition;

    /// <summary>Scroll offset of the conversation view (restored on tab switch).</summary>
    [ObservableProperty]
    private double _scrollOffset;

    /// <summary>
    /// True when a generation completed in this tab while it was not the active tab.
    /// The UI displays a pulsing green dot on the tab header.
    /// Reset to false when the tab becomes active.
    /// </summary>
    [ObservableProperty]
    private bool _hasCompletionAlert;

    /// <summary>
    /// The persona assigned to this tab. Each tab maintains its own persona
    /// so that switching tabs restores the correct assistant behavior.
    /// </summary>
    [ObservableProperty]
    private Persona? _activePersona;

    /// <summary>
    /// The model configuration resolved from <see cref="ActivePersona"/>'s
    /// <see cref="Persona.DefaultModelConfigId"/>. Stored per-tab so that
    /// each tab uses the model associated with its persona.
    /// </summary>
    [ObservableProperty]
    private ModelConfiguration? _activeModelConfig;

    /// <summary>Display title for the tab header.</summary>
    public string Title => Thread.Title ?? "New Chat";

    public ChatTabItem(ChatThread thread)
    {
        Thread = thread ?? throw new ArgumentNullException(nameof(thread));
    }
}
