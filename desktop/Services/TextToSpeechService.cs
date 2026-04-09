using System.Diagnostics;

namespace Misshits.Desktop.Services;

public class TextToSpeechService : ITextToSpeechService
{
    private Process? _currentProcess;

    public void Speak(string text)
    {
        Cancel();
        if (string.IsNullOrWhiteSpace(text)) return;

        var escaped = text.Replace("'", "'\\''");

        if (OperatingSystem.IsWindows())
        {
            _currentProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Speak('{escaped}')\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            _currentProcess = Process.Start("say", $"-v Daniel '{escaped}'");
        }
        else if (OperatingSystem.IsLinux())
        {
            _currentProcess = Process.Start("espeak", $"-v en-gb '{escaped}'");
        }
    }

    public void Cancel()
    {
        if (_currentProcess is { HasExited: false })
        {
            _currentProcess.Kill();
            _currentProcess.Dispose();
        }
        _currentProcess = null;
    }
}
