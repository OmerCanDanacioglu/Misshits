using NUnit.Framework;
using Shouldly;
using Misshits.Desktop.Models;

namespace Misshits.Desktop.Tests;

public class KeyViewModelTests
{
    [Test]
    public void IsActive_TrueWhenPressed()
    {
        var key = new KeyViewModel(new KeyDef("A", "KeyA"));
        key.IsPressed = true;
        key.IsActive.ShouldBeTrue();
    }

    [Test]
    public void IsActive_TrueWhenToggled()
    {
        var key = new KeyViewModel(new KeyDef("Shift", "ShiftLeft"));
        key.IsToggled = true;
        key.IsActive.ShouldBeTrue();
    }

    [Test]
    public void IsActive_FalseWhenNeitherPressedNorToggled()
    {
        var key = new KeyViewModel(new KeyDef("A", "KeyA"));
        key.IsActive.ShouldBeFalse();
    }

    [Test]
    public void DisplayLabel_InitializedFromDefinition()
    {
        var key = new KeyViewModel(new KeyDef("Q", "KeyQ"));
        key.DisplayLabel.ShouldBe("Q");
    }

    [Test]
    public void DisplayLabel_CanBeUpdated()
    {
        var key = new KeyViewModel(new KeyDef("Q", "KeyQ"));
        key.DisplayLabel = "q";
        key.DisplayLabel.ShouldBe("q");
    }

    [Test]
    public void Properties_ExposeDefinitionValues()
    {
        var def = new KeyDef("Enter", "Enter", 1.5, true, "enter");
        var key = new KeyViewModel(def);

        key.Code.ShouldBe("Enter");
        key.Width.ShouldBe(1.5);
        key.Special.ShouldBeTrue();
        key.SubLabel.ShouldBe("enter");
        key.IsSpace.ShouldBeFalse();
    }

    [Test]
    public void IsSpace_TrueForSpaceKey()
    {
        var key = new KeyViewModel(new KeyDef("space", "Space", 8));
        key.IsSpace.ShouldBeTrue();
    }

    [Test]
    public void PropertyChanged_FiresForIsPressed()
    {
        var key = new KeyViewModel(new KeyDef("A", "KeyA"));
        var changed = new List<string>();
        key.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        key.IsPressed = true;

        changed.ShouldContain("IsPressed");
        changed.ShouldContain("IsActive");
    }

    [Test]
    public void PropertyChanged_FiresForIsToggled()
    {
        var key = new KeyViewModel(new KeyDef("Shift", "ShiftLeft"));
        var changed = new List<string>();
        key.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        key.IsToggled = true;

        changed.ShouldContain("IsToggled");
        changed.ShouldContain("IsActive");
    }
}
