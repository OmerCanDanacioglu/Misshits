namespace Misshits.Desktop.Services;

public interface ITextToSpeechService
{
    void Speak(string text);
    void Cancel();
}
