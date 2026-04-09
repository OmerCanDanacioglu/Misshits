namespace Misshits.Desktop.Models;

public class SmartConnectionOptions
{
    public string BaseUrl { get; set; } = "https://smartconnectionapimanagement.azure-api.net/staging";
    public string ClientVersion { get; set; } = "3.0.98";
    public string UserId { get; set; } = "Misshits";
    public string FallbackPrompt { get; set; } =
        "Fix the sentence. Try to capture the meaning of the sentence. " +
        "Find if there are any out of place words and replace them with correct ones.";
    public string PredictionPrompt { get; set; } =
        "Predict the next 5 most likely words the user would type next. " +
        "Return ONLY the words separated by commas, nothing else. " +
        "Consider common English phrases and natural conversation. " +
        "Keep predictions short (single words or common contractions like I'm, don't).";
}
