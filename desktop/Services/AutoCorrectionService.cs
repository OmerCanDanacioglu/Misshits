using System.Text.RegularExpressions;
using Misshits.Desktop.Models;

namespace Misshits.Desktop.Services;

public class AutoCorrectionService(ISpellCheckService spellCheck) : IAutoCorrectionService
{
    private readonly List<WordCorrection> _corrections = new();

    public IReadOnlyList<WordCorrection> Corrections => _corrections.AsReadOnly();

    public void ClearCorrections() => _corrections.Clear();

    public string AutoCorrectAndAppend(string currentText, string suffix,
        string currentWord, IReadOnlyList<Suggestion> currentSuggestions, bool enabled)
    {
        var match = Regex.Match(currentText, @"[a-zA-Z]+$");
        if (!match.Success || !enabled)
            return currentText + suffix;

        var word = match.Value;

        // Only use cached suggestions if they match the current word
        if (currentWord.Equals(word, StringComparison.OrdinalIgnoreCase)
            && currentSuggestions.Count > 0 && currentSuggestions[0].Distance > 0)
        {
            var corrected = currentSuggestions[0].Term;
            _corrections.Add(new WordCorrection(word, corrected));
            return currentText[..match.Index] + corrected + suffix;
        }

        // Suggestions stale — try deferred correction asynchronously (caller handles)
        return currentText + suffix;
    }

    public WordCorrection? TryDeferredCorrection(string word)
    {
        if (word.Length < 2) return null;

        var results = spellCheck.Lookup(word);
        if (results.Count == 0 || results[0].Distance == 0) return null;

        var correction = new WordCorrection(word, results[0].Term);
        _corrections.Add(correction);
        return correction;
    }
}
