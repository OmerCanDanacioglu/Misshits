namespace Misshits.Desktop.Services;

public interface ISymSpellService
{
    Task LoadDictionaryAsync(IServiceProvider services);
    List<Suggestion> Lookup(string input, int? maxDistance = null, int? maxLength = null);
}
