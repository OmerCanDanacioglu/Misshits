using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misshits.Desktop.Data;

namespace Misshits.Desktop.Services;

public class SymSpellService : ISymSpellService
{
    private readonly int _maxEditDistance;
    private readonly int _prefixLength;
    private readonly Dictionary<string, HashSet<string>> _deletes = new();
    private readonly Dictionary<string, long> _words = new();
    private bool _loaded;

    public SymSpellService(int maxEditDistance = 2, int prefixLength = 7)
    {
        _maxEditDistance = maxEditDistance;
        _prefixLength = prefixLength;
    }

    /// <summary>
    /// Load all words from the database into the in-memory SymSpell index.
    /// </summary>
    public async Task LoadDictionaryAsync(IServiceProvider services)
    {
        if (_loaded) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entries = await db.WordFrequencies
            .AsNoTracking()
            .ToListAsync();

        foreach (var entry in entries)
        {
            var word = entry.Word.ToLowerInvariant();
            _words[word] = entry.Frequency;

            foreach (var del in GenerateEditsWithin(word, _maxEditDistance))
            {
                if (_deletes.TryGetValue(del, out var set))
                    set.Add(word);
                else
                    _deletes[del] = new HashSet<string> { word };
            }
        }

        _loaded = true;
    }

    private static readonly byte[] Magic = "SYMSPELL"u8.ToArray();
    private const byte IndexVersion = 1;

    /// <summary>
    /// Save the pre-computed index to a binary file.
    /// </summary>
    public async Task SaveIndexAsync(string path)
    {
        await using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        // Header
        bw.Write(Magic);
        bw.Write(IndexVersion);
        bw.Write((byte)_maxEditDistance);
        bw.Write((byte)_prefixLength);

        // Words
        bw.Write(_words.Count);
        foreach (var (word, freq) in _words)
        {
            bw.Write(word);
            bw.Write(freq);
        }

        // Deletes
        bw.Write(_deletes.Count);
        foreach (var (del, candidates) in _deletes)
        {
            bw.Write(del);
            bw.Write(candidates.Count);
            foreach (var candidate in candidates)
                bw.Write(candidate);
        }

        Console.WriteLine($"Misshits: Index saved to {path} ({fs.Length / 1024 / 1024} MB)");
    }

    /// <summary>
    /// Load from a pre-computed binary index file. Returns false if file missing or corrupt.
    /// </summary>
    public async Task<bool> LoadIndexAsync(string path)
    {
        if (_loaded) return true;
        if (!File.Exists(path)) return false;

        try
        {
            await using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // Validate header
            var magic = br.ReadBytes(8);
            if (!magic.AsSpan().SequenceEqual(Magic)) return false;

            var version = br.ReadByte();
            if (version != IndexVersion) return false;

            var maxDist = br.ReadByte();
            var prefixLen = br.ReadByte();
            if (maxDist != _maxEditDistance || prefixLen != _prefixLength) return false;

            // Words
            var wordCount = br.ReadInt32();
            for (var i = 0; i < wordCount; i++)
            {
                var word = br.ReadString();
                var freq = br.ReadInt64();
                _words[word] = freq;
            }

            // Deletes
            var deleteCount = br.ReadInt32();
            for (var i = 0; i < deleteCount; i++)
            {
                var del = br.ReadString();
                var candidateCount = br.ReadInt32();
                var candidates = new HashSet<string>(candidateCount);
                for (var j = 0; j < candidateCount; j++)
                    candidates.Add(br.ReadString());
                _deletes[del] = candidates;
            }

            _loaded = true;
            Console.WriteLine($"Misshits: Dictionary loaded from cache ({_words.Count} words, {_deletes.Count} delete entries)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Misshits: Cache load failed, will fall back to DB: {ex.Message}");
            _words.Clear();
            _deletes.Clear();
            return false;
        }
    }

    /// <summary>
    /// Look up spelling suggestions for input.
    /// </summary>
    public List<Suggestion> Lookup(string input, int? maxDistance = null, int? maxLength = null)
    {
        var max = maxDistance ?? _maxEditDistance;
        var lower = input.ToLowerInvariant();

        _words.TryGetValue(lower, out var exactFreq);
        var isExactMatch = exactFreq > 0;

        // For longer words that are exact matches with high frequency, return immediately
        if (isExactMatch && lower.Length > 2)
            return new List<Suggestion> { new(lower, 0, exactFreq) };

        var suggestions = new Dictionary<string, Suggestion>();

        // Include the exact match itself if it exists
        if (isExactMatch)
            suggestions[lower] = new Suggestion(lower, 0, exactFreq);

        var inputEdits = GenerateEditsWithin(lower, max);
        inputEdits.Add(lower);

        foreach (var variant in inputEdits)
        {
            if (!_deletes.TryGetValue(variant, out var candidates))
                continue;

            foreach (var candidate in candidates)
            {
                if (suggestions.ContainsKey(candidate))
                    continue;

                if (maxLength.HasValue && candidate.Length > maxLength.Value)
                    continue;

                var dist = DamerauLevenshtein(lower, candidate);
                if (dist <= max)
                {
                    _words.TryGetValue(candidate, out var freq);
                    suggestions[candidate] = new Suggestion(candidate, dist, freq);
                }
            }
        }

        var results = suggestions.Values
            .OrderBy(s => s.Distance)
            .ThenByDescending(s => s.Frequency)
            .ToList();

        // For short exact matches: if a distance-1 candidate has much higher
        // frequency, promote it above the exact match so auto-correct picks it
        if (isExactMatch && lower.Length <= 2 && results.Count > 1)
        {
            var topAlt = results.FirstOrDefault(s => s.Distance == 1);
            if (topAlt != null && topAlt.Frequency > exactFreq * 10)
            {
                // Return the higher-frequency alternative first (as distance 1),
                // with the exact match second
                results.Remove(topAlt);
                results.Insert(0, topAlt);
            }
        }

        return results;
    }

    private HashSet<string> GenerateEditsWithin(string word, int distance)
    {
        var result = new HashSet<string>();
        if (distance == 0) return result;

        var prefix = word.Length > _prefixLength ? word[.._prefixLength] : word;
        var current = new HashSet<string> { prefix };

        for (var d = 1; d <= distance; d++)
        {
            var next = new HashSet<string>();
            foreach (var s in current)
            {
                for (var i = 0; i < s.Length; i++)
                {
                    var del = s[..i] + s[(i + 1)..];
                    if (result.Add(del))
                        next.Add(del);
                }
            }
            current = next;
        }

        return result;
    }

    private int DamerauLevenshtein(string a, string b)
    {
        var lenA = a.Length;
        var lenB = b.Length;

        if (lenA == 0) return lenB;
        if (lenB == 0) return lenA;
        if (a == b) return 0;
        if (Math.Abs(lenA - lenB) > _maxEditDistance) return _maxEditDistance + 1;

        var d = new int[lenA + 1, lenB + 1];

        for (var i = 0; i <= lenA; i++) d[i, 0] = i;
        for (var j = 0; j <= lenB; j++) d[0, j] = j;

        for (var i = 1; i <= lenA; i++)
        {
            for (var j = 1; j <= lenB; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);
            }
        }

        return d[lenA, lenB];
    }
}
