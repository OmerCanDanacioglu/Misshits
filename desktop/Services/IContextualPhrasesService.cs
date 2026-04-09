namespace Misshits.Desktop.Services;

public interface IContextualPhrasesService
{
    List<string> GetCurrentPhrases();
    string CurrentTimePeriod { get; }
    string GetTimePeriodForHour(int hour);
    List<string> GetPhrasesForPeriod(string period);
}
