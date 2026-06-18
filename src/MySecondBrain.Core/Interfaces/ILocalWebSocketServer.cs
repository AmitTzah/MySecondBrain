namespace MySecondBrain.Core.Interfaces;

public interface ILocalWebSocketServer : IDisposable
{
    int Port { get; }
    bool IsRunning { get; }
    string AuthToken { get; }
    Task StartAsync(int? preferredPort = null, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    event EventHandler<string>? MessageReceived;
    Task SendAsync(string message, CancellationToken ct = default);
    string RegenerateAuthToken();
}
