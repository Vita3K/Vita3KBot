namespace Vita3KBot.APIClients.GeminiClient
{
  // Centralizes Gemini model names and endpoint in one place.
  // When a new model is released, only this file needs to be updated
  // for the change to propagate everywhere.
  public static class GeminiModels
  {
    // Base URL for the Generative Language API
    public const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    // General-purpose model used for standard responses, classification, log analysis, etc.
    public const string Flash = "gemini-3.7-flash";

    // Model used when Google Search grounding is required
    public const string FlashSearch = "gemini-2.5-flash";

    // Lightweight model used for classification tasks (e.g. piracy detection)
    public const string FlashLite = "gemini-3.5-flash-lite";
  }
}
