using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Interfaces;

public interface IDeviceTestLogWriter
{
    Task<string> WriteAsync(DeviceTestLogRequest request, CancellationToken ct = default);
}
