using NUnit.Framework;
using Shouldly;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.Tests;

public class TextBufferTests
{
    [Test]
    public void SetText_ChangesTextProperty()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.Text.ShouldBe("hello");
    }

    [Test]
    public void SetText_FiresTextChangedEvent()
    {
        var buffer = new TextBuffer();
        string? received = null;
        buffer.TextChanged += text => received = text;

        buffer.SetText("hello");
        received.ShouldBe("hello");
    }

    [Test]
    public void SetText_SameValue_IsNoOp()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");

        var eventFired = false;
        buffer.TextChanged += _ => eventFired = true;

        buffer.SetText("hello");
        eventFired.ShouldBeFalse();
    }

    [Test]
    public void AppendText_AppendsCorrectly()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.AppendText(" world");
        buffer.Text.ShouldBe("hello world");
    }

    [Test]
    public void AppendText_SmartSpacing_AddsSpaceWhenNeeded()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.AppendText("world", smartSpacing: true);
        buffer.Text.ShouldBe("hello world");
    }

    [Test]
    public void AppendText_SmartSpacing_SkipsSpaceAfterSpace()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello ");
        buffer.AppendText("world", smartSpacing: true);
        buffer.Text.ShouldBe("hello world");
    }

    [Test]
    public void AppendText_SmartSpacing_SkipsSpaceAfterNewline()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello\n");
        buffer.AppendText("world", smartSpacing: true);
        buffer.Text.ShouldBe("hello\nworld");
    }

    [Test]
    public void AppendText_SmartSpacing_SkipsSpaceWhenEmpty()
    {
        var buffer = new TextBuffer();
        buffer.AppendText("hello", smartSpacing: true);
        buffer.Text.ShouldBe("hello");
    }

    [Test]
    public void Undo_RevertsToPreviousState()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.SetText("hello world");
        buffer.Undo();
        buffer.Text.ShouldBe("hello");
    }

    [Test]
    public void Undo_WhenEmpty_DoesNothing()
    {
        var buffer = new TextBuffer();
        buffer.Undo();
        buffer.Text.ShouldBe("");
    }

    [Test]
    public void Undo_MultipleSteps()
    {
        var buffer = new TextBuffer();
        buffer.SetText("a");
        buffer.SetText("ab");
        buffer.SetText("abc");
        buffer.Undo();
        buffer.Text.ShouldBe("ab");
        buffer.Undo();
        buffer.Text.ShouldBe("a");
        buffer.Undo();
        buffer.Text.ShouldBe("");
    }

    [Test]
    public void Clear_ResetsText()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello world");
        buffer.Clear();
        buffer.Text.ShouldBe("");
    }

    [Test]
    public void HistoryLimit_DoesNotCauseInfiniteLoop()
    {
        var buffer = new TextBuffer();
        for (var i = 0; i < 60; i++)
            buffer.SetText($"text{i}");

        buffer.SetText("final");
        buffer.Text.ShouldBe("final");
    }

    [Test]
    public void DisplayText_IncludesCursorWhenVisible()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.SetCursorVisible(true);
        buffer.DisplayText.ShouldBe("hello|");
    }

    [Test]
    public void DisplayText_ShowsSpaceWhenCursorHidden()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.SetCursorVisible(false);
        buffer.DisplayText.ShouldBe("hello ");
    }
}
