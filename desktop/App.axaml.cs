using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misshits.Desktop.Data;
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
            Console.WriteLine("Misshits: Starting up...");

            var services = new ServiceCollection();

            var dbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Misshits");
            Directory.CreateDirectory(dbDir);
            var dbPath = Path.Combine(dbDir, "misshits.db");
            Console.WriteLine($"Misshits: DB path = {dbPath}");

            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            services.AddSingleton<SymSpellService>();
            services.AddHttpClient<SmartConnectionService>();
            services.AddSingleton<SpellCheckService>();
            services.AddSingleton<QuickPhraseService>();
            services.AddSingleton<TextToSpeechService>();

            services.AddSingleton<KeyboardViewModel>();
            services.AddSingleton<QuickPhrasesViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            var provider = services.BuildServiceProvider();
            Services = provider;

            Console.WriteLine("Misshits: Services built, creating window...");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = provider.GetRequiredService<MainWindowViewModel>();
                desktop.MainWindow = new MainWindow { DataContext = vm };
                Console.WriteLine("Misshits: Window created.");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Ensure DB and tables exist (no-op if already created)
                        using var scope = provider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await db.Database.EnsureCreatedAsync();

                        // Load SymSpell dictionary from DB into memory
                        var symSpell = provider.GetRequiredService<SymSpellService>();
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
        catch (Exception ex)
        {
            Console.WriteLine($"Misshits: Fatal startup error: {ex}");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
