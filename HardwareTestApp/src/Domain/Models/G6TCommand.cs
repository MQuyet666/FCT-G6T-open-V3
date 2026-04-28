namespace FCT.G6T.Domain.Models;

public class G6TCommand
{
    public G6TCommandId CommandId { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

