namespace HardwareTestApp.src.Application.Interfaces;

public interface IComPortProvider
{
    IReadOnlyList<string> GetAvailableComPorts();
}
