namespace Misshits.Desktop.Services;

public class SpellCheckService(SymSpellService symSpell)
{
    public List<Suggestion> Lookup(string word, bool shorterOnly = false)
    {
        return symSpell.Lookup(word, 2, shorterOnly ? word.Length : (int?)null);
    }
}
