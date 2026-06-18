using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Services;

public class KestrelWebSocketServer : ILocalWebSocketServer
{
#pragma warning disable CS0414, CS0067
    public event EventHandler<string>? MessageReceived;
#pragma warning restore CS0414, CS0067

    public int Port => 0;

    public bool IsRunning => false;

    public string AuthToken => string.Empty;

    public Task StartAsync(int? preferredPort = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendAsync(string message, CancellationToken ct = default) =>
        Task.CompletedTask;

    public string RegenerateAuthToken() => string.Empty;

    public void Dispose()
    {
        MessageReceived = null;
    }
}
