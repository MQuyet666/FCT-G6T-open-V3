using FCT.G6T.Domain.Models;

namespace FCT.G6T.Domain.Interfaces;

public interface IG6TAdapter : IDisposable
{
    void Connect(string comPort, int baudRate);
    void Disconnect();
    Task<G6TResponse> SendCommandAsync(G6TCommand command, CancellationToken ct = default);
    bool IsConnected { get; }
    string ConnectedComPort { get; }
}

