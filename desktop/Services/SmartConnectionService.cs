using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Misshits.Desktop.Models;

namespace Misshits.Desktop.Services;

public class SmartConnectionService
{
    private const string BaseUrl = "https://smartconnectionapimanagement.azure-api.net/staging";
    private const string ClientVersion = "3.0.98";
    private const string UserId = "Misshits";
    private const string FallbackPrompt =
        "Fix the sentence. Try to capture the meaning of the sentence. " +
        "Find if there are any out of place words and replace them with correct ones.";

    private readonly HttpClient _http;
    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    public SmartConnectionService(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
        _http.DefaultRequestHeaders.Add("client-version", ClientVersion);
    }

    public async Task<string?> CorrectSentenceAsync(string sentence, List<WordCorrection>? corrections)
    {
        var prompt = BuildPrompt(corrections);
        var token = await GetTokenAsync();
        var result = await SendCorrectionRequest(sentence, prompt, token);

        // If 401, refresh token and retry once
        if (result == null)
        {
            _token = null;
            token = await GetTokenAsync();
            result = await SendCorrectionRequest(sentence, prompt, token);
        }

        return result;
    }

    public async Task<List<string>> PredictWordsAsync(string context)
    {
        var token = await GetTokenAsync();
        var result = await SendPredictionRequest(context, token);

        if (result == null)
        {
            _token = null;
            token = await GetTokenAsync();
            result = await SendPredictionRequest(context, token);
        }

        return result ?? new List<string>();
    }

    private async Task<List<string>?> SendPredictionRequest(string context, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/gridCommands/sendToLLM");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            customPrompt = "Predict the next 5 most likely words the user would type next. " +
                "Return ONLY the words separated by commas, nothing else. " +
                "Consider common English phrases and natural conversation. " +
                "Keep predictions short (single words or common contractions like I'm, don't).",
            userContent = context,
            expectedResponseCount = 1,
            locale = "en-gb"
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;

        if (!response.IsSuccessStatusCode)
            return new List<string>();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<LlmResponse>(json);

        var raw = result?.Responses?.FirstOrDefault() ?? "";
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 0 && !w.Contains(' '))
            .Take(6)
            .ToList();
    }

    private static string BuildPrompt(List<WordCorrection>? corrections)
    {
        if (corrections == null || corrections.Count == 0)
            return FallbackPrompt;

        var wordList = string.Join(", ",
            corrections.Select(c => $"'{c.Original}' was auto-corrected to '{c.Corrected}'"));

        return
            $"The following words in this sentence were auto-corrected and may be wrong: {wordList}. " +
            "Review ONLY these auto-corrected words. If any were corrected to the wrong word, " +
            "replace them with the correct word based on the sentence context. " +
            "Do NOT change any other words, punctuation, or structure of the sentence. " +
            "Return the full sentence with only the necessary fixes applied.";
    }

    private async Task<string?> SendCorrectionRequest(string sentence, string prompt, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/gridCommands/sendToLLM");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            customPrompt = prompt,
            userContent = sentence,
            expectedResponseCount = 1,
            locale = "en-gb"
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;

        if (!response.IsSuccessStatusCode)
            return sentence; // Return original on error

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<LlmResponse>(json);

        return result?.Responses?.FirstOrDefault() ?? sentence;
    }

    private async Task<string> GetTokenAsync()
    {
        if (_token != null && DateTime.UtcNow < _tokenExpiry)
            return _token;

        await _authLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_token != null && DateTime.UtcNow < _tokenExpiry)
                return _token;

            var request = new HttpRequestMessage(HttpMethod.Post, "/authentication/login");
            var body = new { UserId };
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResponse>(json);

            _token = result?.Token ?? throw new Exception("No token in login response");
            _tokenExpiry = DateTime.UtcNow.AddMinutes(90); // Refresh before 2h expiry

            return _token;
        }
        finally
        {
            _authLock.Release();
        }
    }

    private record LoginResponse(
        [property: JsonPropertyName("token")] string Token);

    private record LlmResponse(
        [property: JsonPropertyName("responses")] List<string>? Responses);
}
