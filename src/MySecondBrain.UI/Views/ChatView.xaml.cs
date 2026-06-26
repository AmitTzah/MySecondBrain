using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class ChatView : UserControl
{
    private ChatThreadViewModel? _viewModel;

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
