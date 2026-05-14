namespace FCT.G6T.Domain.Models;

public readonly record struct RoiRegion(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
