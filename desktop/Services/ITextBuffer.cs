namespace Misshits.Desktop.Services;

public interface ITextBuffer
{
    string Text { get; }
    string DisplayText { get; }
    event Action<string>? TextChanged;
    void SetText(string newText);
    void AppendText(string text, bool smartSpacing = false);
    void Undo();
    void Clear();
    void SetCursorVisible(bool visible);
}
