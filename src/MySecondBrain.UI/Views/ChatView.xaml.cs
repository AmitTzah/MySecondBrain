using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class ChatView : UserControl
{
    private ChatThreadViewModel? _viewModel;
    private bool _isAutoScrolling;

    public ChatView()
    {
        InitializeComponent();

        // Resolve ChatThreadViewModel from DI and set as DataContext
        _viewModel = App.ServiceProvider.GetRequiredService<ChatThreadViewModel>();
        DataContext = _viewModel;

        // Initialize the ViewModel (load default persona, populate list)
        Loaded += async (_, _) =>
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "ChatView initialization failed");
            }
        };

        // Register Ctrl+N for persona picker dialog
        var openPickerBinding = new KeyBinding
        {
            Key = Key.N,
            Modifiers = ModifierKeys.Control,
            Command = new RelayCommandAdapter(ShowPersonaPickerDialog)
        };
        InputBindings.Add(openPickerBinding);
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
