using Xunit;
using FluentAssertions;
using Moq;
using Misshits.Desktop.Models;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.Tests;

public class AutoCorrectionServiceTests
{
    private readonly Mock<ISpellCheckService> _spellCheckMock = new();
    private AutoCorrectionService CreateService() => new(_spellCheckMock.Object);

    [Fact]
    public void AutoCorrectAndAppend_CorrectedText_WhenSuggestionsMatchCurrentWord()
    {
        var service = CreateService();
        var suggestions = new List<Suggestion> { new("hello", 1, 80000) };

        var result = service.AutoCorrectAndAppend("helo", " ", "helo", suggestions, enabled: true);

        result.Should().Be("hello ");
        service.Corrections.Should().ContainSingle(c => c.Original == "helo" && c.Corrected == "hello");
    }

    [Fact]
    public void AutoCorrectAndAppend_ReturnsOriginalPlusSuffix_WhenDisabled()
    {
        var service = CreateService();
        var suggestions = new List<Suggestion> { new("hello", 1, 80000) };

        var result = service.AutoCorrectAndAppend("helo", " ", "helo", suggestions, enabled: false);

        result.Should().Be("helo ");
        service.Corrections.Should().BeEmpty();
    }

    [Fact]
    public void AutoCorrectAndAppend_ReturnsOriginalPlusSuffix_WhenNoSuggestions()
    {
        var service = CreateService();

        var result = service.AutoCorrectAndAppend("hello", " ", "hello", new List<Suggestion>(), enabled: true);

        result.Should().Be("hello ");
    }

    [Fact]
    public void AutoCorrectAndAppend_SkipsStaleSuggestions()
    {
        var service = CreateService();
        // currentWord is "world" but text ends with "helo" — suggestions are stale
        var suggestions = new List<Suggestion> { new("world", 1, 50000) };

        var result = service.AutoCorrectAndAppend("helo", " ", "world", suggestions, enabled: true);

        result.Should().Be("helo ");
        service.Corrections.Should().BeEmpty();
    }

    [Fact]
    public void AutoCorrectAndAppend_SkipsExactMatch()
    {
        var service = CreateService();
        var suggestions = new List<Suggestion> { new("hello", 0, 80000) };

        var result = service.AutoCorrectAndAppend("hello", " ", "hello", suggestions, enabled: true);

        result.Should().Be("hello ");
        service.Corrections.Should().BeEmpty();
    }

    [Fact]
    public void TryDeferredCorrection_ReturnsCorrection_WhenSpellCheckFindsOne()
    {
        _spellCheckMock.Setup(s => s.Lookup("helo", false))
            .Returns(new List<Suggestion> { new("hello", 1, 80000) });

        var service = CreateService();
        var result = service.TryDeferredCorrection("helo");

        result.Should().NotBeNull();
        result!.Original.Should().Be("helo");
        result.Corrected.Should().Be("hello");
    }

    [Fact]
    public void TryDeferredCorrection_ReturnsNull_ForCorrectWord()
    {
        _spellCheckMock.Setup(s => s.Lookup("hello", false))
            .Returns(new List<Suggestion> { new("hello", 0, 80000) });

        var service = CreateService();
        var result = service.TryDeferredCorrection("hello");

        result.Should().BeNull();
    }

    [Fact]
    public void TryDeferredCorrection_ReturnsNull_ForShortWord()
    {
        var service = CreateService();
        var result = service.TryDeferredCorrection("a");

        result.Should().BeNull();
    }

    [Fact]
    public void ClearCorrections_EmptiesList()
    {
        _spellCheckMock.Setup(s => s.Lookup("helo", false))
            .Returns(new List<Suggestion> { new("hello", 1, 80000) });

        var service = CreateService();
        service.TryDeferredCorrection("helo");
        service.Corrections.Should().HaveCount(1);

        service.ClearCorrections();
        service.Corrections.Should().BeEmpty();
    }

    [Fact]
    public void Corrections_TracksMultipleCorrections()
    {
        var service = CreateService();
        var suggestions1 = new List<Suggestion> { new("hello", 1, 80000) };
        var suggestions2 = new List<Suggestion> { new("world", 1, 50000) };

        service.AutoCorrectAndAppend("helo", " ", "helo", suggestions1, true);
        service.AutoCorrectAndAppend("helo wrld", " ", "wrld", suggestions2, true);

        service.Corrections.Should().HaveCount(2);
    }
}
