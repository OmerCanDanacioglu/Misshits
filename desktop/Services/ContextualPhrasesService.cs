namespace Misshits.Desktop.Services;

public class ContextualPhrasesService : IContextualPhrasesService
{
    private static readonly Dictionary<string, List<string>> TimePhrases = new()
    {
        ["Morning"] = new()
        {
            "Good morning", "I slept well", "I didn't sleep well",
            "What's for breakfast", "I need my medication",
            "I'd like tea", "I'd like coffee", "I need to get dressed"
        },
        ["Midday"] = new()
        {
            "I'm hungry", "What's for lunch", "Can we go outside",
            "I'd like a drink", "I need a break", "What time is it"
        },
        ["Afternoon"] = new()
        {
            "Good afternoon", "I'd like a snack", "Can we watch TV",
            "I'd like to rest", "Can we go for a walk", "I need help"
        },
        ["Evening"] = new()
        {
            "Good evening", "What's for dinner", "I'm tired",
            "Can I have a bath", "I'd like to relax", "What's on TV tonight"
        },
        ["Night"] = new()
        {
            "Good night", "I can't sleep", "I need the toilet",
            "Turn off the light", "I'm cold", "I'm in pain", "I need water"
        }
    };

    private static readonly List<string> Universal = new()
    {
        "Yes", "No", "Thank you", "Please", "Help"
    };

    public string CurrentTimePeriod => GetTimePeriodForHour(DateTime.Now.Hour);

    public string GetTimePeriodForHour(int hour) => hour switch
    {
        >= 5 and < 11 => "Morning",
        >= 11 and < 14 => "Midday",
        >= 14 and < 17 => "Afternoon",
        >= 17 and < 21 => "Evening",
        _ => "Night"
    };

    public List<string> GetCurrentPhrases()
    {
        return GetPhrasesForPeriod(CurrentTimePeriod);
    }

    public List<string> GetPhrasesForPeriod(string period)
    {
        var phrases = new List<string>();
        if (TimePhrases.TryGetValue(period, out var timePhrases))
            phrases.AddRange(timePhrases);
        phrases.AddRange(Universal);
        return phrases;
    }
}
