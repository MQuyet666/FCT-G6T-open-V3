using System;

namespace FCT.G6T.HAL.Serial;

public class HardwareException : Exception
{
    public HardwareException(string message)
        : base(message)
    {
    }

    public HardwareException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
