using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MisshitsApi.Models;

namespace MisshitsApi.Data;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Seeds the database with SUBTLEX-UK word frequencies if empty.
    /// Expects the file at api/Data/SUBTLEX-UK.txt
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        if (await db.WordFrequencies.AnyAsync())
            return;

        var filePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "SUBTLEX-UK.txt");
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Warning: SUBTLEX-UK.txt not found at {Path.GetFullPath(filePath)}. Skipping seed.");
            return;
        }

        Console.WriteLine("Seeding database with SUBTLEX-UK data...");

        var entries = new List<WordFrequency>();
        var lines = await File.ReadAllLinesAsync(filePath);

        // Spell_check column (index 22): "UK", "UKUS" = valid words, "X" = invalid
        var validSpellChecks = new HashSet<string> { "UK", "UKUS", "US" };

        // Skip header row
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length < 23) continue;

            var spellCheck = parts[22].Trim();
            if (!validSpellChecks.Contains(spellCheck)) continue;

            var word = parts[0].Trim().ToLowerInvariant();
            if (!AlphaOnly().IsMatch(word)) continue;
            if (word.Length < 2) continue;
            if (!long.TryParse(parts[1], out var freq)) continue;

            entries.Add(new WordFrequency { Word = word, Frequency = freq });
        }

        // Handle duplicates: keep highest frequency
        var deduplicated = entries
            .GroupBy(e => e.Word)
            .Select(g => g.OrderByDescending(e => e.Frequency).First())
            .ToList();

        // Batch insert for performance
        const int batchSize = 5000;
        for (var i = 0; i < deduplicated.Count; i += batchSize)
        {
            var batch = deduplicated.Skip(i).Take(batchSize);
            db.WordFrequencies.AddRange(batch);
            await db.SaveChangesAsync();
            Console.WriteLine($"  Seeded {Math.Min(i + batchSize, deduplicated.Count)}/{deduplicated.Count} words...");
        }

        Console.WriteLine($"Seeded {deduplicated.Count} words.");

        // Seed default quick phrases if none exist
        if (!await db.QuickPhrases.AnyAsync())
        {
            var defaults = new[]
            {
                "Yes", "No", "Thank you", "Please", "Hello",
                "Goodbye", "I need help", "I don't understand",
                "Can you repeat that?", "I'm fine", "Excuse me",
                "How are you?", "Nice to meet you", "Sorry"
            };
            foreach (var text in defaults)
                db.QuickPhrases.Add(new Models.QuickPhrase { Text = text });
            await db.SaveChangesAsync();
            Console.WriteLine($"Seeded {defaults.Length} default quick phrases.");
        }
    }

    [GeneratedRegex(@"^[a-z]+$")]
    private static partial Regex AlphaOnly();
}
