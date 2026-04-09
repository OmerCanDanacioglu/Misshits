using Misshits.Desktop.Models;

namespace Misshits.Desktop.Services;

public interface IAutoCorrectionService
{
    /// <summary>
    /// Auto-correct the last word in text and append suffix.
    /// Returns the new text string.
    /// </summary>
    string AutoCorrectAndAppend(string currentText, string suffix,
        string currentWord, IReadOnlyList<Suggestion> currentSuggestions, bool enabled);

    /// <summary>
    /// Attempt deferred correction for a word (direct spell lookup, no debounce).
    /// Returns the correction if found, null otherwise.
    /// </summary>
    WordCorrection? TryDeferredCorrection(string word);

    IReadOnlyList<WordCorrection> Corrections { get; }
    void ClearCorrections();
}
