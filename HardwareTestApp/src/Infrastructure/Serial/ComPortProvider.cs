using System.IO.Ports;
using FCT.G6T.Application.Interfaces;

namespace FCT.G6T.Infrastructure.Serial;

public class ComPortProvider : IComPortProvider
{
    public IReadOnlyList<string> GetAvailableComPorts()
    {
        return SerialPort.GetPortNames()
            .OrderBy(name => name)
            .ToArray();
    }
}

