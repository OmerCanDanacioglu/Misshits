namespace Misshits.Desktop.Models;

public class WordFrequency
{
    public int Id { get; set; }
    public required string Word { get; set; }
    public long Frequency { get; set; }
}
