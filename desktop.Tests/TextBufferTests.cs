using Xunit;
using FluentAssertions;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.Tests;

public class TextBufferTests
{
    [Fact]
    public void SetText_ChangesTextProperty()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.Text.Should().Be("hello");
    }

    [Fact]
    public void SetText_FiresTextChangedEvent()
    {
        var buffer = new TextBuffer();
        string? received = null;
        buffer.TextChanged += text => received = text;

        buffer.SetText("hello");
        received.Should().Be("hello");
    }

    [Fact]
    public void SetText_SameValue_IsNoOp()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");

        var eventFired = false;
        buffer.TextChanged += _ => eventFired = true;

        buffer.SetText("hello");
        eventFired.Should().BeFalse();
    }

    [Fact]
    public void AppendText_AppendsCorrectly()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.AppendText(" world");
        buffer.Text.Should().Be("hello world");
    }

    [Fact]
    public void AppendText_SmartSpacing_AddsSpaceWhenNeeded()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.AppendText("world", smartSpacing: true);
        buffer.Text.Should().Be("hello world");
    }

    [Fact]
    public void AppendText_SmartSpacing_SkipsSpaceAfterSpace()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello ");
        buffer.AppendText("world", smartSpacing: true);
        buffer.Text.Should().Be("hello world");
    }

    [Fact]
    public void AppendText_SmartSpacing_SkipsSpaceAfterNewline()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello\n");
        buffer.AppendText("world", smartSpacing: true);
        buffer.Text.Should().Be("hello\nworld");
    }

    [Fact]
    public void AppendText_SmartSpacing_SkipsSpaceWhenEmpty()
    {
        var buffer = new TextBuffer();
        buffer.AppendText("hello", smartSpacing: true);
        buffer.Text.Should().Be("hello");
    }

    [Fact]
    public void Undo_RevertsToPreviousState()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.SetText("hello world");
        buffer.Undo();
        buffer.Text.Should().Be("hello");
    }

    [Fact]
    public void Undo_WhenEmpty_DoesNothing()
    {
        var buffer = new TextBuffer();
        buffer.Undo();
        buffer.Text.Should().Be("");
    }

    [Fact]
    public void Undo_MultipleSteps()
    {
        var buffer = new TextBuffer();
        buffer.SetText("a");
        buffer.SetText("ab");
        buffer.SetText("abc");
        buffer.Undo();
        buffer.Text.Should().Be("ab");
        buffer.Undo();
        buffer.Text.Should().Be("a");
        buffer.Undo();
        buffer.Text.Should().Be("");
    }

    [Fact]
    public void Clear_ResetsText()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello world");
        buffer.Clear();
        buffer.Text.Should().Be("");
    }

    [Fact]
    public void HistoryLimit_DoesNotCauseInfiniteLoop()
    {
        var buffer = new TextBuffer();
        // Push 60 entries — exceeds the 50 limit
        for (var i = 0; i < 60; i++)
            buffer.SetText($"text{i}");

        // Should not hang — regression test for Stack.TrimExcess bug
        buffer.SetText("final");
        buffer.Text.Should().Be("final");
    }

    [Fact]
    public void DisplayText_IncludesCursorWhenVisible()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.SetCursorVisible(true);
        buffer.DisplayText.Should().Be("hello|");
    }

    [Fact]
    public void DisplayText_ShowsSpaceWhenCursorHidden()
    {
        var buffer = new TextBuffer();
        buffer.SetText("hello");
        buffer.SetCursorVisible(false);
        buffer.DisplayText.Should().Be("hello ");
    }
}
