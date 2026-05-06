namespace FCT.G6T.Infrastructure.Configuration;

public class LoggingSettings
{
    public string Directory { get; set; } = "logs";
    public string FilePrefix { get; set; } = "fct-";
    public int RetentionDays { get; set; } = 30;
}
