using NUnit.Framework;
using NSubstitute;
using Shouldly;
using Misshits.Desktop.Models;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.Tests;

[TestFixture]
public class AutoCorrectionServiceTests
{
    private readonly ISpellCheckService _spellCheck = Substitute.For<ISpellCheckService>();
    private AutoCorrectionService CreateService() => new(_spellCheck);

    [Test]
    public void AutoCorrectAndAppend_CorrectedText_WhenSuggestionsMatchCurrentWord()
    {
        var service = CreateService();
        var suggestions = new List<Suggestion> { new("hello", 1, 80000) };

        var result = service.AutoCorrectAndAppend("helo", " ", "helo", suggestions, enabled: true);

        result.ShouldBe("hello ");
        service.Corrections.ShouldContain(c => c.Original == "helo" && c.Corrected == "hello");
    }

    [Test]
    public void AutoCorrectAndAppend_ReturnsOriginalPlusSuffix_WhenDisabled()
    {
        var service = CreateService();
        var suggestions = new List<Suggestion> { new("hello", 1, 80000) };

        var result = service.AutoCorrectAndAppend("helo", " ", "helo", suggestions, enabled: false);

        result.ShouldBe("helo ");
        service.Corrections.ShouldBeEmpty();
    }

    [Test]
    public void AutoCorrectAndAppend_ReturnsOriginalPlusSuffix_WhenNoSuggestions()
    {
        var service = CreateService();

        var result = service.AutoCorrectAndAppend("hello", " ", "hello", new List<Suggestion>(), enabled: true);

        result.ShouldBe("hello ");
    }

    [Test]
    public void AutoCorrectAndAppend_SkipsStaleSuggestions()
    {
        var service = CreateService();
        var suggestions = new List<Suggestion> { new("world", 1, 50000) };

        var result = service.AutoCorrectAndAppend("helo", " ", "world", suggestions, enabled: true);

        result.ShouldBe("helo ");
        service.Corrections.ShouldBeEmpty();
    }

    [Test]
    public void AutoCorrectAndAppend_SkipsExactMatch()
    {
        var service = CreateService();
        var suggestions = new List<Suggestion> { new("hello", 0, 80000) };

        var result = service.AutoCorrectAndAppend("hello", " ", "hello", suggestions, enabled: true);

        result.ShouldBe("hello ");
        service.Corrections.ShouldBeEmpty();
    }

    [Test]
    public void TryDeferredCorrection_ReturnsCorrection_WhenSpellCheckFindsOne()
    {
        _spellCheck.Lookup("helo", false)
            .Returns(new List<Suggestion> { new("hello", 1, 80000) });

        var service = CreateService();
        var result = service.TryDeferredCorrection("helo");

        result.ShouldNotBeNull();
        result!.Original.ShouldBe("helo");
        result.Corrected.ShouldBe("hello");
    }

    [Test]
    public void TryDeferredCorrection_ReturnsNull_ForCorrectWord()
    {
        _spellCheck.Lookup("hello", false)
            .Returns(new List<Suggestion> { new("hello", 0, 80000) });

        var service = CreateService();
        var result = service.TryDeferredCorrection("hello");

        result.ShouldBeNull();
    }

    [Test]
    public void TryDeferredCorrection_ReturnsNull_ForShortWord()
    {
        var service = CreateService();
        var result = service.TryDeferredCorrection("a");

        result.ShouldBeNull();
    }

    [Test]
    public void ClearCorrections_EmptiesList()
    {
        _spellCheck.Lookup("helo", false)
            .Returns(new List<Suggestion> { new("hello", 1, 80000) });

        var service = CreateService();
        service.TryDeferredCorrection("helo");
        service.Corrections.Count.ShouldBe(1);

        service.ClearCorrections();
        service.Corrections.ShouldBeEmpty();
    }

    [Test]
    public void Corrections_TracksMultipleCorrections()
    {
        var service = CreateService();
        var suggestions1 = new List<Suggestion> { new("hello", 1, 80000) };
        var suggestions2 = new List<Suggestion> { new("world", 1, 50000) };

        service.AutoCorrectAndAppend("helo", " ", "helo", suggestions1, true);
        service.AutoCorrectAndAppend("helo wrld", " ", "wrld", suggestions2, true);

        service.Corrections.Count.ShouldBe(2);
    }
}
