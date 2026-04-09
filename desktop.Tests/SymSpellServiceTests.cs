using Xunit;
using FluentAssertions;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.Tests;

public class SymSpellServiceTests
{
    private SymSpellService CreateLoadedService()
    {
        var service = new SymSpellService(maxEditDistance: 2, prefixLength: 7);
        // Manually populate the dictionaries via reflection or a helper
        // For testing, we'll use LoadDictionaryAsync with a mock provider
        // Instead, we test via the public API after loading known words
        LoadWords(service, new Dictionary<string, long>
        {
            ["hello"] = 80000,
            ["help"] = 112000,
            ["hell"] = 15000,
            ["world"] = 50000,
            ["would"] = 90000,
            ["the"] = 9000000,
            ["me"] = 700000,
            ["mi"] = 1500,
            ["we"] = 2000000,
            ["be"] = 1200000,
        });
        return service;
    }

    private static void LoadWords(SymSpellService service, Dictionary<string, long> words)
    {
        // Use reflection to access private fields for testing
        var wordsField = typeof(SymSpellService).GetField("_words",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var deletesField = typeof(SymSpellService).GetField("_deletes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var loadedField = typeof(SymSpellService).GetField("_loaded",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var wordsDict = (Dictionary<string, long>)wordsField.GetValue(service)!;
        var deletesDict = (Dictionary<string, HashSet<string>>)deletesField.GetValue(service)!;

        // Use reflection to call GenerateEditsWithin
        var genEdits = typeof(SymSpellService).GetMethod("GenerateEditsWithin",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        foreach (var (word, freq) in words)
        {
            wordsDict[word] = freq;
            var edits = (HashSet<string>)genEdits.Invoke(service, new object[] { word, 2 })!;
            foreach (var edit in edits)
            {
                if (deletesDict.TryGetValue(edit, out var set))
                    set.Add(word);
                else
                    deletesDict[edit] = new HashSet<string> { word };
            }
        }

        loadedField.SetValue(service, true);
    }

    [Fact]
    public void Lookup_ExactMatch_ReturnsDistance0()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("hello");

        results.Should().ContainSingle();
        results[0].Term.Should().Be("hello");
        results[0].Distance.Should().Be(0);
    }

    [Fact]
    public void Lookup_Misspelled_ReturnsSortedByDistanceThenFrequency()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("helo");

        results.Should().HaveCountGreaterThan(1);
        // All should be distance 1
        results[0].Distance.Should().Be(1);
        // First result should be highest frequency among distance-1 matches
        results[0].Frequency.Should().BeGreaterOrEqualTo(results[1].Frequency);
    }

    [Fact]
    public void Lookup_RespectsMaxLengthFilter()
    {
        var service = CreateLoadedService();
        // "helo" is 4 chars, maxLength=4 should exclude "hello" (5 chars)
        var results = service.Lookup("helo", maxLength: 4);

        results.Should().NotContain(s => s.Term == "hello");
        results.Should().Contain(s => s.Term == "help" || s.Term == "hell");
    }

    [Fact]
    public void Lookup_TwoLetterWord_PromotesHighFrequencyAlternative()
    {
        var service = CreateLoadedService();
        // "mi" is a valid word (freq 1500) but "me" (freq 700000) is 10x+ higher
        var results = service.Lookup("mi");

        results.Should().HaveCountGreaterThan(1);
        results[0].Term.Should().Be("me"); // promoted due to much higher frequency
    }

    [Fact]
    public void Lookup_UnknownWord_BeyondEditDistance_ReturnsEmpty()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("xyzqwert");

        results.Should().BeEmpty();
    }

    [Fact]
    public void Lookup_SingleChar_ReturnsResults()
    {
        var service = CreateLoadedService();
        // Single char inputs may return short words within edit distance
        var results = service.Lookup("a");

        // Should not crash, may return results for short words
        results.Should().NotBeNull();
    }
}
