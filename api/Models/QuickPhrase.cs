namespace MisshitsApi.Models;

public class QuickPhrase
{
    public int Id { get; set; }
    public required string Text { get; set; }
    public int UsageCount { get; set; }
}
