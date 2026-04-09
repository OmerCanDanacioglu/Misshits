using Microsoft.EntityFrameworkCore;
using MisshitsApi.Data;
using MisshitsApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=misshits.db"));

builder.Services.AddSingleton<SymSpellService>();
builder.Services.AddHttpClient<SmartConnectionService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();

// Seed database and load SymSpell dictionary at startup
await DatabaseSeeder.SeedAsync(app.Services);
var symSpell = app.Services.GetRequiredService<SymSpellService>();
await symSpell.LoadDictionaryAsync(app.Services);

app.MapGet("/api/spellcheck", (string word, int? maxDistance, int? maxLength) =>
{
    if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
        return Results.Ok(Array.Empty<object>());

    var suggestions = symSpell.Lookup(word, maxDistance ?? 2, maxLength);
    return Results.Ok(suggestions.Take(5));
});

app.MapPost("/api/correct-sentence", async (SentenceRequest req, SmartConnectionService smartConnection) =>
{
    if (string.IsNullOrWhiteSpace(req.Sentence))
        return Results.Ok(new { corrected = req.Sentence });

    var dtos = req.Corrections?
        .Select(c => new WordCorrectionDto(c.Original, c.Corrected))
        .ToList();
    var corrected = await smartConnection.CorrectSentenceAsync(req.Sentence, dtos);
    return Results.Ok(new { corrected });
});

app.MapPost("/api/predict-words", async (PredictRequest req, SmartConnectionService smartConnection) =>
{
    if (string.IsNullOrWhiteSpace(req.Context))
        return Results.Ok(new { predictions = Array.Empty<string>() });

    var predictions = await smartConnection.PredictWordsAsync(req.Context);
    return Results.Ok(new { predictions });
});

// Quick Phrases CRUD
app.MapGet("/api/phrases", async (AppDbContext db) =>
{
    var phrases = await db.QuickPhrases
        .OrderByDescending(p => p.UsageCount)
        .ToListAsync();
    return Results.Ok(phrases);
});

app.MapPost("/api/phrases", async (QuickPhraseRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest("Text is required");

    var existing = await db.QuickPhrases.FirstOrDefaultAsync(p => p.Text == req.Text);
    if (existing != null)
        return Results.Conflict("Phrase already exists");

    var phrase = new MisshitsApi.Models.QuickPhrase { Text = req.Text };
    db.QuickPhrases.Add(phrase);
    await db.SaveChangesAsync();
    return Results.Ok(phrase);
});

app.MapDelete("/api/phrases/{id}", async (int id, AppDbContext db) =>
{
    var phrase = await db.QuickPhrases.FindAsync(id);
    if (phrase == null) return Results.NotFound();
    db.QuickPhrases.Remove(phrase);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/phrases/{id}/use", async (int id, AppDbContext db) =>
{
    var phrase = await db.QuickPhrases.FindAsync(id);
    if (phrase == null) return Results.NotFound();
    phrase.UsageCount++;
    await db.SaveChangesAsync();
    return Results.Ok(phrase);
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

record WordCorrection(string Original, string Corrected);
record SentenceRequest(string Sentence, List<WordCorrection>? Corrections);
record PredictRequest(string Context);
record QuickPhraseRequest(string Text);
