using FCT.G6T.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FCT.G6T.Infrastructure.ModuleName;

public sealed class DeviceAdapterTemplate : IDeviceContract, IDisposable
{
    private readonly ILogger<DeviceAdapterTemplate> _logger;
    private bool _disposed;

    public DeviceAdapterTemplate(ILogger<DeviceAdapterTemplate> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        try
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Device operation failed.");
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DeviceAdapterTemplate));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
