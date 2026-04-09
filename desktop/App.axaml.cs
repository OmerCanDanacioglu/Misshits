using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misshits.Desktop.Data;
using Misshits.Desktop.Models;
using Misshits.Desktop.Services;
using Misshits.Desktop.ViewModels;
using Misshits.Desktop.Views;

namespace Misshits.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var provider = ConfigureServices();
            Services = provider;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = provider.GetRequiredService<MainWindowViewModel>();
                desktop.MainWindow = new MainWindow { DataContext = vm };

                LoadDictionaryInBackground(provider);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Misshits: Fatal startup error: {ex}");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Database
        var dbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Misshits");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "misshits.db");

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Configuration
        services.AddSingleton(new SmartConnectionOptions());

        // Services
        services.AddSingleton<ISymSpellService, SymSpellService>();
        services.AddHttpClient<ISmartConnectionService, SmartConnectionService>();
        services.AddSingleton<ISpellCheckService, SpellCheckService>();
        services.AddSingleton<IQuickPhraseService, QuickPhraseService>();
        services.AddSingleton<ITextToSpeechService, TextToSpeechService>();
        services.AddSingleton<ITextBuffer, TextBuffer>();
        services.AddSingleton<IAutoCorrectionService, AutoCorrectionService>();

        // ViewModels
        services.AddSingleton<KeyboardViewModel>();
        services.AddSingleton<QuickPhrasesViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    private static void LoadDictionaryInBackground(IServiceProvider provider)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();

                var symSpell = provider.GetRequiredService<ISymSpellService>();
                await symSpell.LoadDictionaryAsync(provider);
                Console.WriteLine("Misshits: Dictionary loaded.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Misshits: Startup error: {ex}");
            }
        });
    }
}
