using System.IO.Ports;
using HardwareTestApp.src.Application.Interfaces;

namespace HardwareTestApp.src.Infrastructure.Serial;

public class ComPortProvider : IComPortProvider
{
    public IReadOnlyList<string> GetAvailableComPorts()
    {
        return SerialPort.GetPortNames()
            .OrderBy(name => name)
            .ToArray();
    }
}
