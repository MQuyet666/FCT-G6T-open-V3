namespace HardwareTestApp.src.Domain.Models;

public class G6TResponse
{
    public G6TCommandId CommandId { get; init; }
    public G6TStatus Status { get; init; }
    public bool IsSuccess => Status == G6TStatus.Success;
}
