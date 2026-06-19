using System.Collections.ObjectModel;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace MySecondBrain.UI.Views;

public partial class ChatView : UserControl
{
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
    }
}

public class SampleMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}
