using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class ChatView : UserControl
{
    private ChatThreadViewModel? _viewModel;

    public ObservableCollection<SampleMessage> SampleMessages { get; } =
    [
        new() { Role = "User", Content = "Explain what dependency injection is in .NET.", Timestamp = "10:32 AM" },
        new() { Role = "Assistant", Content = "Dependency Injection (DI) is a design pattern where objects receive their dependencies from an external source rather than creating them internally. In .NET, the built-in DI container in Microsoft.Extensions.DependencyInjection manages service lifetimes and resolves dependencies automatically.", Timestamp = "10:32 AM" },
        new() { Role = "User", Content = "Can you show a code example?", Timestamp = "10:33 AM" },
        new() { Role = "Assistant", Content = "Sure! Here's a simple example:\n\n```csharp\nvar services = new ServiceCollection();\nservices.AddSingleton<IMyService, MyService>();\nvar provider = services.BuildServiceProvider();\nvar service = provider.GetRequiredService<IMyService>();\n```", Timestamp = "10:33 AM" }
    ];

    public ChatView()
    {
        InitializeComponent();

        // Resolve ChatThreadViewModel from DI and set as DataContext
        _viewModel = App.ServiceProvider.GetRequiredService<ChatThreadViewModel>();
        DataContext = _viewModel;

        // Initialize the ViewModel (load default persona, populate list)
        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
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

public class SampleMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}
