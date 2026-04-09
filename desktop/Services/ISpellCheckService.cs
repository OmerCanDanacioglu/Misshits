namespace Misshits.Desktop.Services;

public interface ISpellCheckService
{
    List<Suggestion> Lookup(string word, bool shorterOnly = false);
}
