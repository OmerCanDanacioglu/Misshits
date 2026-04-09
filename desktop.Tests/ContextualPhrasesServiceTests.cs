using NUnit.Framework;
using Shouldly;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.Tests;

[TestFixture]
public class ContextualPhrasesServiceTests
{
    private readonly ContextualPhrasesService _service = new();

    [Test]
    public void GetTimePeriodForHour_ReturnsCorrectPeriods()
    {
        _service.GetTimePeriodForHour(6).ShouldBe("Morning");
        _service.GetTimePeriodForHour(10).ShouldBe("Morning");
        _service.GetTimePeriodForHour(12).ShouldBe("Midday");
        _service.GetTimePeriodForHour(15).ShouldBe("Afternoon");
        _service.GetTimePeriodForHour(19).ShouldBe("Evening");
        _service.GetTimePeriodForHour(22).ShouldBe("Night");
        _service.GetTimePeriodForHour(3).ShouldBe("Night");
    }

    [Test]
    public void GetTimePeriodForHour_BoundaryValues()
    {
        _service.GetTimePeriodForHour(5).ShouldBe("Morning");
        _service.GetTimePeriodForHour(11).ShouldBe("Midday");
        _service.GetTimePeriodForHour(14).ShouldBe("Afternoon");
        _service.GetTimePeriodForHour(17).ShouldBe("Evening");
        _service.GetTimePeriodForHour(21).ShouldBe("Night");
        _service.GetTimePeriodForHour(0).ShouldBe("Night");
        _service.GetTimePeriodForHour(4).ShouldBe("Night");
    }

    [Test]
    public void GetPhrasesForPeriod_MorningContainsMorningPhrases()
    {
        var phrases = _service.GetPhrasesForPeriod("Morning");
        phrases.ShouldContain("Good morning");
        phrases.ShouldContain("What's for breakfast");
    }

    [Test]
    public void GetPhrasesForPeriod_NightContainsNightPhrases()
    {
        var phrases = _service.GetPhrasesForPeriod("Night");
        phrases.ShouldContain("Good night");
        phrases.ShouldContain("I can't sleep");
    }

    [Test]
    public void GetPhrasesForPeriod_AlwaysIncludesUniversalPhrases()
    {
        foreach (var period in new[] { "Morning", "Midday", "Afternoon", "Evening", "Night" })
        {
            var phrases = _service.GetPhrasesForPeriod(period);
            phrases.ShouldContain("Yes");
            phrases.ShouldContain("No");
            phrases.ShouldContain("Thank you");
        }
    }

    [Test]
    public void GetCurrentPhrases_ReturnsNonEmptyList()
    {
        var phrases = _service.GetCurrentPhrases();
        phrases.ShouldNotBeEmpty();
    }

    [Test]
    public void CurrentTimePeriod_ReturnsValidPeriod()
    {
        var valid = new[] { "Morning", "Midday", "Afternoon", "Evening", "Night" };
        valid.ShouldContain(_service.CurrentTimePeriod);
    }

    [Test]
    public void GetPhrasesForPeriod_UnknownPeriod_ReturnsOnlyUniversal()
    {
        var phrases = _service.GetPhrasesForPeriod("Unknown");
        phrases.ShouldContain("Yes");
        phrases.ShouldNotContain("Good morning");
        phrases.ShouldNotContain("Good night");
    }
}
