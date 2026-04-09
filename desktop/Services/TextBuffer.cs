namespace Misshits.Desktop.Services;

public class TextBuffer : ITextBuffer
{
    private readonly Stack<string> _history = new();
    private const int MaxHistory = 50;
    private string _text = "";
    private bool _cursorVisible = true;

    public string Text => _text;
    public string DisplayText => _text + (_cursorVisible ? "|" : " ");
    public event Action<string>? TextChanged;

    public void SetText(string newText)
    {
        if (newText == _text) return;
        PushHistory();
        _text = newText;
        TextChanged?.Invoke(_text);
    }

    public void AppendText(string text, bool smartSpacing = false)
    {
        if (smartSpacing)
        {
            var needsSpace = _text.Length > 0 && !_text.EndsWith(' ') && !_text.EndsWith('\n');
            SetText(_text + (needsSpace ? " " : "") + text);
        }
        else
        {
            SetText(_text + text);
        }
    }

    public void Undo()
    {
        if (_history.Count == 0) return;
        _text = _history.Pop();
        TextChanged?.Invoke(_text);
    }

    public void Clear() => SetText("");

    public void SetCursorVisible(bool visible)
    {
        _cursorVisible = visible;
    }

    private void PushHistory()
    {
        _history.Push(_text);
        // Trim oldest entries if over limit
        if (_history.Count > MaxHistory)
        {
            var items = _history.ToArray();
            _history.Clear();
            // Keep only the most recent MaxHistory items (they're in reverse order from ToArray)
            for (var i = Math.Min(items.Length, MaxHistory) - 1; i >= 0; i--)
                _history.Push(items[i]);
        }
    }
}
