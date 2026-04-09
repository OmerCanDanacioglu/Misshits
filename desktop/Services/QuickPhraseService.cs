using Microsoft.EntityFrameworkCore;
using Misshits.Desktop.Data;
using Misshits.Desktop.Models;

namespace Misshits.Desktop.Services;

public class QuickPhraseService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<QuickPhrase>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.QuickPhrases
            .OrderByDescending(p => p.UsageCount)
            .ToListAsync();
    }

    public async Task<QuickPhrase> AddAsync(string text)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var phrase = new QuickPhrase { Text = text };
        db.QuickPhrases.Add(phrase);
        await db.SaveChangesAsync();
        return phrase;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var phrase = await db.QuickPhrases.FindAsync(id);
        if (phrase != null)
        {
            db.QuickPhrases.Remove(phrase);
            await db.SaveChangesAsync();
        }
    }

    public async Task<string?> UseAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var phrase = await db.QuickPhrases.FindAsync(id);
        if (phrase == null) return null;
        phrase.UsageCount++;
        await db.SaveChangesAsync();
        return phrase.Text;
    }
}
