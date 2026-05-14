using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Services;

public sealed class HardwareTraceService : IHardwareTraceService
{
    private readonly IG6TAdapter _g6tAdapter;
    private readonly IDetectorAdapter _detectorAdapter;
    private bool _isStarted;

    public event EventHandler<string>? TraceReceived;

    public HardwareTraceService(IG6TAdapter g6tAdapter, IDetectorAdapter detectorAdapter)
    {
        _g6tAdapter = g6tAdapter;
        _detectorAdapter = detectorAdapter;
    }

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _g6tAdapter.Trace += OnG6TTrace;
        _detectorAdapter.Trace += OnDetectorTrace;
        _isStarted = true;
    }

    public void Stop()
    {
        if (!_isStarted)
        {
            return;
        }

        _g6tAdapter.Trace -= OnG6TTrace;
        _detectorAdapter.Trace -= OnDetectorTrace;
        _isStarted = false;
    }

    private void OnG6TTrace(object? sender, G6TTraceEventArgs e)
    {
        TraceReceived?.Invoke(this, e.Message);
    }

    private void OnDetectorTrace(object? sender, DetectorTraceEventArgs e)
    {
        TraceReceived?.Invoke(this, e.Message);
    }

    public void Dispose()
    {
        Stop();
    }
}
