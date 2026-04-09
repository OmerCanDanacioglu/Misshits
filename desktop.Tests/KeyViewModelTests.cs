using Xunit;
using FluentAssertions;
using Misshits.Desktop.Models;

namespace Misshits.Desktop.Tests;

public class KeyViewModelTests
{
    [Fact]
    public void IsActive_TrueWhenPressed()
    {
        var key = new KeyViewModel(new KeyDef("A", "KeyA"));
        key.IsPressed = true;
        key.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_TrueWhenToggled()
    {
        var key = new KeyViewModel(new KeyDef("Shift", "ShiftLeft"));
        key.IsToggled = true;
        key.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_FalseWhenNeitherPressedNorToggled()
    {
        var key = new KeyViewModel(new KeyDef("A", "KeyA"));
        key.IsActive.Should().BeFalse();
    }

    [Fact]
    public void DisplayLabel_InitializedFromDefinition()
    {
        var key = new KeyViewModel(new KeyDef("Q", "KeyQ"));
        key.DisplayLabel.Should().Be("Q");
    }

    [Fact]
    public void DisplayLabel_CanBeUpdated()
    {
        var key = new KeyViewModel(new KeyDef("Q", "KeyQ"));
        key.DisplayLabel = "q";
        key.DisplayLabel.Should().Be("q");
    }

    [Fact]
    public void Properties_ExposeDefinitionValues()
    {
        var def = new KeyDef("Enter", "Enter", 1.5, true, "enter");
        var key = new KeyViewModel(def);

        key.Code.Should().Be("Enter");
        key.Width.Should().Be(1.5);
        key.Special.Should().BeTrue();
        key.SubLabel.Should().Be("enter");
        key.IsSpace.Should().BeFalse();
    }

    [Fact]
    public void IsSpace_TrueForSpaceKey()
    {
        var key = new KeyViewModel(new KeyDef("space", "Space", 8));
        key.IsSpace.Should().BeTrue();
    }

    [Fact]
    public void PropertyChanged_FiresForIsPressed()
    {
        var key = new KeyViewModel(new KeyDef("A", "KeyA"));
        var changed = new List<string>();
        key.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        key.IsPressed = true;

        changed.Should().Contain("IsPressed");
        changed.Should().Contain("IsActive");
    }

    [Fact]
    public void PropertyChanged_FiresForIsToggled()
    {
        var key = new KeyViewModel(new KeyDef("Shift", "ShiftLeft"));
        var changed = new List<string>();
        key.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        key.IsToggled = true;

        changed.Should().Contain("IsToggled");
        changed.Should().Contain("IsActive");
    }
}
