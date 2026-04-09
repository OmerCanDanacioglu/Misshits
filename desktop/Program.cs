using Avalonia;
using Avalonia.Media;

namespace Misshits.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new X11PlatformOptions
            {
                RenderingMode = new[] { X11RenderingMode.Software }
            })
            .LogToTrace();
}
