namespace FCT.G6T.Domain.Models;

public static class LedColorThresholds
{
    public static bool IsRed(int red, int green, int blue)
    {
        return red >= 200 &&
            green <= 105 &&
            blue <= 105 &&
            red >= green * 1.8 &&
            red >= blue * 1.8;
    }

    public static bool IsGreen(int red, int green, int blue)
    {
        return green >= 170 &&
            red <= 160 &&
            blue <= 160 &&
            green >= red * 1.25 &&
            green >= blue * 1.25;
    }

    public static bool IsYellow(int red, int green, int blue)
    {
        return red >= 195 &&
            green >= 175 &&
            blue <= 80 &&
            red <= green * 1.35 &&
            green <= red * 1.15 &&
            Math.Min(red, green) >= blue * 2.4;
    }

    public static bool IsBlue(int red, int green, int blue)
    {
        return blue >= 170 &&
            green >= 150 &&
            red <= 120 &&
            blue >= red * 1.8 &&
            green >= red * 1.5;
    }
}
