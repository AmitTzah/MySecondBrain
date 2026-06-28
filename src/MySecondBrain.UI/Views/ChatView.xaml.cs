using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MdXaml;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;
using Serilog;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class ChatView : UserControl
{
    private ChatThreadViewModel? _viewModel;
    private static int s_instanceCounter;
    private readonly int _instanceId = Interlocked.Increment(ref s_instanceCounter);

    /// <summary>
    /// True when the user is at or very near the bottom of the scroll viewer.
    /// When true and content grows during streaming, the view auto-scrolls to
    /// follow new content. Set false when the user scrolls up (any amount);
    /// set true when they scroll back to the bottom or click the scroll-to-bottom
    /// button. This single flag replaces the old _isAutoScrolling / _userTookControl
    /// pair, which created a priority race between the deferred Background scroll
    /// and the Render-priority streaming timer.
    /// </summary>
    private bool _isPinnedToBottom = true;

    // ═══ Streaming rendering fix ════════════════════════════════════════
    /// <summary>Adaptive DispatcherTimer that finds the last ListBox item's
    /// <c>MarkdownScrollViewer</c> and sets its <c>Markdown</c> property directly
    /// to the accumulated streaming content, bypassing the binding system.
    /// Interval adjusts per render: 80ms under 2K, 200ms 2K-10K, 400ms over 10K.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _streamingTimer;

    /// <summary>Tracks the last rendered content to avoid redundant updates.</summary>
    private string _lastStreamedContent = string.Empty;

    public ChatView()
    {
        InitializeComponent();

        // Resolve ChatThreadViewModel from DI and set as DataContext
        _viewModel = App.ServiceProvider.GetRequiredService<ChatThreadViewModel>();
        DataContext = _viewModel;

        // ═══ Streaming timer setup (BEFORE Unloaded handler that references it) ═══
        // 80ms (adaptive) DispatcherTimer at Render priority. Timer callback finds
        // the last ListBox item's MarkdownScrollViewer and sets its Markdown
        // property directly to StreamingContent, bypassing the binding.
        // The streaming message was added to the ObservableCollection ONCE by
        // OnStreamChunkReceived — no RemoveAt/Insert during streaming.
        // Adaptive debounce: initial 80ms; adjusted per render in OnStreamingTimerTick
        // based on content length (80ms/<2K, 200ms/2K-10K, 400ms/>10K).
        _streamingTimer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(80),
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
    ///
    /// Implements three streaming optimizations:
    /// 1. <b>Adaptive debounce</b> — adjusts timer interval based on content length
    ///    (80ms under 2K, 200ms 2K-10K, 400ms over 10K).
    /// 2. <b>Streaming truncation</b> — when auto-scrolling (user at bottom), only
    ///    renders the last 4000 chars to reduce MdXaml layout cost; renders full
    ///    content when user has scrolled up to read earlier parts.
    /// 3. <b>Adaptive timer interval</b> — updates <c>_streamingTimer.Interval</c>
    ///    after each render using the same length-based thresholds as #1.
    /// </summary>
    private void OnStreamingTimerTick(object? sender, EventArgs e)
    {
        if (_viewModel is null) return;

        var content = _viewModel.StreamingContent;

        // Skip if nothing changed or streaming stopped with empty content
        if (string.IsNullOrEmpty(content) || content == _lastStreamedContent)
            return;

        _lastStreamedContent = content;

        // ═══ Streaming truncation ════════════════════════════════════════
        // When user is auto-scrolling (at bottom), only pass the last ~4000
        // characters to reduce MdXaml layout cost, aligned to line boundaries
        // so markdown syntax elements (fences, headers) are never split.
        // When user has scrolled up, pass the full content so they can read
        // earlier parts.
        // _lastStreamedContent intentionally tracks the FULL content so any
        // addition anywhere (not just within the tail) triggers a re-render.
        string renderContent;
        if (_viewModel.IsScrolledUp)
            renderContent = content;          // user reading earlier parts — render full
        else if (content.Length > 4000)
        {
            // Find a line boundary within 100 chars before the cut point
            // so we never split markdown syntax elements mid-line.
            var rawStart = content.Length - 4000;
            var tailStart = rawStart;

            // Walk back up to 100 chars to find the preceding newline
            var searchFrom = Math.Max(0, rawStart - 100);
            var lastNewline = content.LastIndexOf('\n', rawStart, rawStart - searchFrom);
            if (lastNewline >= 0)
                tailStart = lastNewline + 1; // start after the newline

            // Avoid splitting a UTF-16 surrogate pair
            if (char.IsLowSurrogate(content[tailStart]))
                tailStart++;

            renderContent = content.Substring(tailStart);
        }
        else
            renderContent = content;

        // Find the ListBox inside the MessageScrollViewer
        var listBox = MessageScrollViewer?.Content as System.Windows.Controls.ListBox;
        if (listBox is null || listBox.Items.Count == 0) return;

        // Find the last item's container and its MarkdownScrollViewer.
        // ── ContainerFromItem race-condition guard ──
        // After tab.Messages is replaced (Stop → load from DB → new
        // ObservableCollection), the VirtualizingStackPanel's
        // ItemContainerGenerator is reset. When the next streaming
        // assistant message is added to the new collection, the
        // container may not be generated yet (layout runs at the same
        // Render priority as this timer). ContainerFromItem returns
        // null in that case — force a layout update and retry once.
        // Additionally, with Recycling mode, ContainerFromItem can
        // return a recycled container that was previously prepared for
        // a different item (e.g., the User message at the previous
        // last position). Validate DataContext to guard against this.
        var lastItem = listBox.Items[^1];
        var lastContainer = listBox.ItemContainerGenerator.ContainerFromItem(lastItem) as FrameworkElement;

        // If the container isn't realized yet, force a layout pass and retry
        if (lastContainer is null)
        {
            listBox.UpdateLayout();
            lastContainer = listBox.ItemContainerGenerator.ContainerFromItem(lastItem) as FrameworkElement;
        }

        if (lastContainer is null)
        {
            Log.Debug("[StreamDiag] Timer: ContainerFromItem returned null for last item (Role={Role})",
                (lastItem as MySecondBrain.Core.Models.Message)?.Role ?? "?");
            return;
        }

        // ── Recycled-container guard ──
        // When the VirtualizingStackPanel recycles a container that was
        // previously used for a different item, the DataContext may not
        // yet reflect the new item. Skip this tick and wait for the
        // layout pass to properly prepare the container.
        if (!ReferenceEquals(lastContainer.DataContext, lastItem))
        {
            Log.Debug("[StreamDiag] Timer: Container DataContext mismatch — skipping tick. Expected={ExpectedRole}, Actual={ActualRole}",
                (lastItem as MySecondBrain.Core.Models.Message)?.Role ?? "?",
                (lastContainer.DataContext as MySecondBrain.Core.Models.Message)?.Role ?? "?");
            return;
        }

        // Defer the entire FindVisualChild + Markdown set to Background priority.
        // At Render priority (timer callback), a recycled container may still
        // have the User template applied. The layout pass (Normal priority)
        // applies the correct template, and Background runs after that.
        // Re-finding the MarkdownScrollViewer at Background priority guarantees
        // we get the correctly-templated visual element.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var mv = FindVisualChild<MarkdownScrollViewer>(lastContainer);
            if (mv is null)
                return;
            mv.Markdown = renderContent;
        }), System.Windows.Threading.DispatcherPriority.Background);

        // ═══ Adaptive timer interval ════════════════════════════════════
        // Adjust the timer interval based on content length. The more content
        // already rendered, the slower we tick to avoid layout thrashing.
        if (renderContent.Length < 2000)
            _streamingTimer.Interval = TimeSpan.FromMilliseconds(80);
        else if (renderContent.Length < 10000)
            _streamingTimer.Interval = TimeSpan.FromMilliseconds(200);
        else
            _streamingTimer.Interval = TimeSpan.FromMilliseconds(400);

        Log.Debug("[StreamDiag] Timer set MarkdownScrollViewer.Markdown. RenderContentLen={Len}, FullContentLen={FullLen}, Interval={Interval}ms",
            renderContent.Length, content.Length, _streamingTimer.Interval.TotalMilliseconds);
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

        // When streaming content arrives, reset the timer to fire immediately
        // on the next render pass. This prevents the user seeing a recycled
        // container template (from VirtualizingStackPanel) before the correct
        // Markdown is rendered. After render, the adaptive interval takes over.
        // Skip when content is being cleared (final chunk / Stop) — the
        // subsequent collection replacement in SendWithStreamingAsync will
        // handle rendering the persisted message; clearing the Markdown
        // early would cause a visible flash.
        if (e.PropertyName == nameof(ChatThreadViewModel.StreamingContent)
            && _viewModel is not null)
        {
            if (!string.IsNullOrEmpty(_viewModel.StreamingContent))
            {
                // New streaming content arrived — fire timer immediately so the
                // first chunk renders ASAP, reducing the window where a recycled
                // container (from VirtualizingStackPanel) might show stale state.
                _streamingTimer.Interval = TimeSpan.FromMilliseconds(1);
            }
            else
            {
                // Streaming content was cleared (Stop / final chunk / new send).
                // Reset the last-rendered-content tracker so the next streaming
                // session starts from a clean state. Without this, _lastStreamedContent
                // retains the previous session's full text, and if the new session's
                // first chunk coincidentally matches the tail of that old text, the
                // timer's early-return guard would skip rendering entirely.
                _lastStreamedContent = string.Empty;
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
    /// outer MessageScrollViewer. Scrolls immediately — no BeginInvoke deferral.
    ///
    /// The previous implementation deferred the actual ScrollToVerticalOffset to
    /// Background priority, creating a priority race with the Render-priority
    /// streaming timer: the _userTookControl flag was set but the scroll hadn't
    /// executed yet, so the next ScrollChanged (triggered by timer content growth)
    /// saw the old scroll position (still at bottom) and cleared the flag,
    /// causing snap-back on the next timer tick.
    ///
    /// OnStreamChunkReceived no longer performs RemoveAt/Insert during streaming
    /// (it updates message Content in-place), so the original reason for deferral
    /// no longer applies.
    /// </summary>
    private void OnMessagePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mark handled immediately to prevent FlowDocumentScrollViewer's
        // internal ScrollViewer from capturing the wheel event.
        e.Handled = true;

        if (MessageScrollViewer is null) return;
        var sv = MessageScrollViewer;

        // Scroll immediately — no BeginInvoke. Delta is typically ±120 per notch.
        // Divide by 3 to get ~40px per tick for a natural scroll feel.
        var newOffset = sv.VerticalOffset - (e.Delta / 3.0);
        newOffset = Math.Max(0, Math.Min(newOffset, sv.ScrollableHeight));
        sv.ScrollToVerticalOffset(newOffset);

        // Check if the user scrolled away from the bottom. Use a tight 2px
        // threshold so even a single tick up immediately unpins auto-scroll.
        // The old 15px threshold was too loose and required multiple ticks.
        var isAtBottom = Math.Abs(sv.ScrollableHeight - newOffset) < 2.0;
        if (!isAtBottom)
        {
            _isPinnedToBottom = false;
            UpdateAutoScrollIndicator(showPaused: true);
        }
        // Note: re-pinning (isAtBottom → _isPinnedToBottom = true) happens in
        // ScrollChanged when it detects a user-initiated scroll-down (e.VerticalChange > 0).
    }

    /// <summary>
    /// Handles ScrollChanged on the message ListBox/ScrollViewer.
    ///
    /// Three responsibilities:
    /// 1. <b>Auto-scroll</b>: When content grows (ExtentHeightChange > 0) during
    ///    streaming AND the user is pinned to bottom, scroll to bottom.
    /// 2. <b>Unpin detection</b>: When the user scrolls up (VerticalChange negative)
    ///    and is no longer at bottom, unpin and show the "Auto-scroll paused" indicator.
    /// 3. <b>Re-pin detection</b>: When the user manually scrolls to bottom
    ///    (VerticalChange > 0 and isAtBottom), re-pin and hide the indicator.
    ///
    /// Uses e.VerticalChange to distinguish user-initiated scrolls from
    /// content-growth-induced position changes, preventing the old bug where
    /// content growth would clear the user-control flag.
    /// </summary>
    private void MessageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        // Tight 2px threshold: a single mouse-wheel tick (~13px) reliably
        // escapes this. The old 15px threshold required 2+ ticks and felt laggy.
        var isAtBottom = Math.Abs(sv.ScrollableHeight - sv.VerticalOffset) < 2.0;
        var isStreaming = _viewModel?.ActiveTab?.IsStreaming ?? false;

        // ── User manually scrolled to bottom → re-pin ──
        // e.VerticalChange > 0 means the offset increased (scrolling down),
        // caused by user input, not by content growth. Content growth changes
        // ExtentHeight, not VerticalOffset (until we programmatically scroll).
        if (isAtBottom && e.VerticalChange > 0)
        {
            _isPinnedToBottom = true;
            UpdateAutoScrollIndicator(showPaused: false);
        }

        // ── User scrolled up → unpin immediately ──
        // e.VerticalChange < 0: offset decreased, user scrolled up.
        // We check !isAtBottom to avoid unpinning on tiny bounces near bottom.
        if (!isAtBottom && e.VerticalChange < 0)
        {
            _isPinnedToBottom = false;
            UpdateAutoScrollIndicator(showPaused: true);
        }

        // ── Auto-scroll: content grew from streaming, user is pinned ──
        // Only triggered by ExtentHeightChange > 0 (content height increased),
        // never by user scroll events. This prevents fighting the user.
        if (_isPinnedToBottom && isStreaming && e.ExtentHeightChange > 0)
        {
            sv.ScrollToBottom();
        }
    }

    /// <summary>
    /// Updates the ViewModel scroll-state properties and the paused-indicator
    /// visibility in a single helper to avoid duplication.
    /// </summary>
    private void UpdateAutoScrollIndicator(bool showPaused)
    {
        if (_viewModel is not null)
        {
            _viewModel.IsScrolledUp = showPaused;
            _viewModel.AutoScrollIndicatorText = showPaused
                ? "Auto-scroll paused"
                : string.Empty;
        }

        if (AutoScrollPausedIndicator is not null)
        {
            AutoScrollPausedIndicator.Visibility = showPaused
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Smooth-scrolls the message ScrollViewer to the bottom with a 200ms
    /// ease-out animation. Re-pins auto-scroll and hides the paused indicator.
    /// Called by the ScrollToBottom floating button.
    /// </summary>
    private void ScrollToBottom_Click(object sender, RoutedEventArgs e)
    {
        if (MessageScrollViewer is null) return;

        var sv = MessageScrollViewer;
        if (sv.ScrollableHeight <= 0) return;

        // Re-pin and hide indicator
        _isPinnedToBottom = true;
        UpdateAutoScrollIndicator(showPaused: false);

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

    private void OnCopyMdClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        var message = (sender as FrameworkElement)?.DataContext as MySecondBrain.Core.Models.Message;
        _viewModel.CopyMdCommand.Execute(message);
        ShowCopyFeedback(sender as System.Windows.Controls.Button, "📋 MD");
    }

    private void OnCopyRichClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        var message = (sender as FrameworkElement)?.DataContext as MySecondBrain.Core.Models.Message;
        _viewModel.CopyRichCommand.Execute(message);
        ShowCopyFeedback(sender as System.Windows.Controls.Button, "📄 Rich");
    }

    /// <summary>
    /// Temporarily changes the button content to ✅ for 1.5 seconds as visual
    /// confirmation that the copy operation succeeded, then restores the original text.
    /// </summary>
    private static void ShowCopyFeedback(System.Windows.Controls.Button? button, string originalContent)
    {
        if (button is null) return;
        button.Content = "✅";
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        timer.Tick += (_, _) =>
        {
            button.Content = originalContent;
            timer.Stop();
        };
        timer.Start();
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
