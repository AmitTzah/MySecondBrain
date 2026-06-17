using Microsoft.Extensions.DependencyInjection;
using System.Windows;
// Resolves ambiguity with System.Windows.Forms.Application from UseWindowsForms=true
using Application = System.Windows.Application;

namespace MySecondBrain.UI;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (_serviceProvider as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services, repositories, and ViewModels will be registered here
        // by subsequent features. Empty for now — just the window.
        services.AddSingleton<MainWindow>();
    }
}
