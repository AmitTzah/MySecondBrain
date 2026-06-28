using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MdXaml;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;
using Serilog;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class ChatView : UserControl
{
    private ChatThreadViewModel? _viewModel;
    private bool _isAutoScrolling;
    private IThemeProvider? _themeProvider;
    private static int s_instanceCounter;
    private readonly int _instanceId = Interlocked.Increment(ref s_instanceCounter);

    // ═══ Streaming rendering fix ════════════════════════════════════════
    /// <summary>50ms DispatcherTimer that finds the last ListBox item's
    /// <c>MarkdownScrollViewer</c> and sets its <c>Markdown</c> property directly
    /// to the accumulated streaming content, bypassing the binding system.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _streamingTimer;

    /// <summary>Tracks the last rendered content to avoid redundant updates.</summary>
    private string _lastStreamedContent = string.Empty;

    public ChatView()
    {
        InitializeComponent();

        // Resolve ChatThreadViewModel from DI and set as DataContext
        _viewModel = App.ServiceProvider.GetRequiredService<ChatThreadViewModel>();
        _themeProvider = App.ServiceProvider.GetService<IThemeProvider>();
        DataContext = _viewModel;

        Log.Debug("[ThemeDiag] ChatView #{InstanceId} constructed, DataContext={DC}, current global theme={Theme}",
            _instanceId, DataContext?.GetType().Name, _themeProvider?.CurrentChatTheme);

        // ═══ Streaming timer setup (BEFORE Unloaded handler that references it) ═══
        // 50ms DispatcherTimer at Render priority. Timer callback finds the
        // last ListBox item's MarkdownScrollViewer and sets its Markdown
        // property directly to StreamingContent, bypassing the binding.
        // The streaming message was added to the ObservableCollection ONCE by
        // OnStreamChunkReceived — no RemoveAt/Insert during streaming.
        _streamingTimer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(50),
            System.Windows.Threading.DispatcherPriority.Render,
            OnStreamingTimerTick,
            System.Windows.Application.Current?.Dispatcher
                ?? System.Windows.Threading.Dispatcher.CurrentDispatcher);

        // Per-tab theme handling: subscribe to ViewModel property changes
        // instead of the global IThemeProvider.ChatThemeChanged event.
        // Multiple ChatView instances exist (one for tab content, one for
        // screen navigation template) and the global event would fire to ALL
        // of them, causing theme "duplication" across tabs.
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Unloaded += (_, _) =>
            {
                Log.Debug("[ThemeDiag] ChatView #{InstanceId} unloading, unsubscribing from ViewModel.PropertyChanged",
                    _instanceId);
                if (_viewModel is not null)
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

                // Stop streaming timer on unload
                _streamingTimer.Stop();
            };

            // Theme is applied via PropertyChanged when InitializeAsync seeds
            // CurrentChatVisualTheme from the global preference (or when the
            // user changes it via the ComboBox). Do NOT call ApplyChatTheme
            // in the constructor — it resets ListBox.ItemsSource before WPF
            // bindings are fully active, causing messages to not appear.
        }

        // Initialize the ViewModel (load default persona, populate list)
        Loaded += async (_, _) =>
        {
            try
            {
                Log.Debug("[MsgDiag] ChatView #{InstanceId} Loaded: ActiveTab={ActiveTab}, ChatTabs.Count={Count}, Messages.Count={MsgCount}",
                    _instanceId,
                    _viewModel?.ActiveTab?.Title ?? "(null)",
                    _viewModel?.ChatTabs.Count ?? 0,
                    _viewModel?.ActiveTab?.Messages?.Count ?? -1);

                if (_viewModel is not null)
                    await _viewModel.InitializeAsync();

                Log.Debug("[MsgDiag] ChatView #{InstanceId} after InitializeAsync: ActiveTab={ActiveTab}, ChatTabs.Count={Count}, Messages.Count={MsgCount}",
                    _instanceId,
                    _viewModel?.ActiveTab?.Title ?? "(null)",
                    _viewModel?.ChatTabs.Count ?? 0,
                    _viewModel?.ActiveTab?.Messages?.Count ?? -1);

                // Check ListBox state
                var lb = MessageScrollViewer?.Content as System.Windows.Controls.ListBox;
                Log.Debug("[MsgDiag] ChatView #{InstanceId} ListBox: hasItemsSource={HasSource}, itemCount={ItemCount}",
                    _instanceId,
                    lb?.ItemsSource != null,
                    lb?.Items.Count ?? -1);

                // Start the streaming rendering timer (runs continuously — cheap when idle)
                if (!_streamingTimer.IsEnabled)
                    _streamingTimer.Start();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "ChatView initialization failed");
            }
        };

        // Wire the ScrollToBottomButton to the MessageScrollViewer\r\n        if (ScrollToBottomBtn is not null)\r\n            ScrollToBottomBtn.TargetScrollViewer = MessageScrollViewer;\r\n\r\n        // Register Ctrl+N to create a new chat (always — persona picker is a separate action)
        var newChatBinding = new KeyBinding
        {
            Key = Key.N,
            Modifiers = ModifierKeys.Control,
            Command = new RelayCommandAdapter(() => _viewModel?.NewChatCommand.Execute(null))
        };
        InputBindings.Add(newChatBinding);

    }

    /// <summary>
    /// Timer callback: finds the last item in the ListBox and sets its
    /// <c>MarkdownScrollViewer.Markdown</c> property directly to the streaming
    /// content, bypassing the WPF binding system. The message was added to the
    /// ObservableCollection ONCE by <c>OnStreamChunkReceived</c>.
    /// </summary>
    private void OnStreamingTimerTick(object? sender, EventArgs e)
    {
        if (_viewModel is null) return;

        var content = _viewModel.StreamingContent;

        // Skip if nothing changed or streaming stopped with empty content
        if (string.IsNullOrEmpty(content) || content == _lastStreamedContent)
            return;

        _lastStreamedContent = content;

        // Find the ListBox inside the MessageScrollViewer
        var listBox = MessageScrollViewer?.Content as System.Windows.Controls.ListBox;
        if (listBox is null || listBox.Items.Count == 0) return;

        // Find the last item's container and its MarkdownScrollViewer
        var lastItem = listBox.Items[^1];
        var lastContainer = listBox.ItemContainerGenerator.ContainerFromItem(lastItem) as FrameworkElement;
        if (lastContainer is null) return;

        var markdownViewer = FindVisualChild<MarkdownScrollViewer>(lastContainer);
        if (markdownViewer is null)
        {
            Log.Debug("[StreamDiag] Timer: MarkdownScrollViewer not found in last container");
            return;
        }

        // Set the Markdown property directly — MarkdownScrollViewer renders it
        // internally using MdXaml, no converter or binding involved
        markdownViewer.Markdown = content;

        Log.Debug("[StreamDiag] Timer set MarkdownScrollViewer.Markdown. ContentLen={Len}", content.Length);
    }

    /// <summary>
    /// Listens for ViewModel property changes. When CurrentChatVisualTheme changes,
    /// swaps message templates for this ChatView instance only (not globally).
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatThreadViewModel.CurrentChatVisualTheme) && _viewModel is not null)
        {
            Log.Debug("[ThemeDiag] ChatView #{InstanceId} detected CurrentChatVisualTheme change to {Theme}",
                _instanceId, _viewModel.CurrentChatVisualTheme);
            ApplyChatTheme(_viewModel.CurrentChatVisualTheme);
        }

        // Also log ActiveTab changes for diagnostics
        if (e.PropertyName == nameof(ChatThreadViewModel.ActiveTab) && _viewModel is not null)
        {
            Log.Debug("[MsgDiag] ChatView #{InstanceId} ActiveTab changed: Title={Title}, Messages.Count={MsgCount}",
                _instanceId,
                _viewModel.ActiveTab?.Title ?? "(null)",
                _viewModel.ActiveTab?.Messages?.Count ?? -1);
        }

        // Refresh message list when font size changes so the converter re-runs
        // with the new font size from IThemeProvider.
        if (e.PropertyName == nameof(ChatThreadViewModel.FontSizeVersion) && _viewModel is not null)
        {
            Log.Debug("[FontDiag] ChatView #{InstanceId} font size changed to {FontSize}, refreshing messages",
                _instanceId, _themeProvider?.FontSize);

            var listBox = MessageScrollViewer?.Content as System.Windows.Controls.ListBox;
            if (listBox is not null)
            {
                var bindingExpr = listBox.GetBindingExpression(System.Windows.Controls.ItemsControl.ItemsSourceProperty);
                if (bindingExpr?.ParentBindingBase is { } binding)
                {
                    listBox.ItemsSource = null;
                    listBox.SetBinding(System.Windows.Controls.ItemsControl.ItemsSourceProperty, binding);
                }
            }
        }
    }

    /// <summary>
    /// Applies the given chat theme to this ChatView's message templates.
    /// Dynamically swaps the User and Assistant message templates in the
    /// MessageDataTemplateSelector and refreshes the ListBox items.
    /// </summary>
    private void ApplyChatTheme(ChatTheme newTheme)
    {
        Log.Debug("[ThemeDiag] ChatView #{InstanceId} applying theme: {NewTheme}",
            _instanceId, newTheme);

        if (_themeProvider is null) return;

        var selector = Resources["MessageTemplateSelector"] as MessageDataTemplateSelector;
        if (selector is null) return;

        if (newTheme == ChatTheme.Classic)
        {
            // Restore the inline rich templates (default)
            selector.UserMessageTemplate = Resources["UserMessageTemplate"] as DataTemplate;
            selector.AssistantMessageTemplate = Resources["AssistantMessageTemplate"] as DataTemplate;
            selector.SystemMessageTemplate = Resources["SystemMessageTemplate"] as DataTemplate;
        }
        else
        {
            // Use role-specific theme templates from the global resource dictionary
            var suffix = newTheme switch
            {
                ChatTheme.Compact => "Compact",
                ChatTheme.Bubble => "Bubble",
                _ => "Classic"
            };

            // Use fully-qualified Application to avoid ambiguity with System.Windows.Forms.Application
            var app = System.Windows.Application.Current;
            var userTpl = app.Resources[$"{suffix}UserTemplate"] as DataTemplate;
            var asstTpl = app.Resources[$"{suffix}AssistantTemplate"] as DataTemplate;

            if (userTpl is not null)
            {
                selector.UserMessageTemplate = userTpl;
                selector.AssistantMessageTemplate = asstTpl;
                selector.SystemMessageTemplate = asstTpl;
            }
        }

        // Force the ListBox to re-evaluate its ItemTemplateSelector.
        // Must preserve the binding — setting ItemsSource=null and back to the
        // raw Binding object (not the evaluated value) keeps the binding alive.
        var listBox = MessageScrollViewer?.Content as System.Windows.Controls.ListBox;
        if (listBox is not null)
        {
            var bindingExpr = listBox.GetBindingExpression(System.Windows.Controls.ItemsControl.ItemsSourceProperty);
            if (bindingExpr?.ParentBindingBase is { } binding)
            {
                listBox.ItemsSource = null;
                listBox.SetBinding(System.Windows.Controls.ItemsControl.ItemsSourceProperty, binding);
            }
        }
    }

    /// <summary>
    /// Toggles all skills on/off in the Skills dropdown.
    /// </summary>
    private void ToggleAllSkills_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        // Determine the new state: if any skill is off, turn all on; otherwise turn all off.
        var anyDisabled = _viewModel.SkillToggles.Any(s => !s.IsEnabled);
        _viewModel.SetAllSkillsEnabled(anyDisabled);
    }

    private void ShowPersonaPickerDialog()
    {
        if (_viewModel is null)
            return;

        // Prepare the filtered list
        _viewModel.PreparePersonaPickerCommand.Execute(null);

        var dialog = new PersonaPickerDialog(_viewModel)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            // ActivePersona is already set via binding; OnActivePersonaChanged handles side effects
        }
    }

    // ================================================================
    // Auto-scroll management
    // ================================================================

    /// <summary>
    /// Forwards mouse wheel events from any FlowDocumentScrollViewer child
    /// (which would otherwise capture them for internal scrolling) to the
    /// outer MessageScrollViewer. The scroll is deferred via BeginInvoke at
    /// Background priority so it executes after all pending data-binding and
    /// layout updates — preventing RemoveAt/Insert layout passes in
    /// OnStreamChunkReceived from overriding the user's scroll position.
    /// </summary>
    private void OnMessagePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mark handled immediately to prevent FlowDocumentScrollViewer's
        // internal ScrollViewer from capturing the wheel event.
        e.Handled = true;

        if (MessageScrollViewer is null) return;

        var delta = e.Delta;
        var sv = MessageScrollViewer;

        // Defer to Background priority — runs after DataBind, Render, Loaded,
        // and all Normal-priority operations (including Dispatcher.Invoke from
        // OnStreamChunkReceived). This ensures ScrollableHeight is accurate
        // and no pending RemoveAt/Insert will override the offset.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Delta is typically ±120 per notch. Divide to get ~40px per tick.
            var newOffset = sv.VerticalOffset - (delta / 3.0);
            newOffset = Math.Max(0, Math.Min(newOffset, sv.ScrollableHeight));
            sv.ScrollToVerticalOffset(newOffset);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Handles ScrollChanged on the message ListBox/ScrollViewer.
    /// When the user scrolls up during streaming, pauses auto-scroll
    /// and shows a "Auto-scroll paused" indicator.
    /// </summary>
    private void MessageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        var isAtBottom = sv.VerticalOffset >= sv.ScrollableHeight - 15;
        var isStreaming = _viewModel?.ActiveTab?.IsStreaming ?? false;

        // Auto-scroll when the user is at the bottom or generation is not happening
        _isAutoScrolling = isAtBottom || !isStreaming;

        // Update ViewModel state for UI bindings
        if (_viewModel is not null)
        {
            _viewModel.IsScrolledUp = !isAtBottom && isStreaming;
            _viewModel.AutoScrollIndicatorText = _viewModel.IsScrolledUp
                ? "Auto-scroll paused"
                : string.Empty;
        }

        // Ensure the AuroScrollPausedIndicator visibility
        if (AutoScrollPausedIndicator is not null)
        {
            AutoScrollPausedIndicator.Visibility = _viewModel?.IsScrolledUp == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Smooth-scrolls the message ScrollViewer to the bottom.
    /// Called by the ScrollToBottom floating button.
    /// </summary>
    private void ScrollToBottom_Click(object sender, RoutedEventArgs e)
    {
        if (MessageScrollViewer is null) return;

        var sv = MessageScrollViewer;
        if (sv.ScrollableHeight <= 0) return;

        var startOffset = sv.VerticalOffset;
        var targetOffset = sv.ScrollableHeight;

        // Smooth animation using DoubleAnimation on VerticalOffset
        var anim = new DoubleAnimation
        {
            From = startOffset,
            To = targetOffset,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(anim, sv);
        Storyboard.SetTargetProperty(anim, new PropertyPath("(ScrollViewer.VerticalOffset)"));
        storyboard.Children.Add(anim);

        // Hide the indicator when user clicks scroll-to-bottom
        if (_viewModel is not null)
        {
            _viewModel.IsScrolledUp = false;
            _viewModel.AutoScrollIndicatorText = string.Empty;
        }

        storyboard.Begin();
    }

    /// <summary>
    /// Recursively searches the visual tree for a child of type <typeparamref name="T"/>.
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
                return typed;
            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }
        return null;
    }
}

/// <summary>
/// Simple adapter to use a lambda as an ICommand for KeyBinding.
/// </summary>
public class RelayCommandAdapter : ICommand
{
    private readonly Action _execute;

    public RelayCommandAdapter(Action execute)
    {
        _execute = execute;
    }

#pragma warning disable CS0067 // KeyBinding doesn't subscribe to CanExecuteChanged
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
