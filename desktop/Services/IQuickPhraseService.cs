using Misshits.Desktop.Models;

namespace Misshits.Desktop.Services;

public interface IQuickPhraseService
{
    Task<List<QuickPhrase>> GetAllAsync();
    Task<QuickPhrase> AddAsync(string text);
    Task DeleteAsync(int id);
    Task<string?> UseAsync(int id);
}
