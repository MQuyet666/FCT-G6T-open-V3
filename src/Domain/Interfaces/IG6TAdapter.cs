using FCT.G6T.Domain.Models;

namespace FCT.G6T.Domain.Interfaces;

public interface IG6TAdapter : IDisposable
{
    event EventHandler<G6TTraceEventArgs>? Trace;
    Task ConnectAsync(string comPort, int baudRate, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<G6TResponse> SendCommandAsync(G6TCommand command, CancellationToken ct = default);
    Task SendCommandNoAckAsync(G6TCommand command, CancellationToken ct = default);
    bool IsConnected { get; }
    string ConnectedComPort { get; }
}

