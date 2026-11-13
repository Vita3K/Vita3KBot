using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Vita3KBot.APIClients.GeminiClient
{
  // Single entry point for every Gemini call.
  public static class GeminiClient
  {
    private const string JsonMimeType = "application/json";

    // Gemini answers 429/503 when it is momentarily overloaded; those are worth retrying,
    // and worth telling the user apart from a real failure.
    private const int MaxAttempts = 3;
    public const string BusyMessage = "Gemini is a bit overloaded right now 🚦 — give it a moment and try again.";

    // Large prompts (log analysis) can take well over the 100 s HttpClient default.
    // Used for the Flash-Lite fallback itself, which has nothing left to fall back to.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    // Hydrogen and the Flash models hang far more often than they are genuinely slow, so give up
    // on them sooner and retry on Flash-Lite instead of making the user wait the full 3 minutes.
    // Hydrogen gets more room than Flash because it handles the large log-analysis prompts.
    private static readonly TimeSpan MainTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan FlashTimeout = TimeSpan.FromSeconds(60);

    // Timeouts are enforced per request below, so the client itself never cuts a call short.
    private static readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    private static readonly string ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
      ?? throw new InvalidOperationException("GEMINI_API_KEY environment variable is not set.");

    public static bool IsBusy(HttpStatusCode code) =>
      code is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;

    private static bool IsTransient(HttpStatusCode code) =>
      IsBusy(code)
      || code is HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout;

    // Serializes the body and retries transient failures with exponential backoff (1 s, 2 s).
    // When Hydrogen or a Flash model times out (client-side or 504), the request is retried once on Flash-Lite.
    // The caller owns the returned response. Throws TaskCanceledException if the final attempt times out.
    public static async Task<HttpResponseMessage> PostAsync(string model, object body)
    {
      var json = JsonSerializer.Serialize(body);

      if (model == GeminiModels.FlashLite)
      {
        return await PostWithRetryAsync(model, json, DefaultTimeout);
      }

      try
      {
        var timeout = GeminiModels.IsFlash(model) ? FlashTimeout : MainTimeout;
        var resp = await PostWithRetryAsync(model, json, timeout);
        if (resp.StatusCode != HttpStatusCode.GatewayTimeout)
        {
          return resp;
        }

        resp.Dispose();
      }
      catch (TaskCanceledException)
      {
        // Fall through to Flash-Lite below.
      }

      Console.WriteLine($"[Gemini] {model} timed out, retrying on {GeminiModels.FlashLite}");
      return await PostWithRetryAsync(GeminiModels.FlashLite, json, DefaultTimeout);
    }

    private static async Task<HttpResponseMessage> PostWithRetryAsync(string model, string json, TimeSpan timeout)
    {
      var url = $"{GeminiModels.BaseUrl}/{model}:generateContent?key={ApiKey}";

      for (var attempt = 1; ; attempt++)
      {
        using var cts = new CancellationTokenSource(timeout);
        var resp = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, JsonMimeType), cts.Token);

        if (resp.IsSuccessStatusCode || !IsTransient(resp.StatusCode) || attempt >= MaxAttempts)
        {
          return resp;
        }

        Console.WriteLine($"[Gemini] {(int)resp.StatusCode} on {model} (attempt {attempt}/{MaxAttempts})");
        resp.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
      }
    }

    // Returns candidates[0].content.parts[0].text of a successful response.
    public static async Task<string> ReadTextAsync(HttpResponseMessage resp)
    {
      using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
      return doc.RootElement
        .GetProperty("candidates")[0]
        .GetProperty("content")
        .GetProperty("parts")[0]
        .GetProperty("text")
        .GetString() ?? "";
    }

    // Strips a ```json fence the model sometimes wraps JSON output in.
    public static string StripJsonFence(string raw)
    {
      var clean = raw.Trim().TrimStart('`');
      if (clean.StartsWith("json"))
      {
        clean = clean[4..];
      }

      return clean.Trim('`').Trim();
    }
  }
}
