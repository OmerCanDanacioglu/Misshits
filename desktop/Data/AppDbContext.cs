using Microsoft.EntityFrameworkCore;
using Misshits.Desktop.Models;

namespace Misshits.Desktop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<WordFrequency> WordFrequencies => Set<WordFrequency>();
    public DbSet<QuickPhrase> QuickPhrases => Set<QuickPhrase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WordFrequency>(entity =>
        {
            entity.HasIndex(e => e.Word).IsUnique();
        });

        modelBuilder.Entity<QuickPhrase>(entity =>
        {
            entity.HasIndex(e => e.Text).IsUnique();
        });
    }
}
