using NUnit.Framework;
using Shouldly;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.Tests;

public class SymSpellServiceTests
{
    private SymSpellService CreateLoadedService()
    {
        var service = new SymSpellService(maxEditDistance: 2, prefixLength: 7);
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
        var wordsField = typeof(SymSpellService).GetField("_words",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var deletesField = typeof(SymSpellService).GetField("_deletes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var loadedField = typeof(SymSpellService).GetField("_loaded",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var wordsDict = (Dictionary<string, long>)wordsField.GetValue(service)!;
        var deletesDict = (Dictionary<string, HashSet<string>>)deletesField.GetValue(service)!;

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

    [Test]
    public void Lookup_ExactMatch_ReturnsDistance0()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("hello");

        results.Count.ShouldBe(1);
        results[0].Term.ShouldBe("hello");
        results[0].Distance.ShouldBe(0);
    }

    [Test]
    public void Lookup_Misspelled_ReturnsSortedByDistanceThenFrequency()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("helo");

        results.Count.ShouldBeGreaterThan(1);
        results[0].Distance.ShouldBe(1);
        results[0].Frequency.ShouldBeGreaterThanOrEqualTo(results[1].Frequency);
    }

    [Test]
    public void Lookup_RespectsMaxLengthFilter()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("helo", maxLength: 4);

        results.ShouldNotContain(s => s.Term == "hello");
        results.ShouldContain(s => s.Term == "help" || s.Term == "hell");
    }

    [Test]
    public void Lookup_TwoLetterWord_PromotesHighFrequencyAlternative()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("mi");

        results.Count.ShouldBeGreaterThan(1);
        results[0].Term.ShouldBe("me");
    }

    [Test]
    public void Lookup_UnknownWord_BeyondEditDistance_ReturnsEmpty()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("xyzqwert");

        results.ShouldBeEmpty();
    }

    [Test]
    public void Lookup_SingleChar_ReturnsResults()
    {
        var service = CreateLoadedService();
        var results = service.Lookup("a");

        results.ShouldNotBeNull();
    }
}
