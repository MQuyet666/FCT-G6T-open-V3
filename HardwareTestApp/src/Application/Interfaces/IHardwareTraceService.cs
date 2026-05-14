namespace FCT.G6T.Application.Interfaces;

public interface IHardwareTraceService : IDisposable
{
    event EventHandler<string>? TraceReceived;
    void Start();
    void Stop();
}
