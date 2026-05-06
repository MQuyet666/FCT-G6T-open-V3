namespace FCT.G6T.Application.Interfaces;

public interface IComPortProvider
{
    IReadOnlyList<string> GetAvailableComPorts();
}

