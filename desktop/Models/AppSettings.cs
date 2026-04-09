namespace Misshits.Desktop.Models;

public class AppSettings
{
    public bool AutoCorrectEnabled { get; set; } = true;
    public bool ShorterOnly { get; set; }
    public bool AutoSpeak { get; set; }
    public bool ApiEnabled { get; set; } = true;
    public bool TimeAwareSuggestions { get; set; } = true;
}
