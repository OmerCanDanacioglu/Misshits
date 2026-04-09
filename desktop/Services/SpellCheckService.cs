namespace Misshits.Desktop.Services;

public class SpellCheckService(ISymSpellService symSpell) : ISpellCheckService
{
    public List<Suggestion> Lookup(string word, bool shorterOnly = false)
    {
        return symSpell.Lookup(word, 2, shorterOnly ? word.Length : (int?)null);
    }
}
