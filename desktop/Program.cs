using Avalonia;
using Avalonia.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Misshits.Desktop.Data;
using Misshits.Desktop.Services;

namespace Misshits.Desktop;

class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        if (args.Contains("--seed"))
        {
            await SeedDatabase();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new X11PlatformOptions
            {
                RenderingMode = new[] { X11RenderingMode.Software }
            })
            .LogToTrace();

    private static async Task SeedDatabase()
    {
        var dbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Misshits");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "misshits.db");
        var cachePath = Path.Combine(dbDir, "symspell.cache");

        Console.WriteLine($"Database path: {dbPath}");

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<ISymSpellService, SymSpellService>();
        var provider = services.BuildServiceProvider();

        await DatabaseSeeder.SeedAsync(provider);
        Console.WriteLine("Database seeded successfully.");

        // Pre-compute and save SymSpell index
        Console.WriteLine("Building SymSpell index...");
        var symSpell = provider.GetRequiredService<ISymSpellService>();
        await symSpell.LoadDictionaryAsync(provider);
        await symSpell.SaveIndexAsync(cachePath);
        Console.WriteLine("Done.");
    }
}
