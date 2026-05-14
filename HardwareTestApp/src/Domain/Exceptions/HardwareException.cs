using System;

namespace FCT.G6T.Domain.Exceptions;

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
