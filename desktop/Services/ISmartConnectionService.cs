using Misshits.Desktop.Models;

namespace Misshits.Desktop.Services;

public interface ISmartConnectionService
{
    Task<string?> CorrectSentenceAsync(string sentence, List<WordCorrection>? corrections);
    Task<List<string>> PredictWordsAsync(string context);
}
