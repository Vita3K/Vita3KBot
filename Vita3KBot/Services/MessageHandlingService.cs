using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Discord;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;
using APIClients;

namespace Vita3KBot.Services
{
  public class MessageHandlingService
  {
    private readonly DiscordSocketClient _client;
    private readonly IServiceProvider _services;
    private static readonly HttpClient _httpClient = new();
    private static readonly string GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new InvalidOperationException("GEMINI_API_KEY environment variable is not set.");

    // ========================
    // Piracy Detection
    // ========================

    // Trigger keywords — only send to AI if one of these is present
    private static readonly string[] TriggerKeywords =
    [
        "download", "game",
        "license", "bin",
        "rom", "iso",
        "link", "free",
        "rif", "pirate"
    ];

    private static async Task MonitorPiracy(SocketUserMessage msg, SocketGuildUser guildUser)
    {
      if (guildUser.IsBot || guildUser.IsWebhook) return;
      if (RolesUtils.IsWhitelisted(guildUser)) return;

      var content = msg.Content;
      if (string.IsNullOrWhiteSpace(content)) return;

      var lower = content.ToLower();

      // Step 2: Skip AI entirely if no trigger keyword is present (no API cost)
      bool hasTrigger = TriggerKeywords.Any(k => lower.Contains(k));

      bool isPiracy = false;

      if (hasTrigger)
      {
        // Step 3: Use Gemini API for context-aware judgment
        isPiracy = await CheckPiracyWithGeminiAsync(content);
      }

      if (!isPiracy) return;

      await HandlePiracyViolationAsync(msg, guildUser);
    }

    private static async Task<bool> CheckPiracyWithGeminiAsync(string content)
    {
      try
      {
        var prompt = $"""
                    Determine whether the following Discord message is related to
                    piracy (illegal downloading, ROM distribution requests, etc.)
                    of games.
                    However, if the message is intended to discourage or stop piracy, please answer“No”

                    Message: "{content}"

                    Reply with YES or NO only.
                    - Answer YES only if the message is clearly and explicitly about piracy.
                    - Answer NO if there is any doubt.
                    """;

        var requestBody = new
        {
          contents = new[] {
                        new { parts = new[] { new { text = prompt } } }
                    }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var response = await _httpClient.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={GeminiApiKey}",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode) return false;

        var resultJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resultJson);

        var answer = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "NO";

        return answer.Trim().ToUpper().StartsWith("YES");

      }
      catch (Exception ex)
      {
        // Silently fail — do not punish users on API errors
        Console.WriteLine($"Gemini API error: {ex.Message}");
        return false;
      }
    }

    private static async Task HandlePiracyViolationAsync(SocketUserMessage msg, SocketGuildUser guildUser)
    {
      var userId = guildUser.Id;

      // Send warning embed to the channel
      var embed = new EmbedBuilder()
          .WithTitle($"⚠️ Warning - NO piracy")
          .WithDescription(
              $"{guildUser.Mention} Discussion of piracy is not allowed on this server.\n" +
              "Please purchase games through official storefronts only.\n\n" +
              "🛒 [PlayStation Store](https://store.playstation.com) and other official retailers.\n" +
              "-# ⓘ We do not condone/support piracy, but what you're looking for might actually be right [here](<https://youtu.be/dQw4w9WgXcQ>).")
          .WithColor(Color.Orange)
          .WithFooter("⚠️ Please note that since we use AI-based detection, there may be errors. We appreciate your understanding.")
          .Build();

      await msg.Channel.SendMessageAsync(embed: embed);
    }

    // ========================
    // Image Spam Detection
    // ========================
    private static readonly ConcurrentDictionary<(ulong UserId, string Hash), List<(ulong ChannelId, ulong MessageId, DateTime PostedAt)>>
        _imagePostLog = new();

    private const int SpamChannelThreshold = 3;
    private static readonly TimeSpan SpamWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CacheCleanupInterval = TimeSpan.FromMinutes(5);

    private static string GetImageHash(IAttachment attachment) =>
        $"{attachment.Filename}:{attachment.Size}";

    // Periodically removes stale entries from the image post cache
    private static async Task RunCacheCleanupLoop()
    {
      while (true)
      {
        await Task.Delay(CacheCleanupInterval);
        var cutoff = DateTime.UtcNow - SpamWindow;
        foreach (var key in _imagePostLog.Keys.ToList())
        {
          if (!_imagePostLog.TryGetValue(key, out var posts)) continue;
          lock (posts)
          {
            posts.RemoveAll(p => p.PostedAt < cutoff);
            if (posts.Count == 0)
              _imagePostLog.TryRemove(key, out _);
          }
        }
      }
    }

    private static async Task MonitorImageSpam(SocketUserMessage msg, SocketGuildUser guildUser)
    {
      if (msg.Attachments.Count == 0) return;
      // Ignore bots, webhooks and administrators
      if (guildUser.IsBot || guildUser.IsWebhook) return;
      if (guildUser.GuildPermissions.Administrator) return;

      var now = DateTime.UtcNow;

      foreach (var attachment in msg.Attachments)
      {
        // Only process image files
        var ext = System.IO.Path.GetExtension(attachment.Filename).ToLower();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp")) continue;

        var key = (guildUser.Id, GetImageHash(attachment));
        var posts = _imagePostLog.GetOrAdd(key, _ => new List<(ulong, ulong, DateTime)>());

        lock (posts)
        {
          // Remove entries outside the time window
          posts.RemoveAll(p => now - p.PostedAt > SpamWindow);
          // Record this post
          posts.Add((msg.Channel.Id, msg.Id, now));

          var distinctChannels = posts.Select(p => p.ChannelId).Distinct().Count();
          if (distinctChannels < SpamChannelThreshold) return;
        }

        await ExecuteImageSpamAction(guildUser, posts.ToList(), msg);
        return;
      }
    }

    private static async Task ExecuteImageSpamAction(
            SocketGuildUser user,
            List<(ulong ChannelId, ulong MessageId, DateTime PostedAt)> posts,
            SocketUserMessage triggerMsg)
    {

      var guild = user.Guild;

      // 1. Delete all detected spam posts
      foreach (var (channelId, messageId, _) in posts)
      {
        try
        {
          if (guild.GetTextChannel(channelId) is { } ch)
            await ch.DeleteMessageAsync(messageId);
        }
        catch
        {
          // Ignore deletion failures and continue
        }
      }

      // 2. Kick the user
      try
      {
        var guildUser = user as IGuildUser ?? guild.GetUser(user.Id);
        if (guildUser != null)
          await guildUser.KickAsync($"Image spam: same image posted to {SpamChannelThreshold}+ channels within {SpamWindow.TotalMinutes} minute(s).");
        else
          Console.WriteLine($"Could not resolve guild user {user.Id} for kick.");
      }
      catch
      {
        Console.WriteLine($"Failed to kick user {user.Id} from guild {guild.Id}.");
      }

      // 3. Notify the user via DM
      try
      {
        var dm = await user.CreateDMChannelAsync();
        await dm.SendMessageAsync(
            $"⚠️ **You have been kicked for spam.**\n" +
            $"Reason: The same image was posted across multiple channels in a short period of time.");
      }
      catch
      {
        Console.WriteLine($"Could not send DM to user {user.Id}: DMs may be disabled.");
      }

      // 4. Log to the moderation channel
      var logEmbed = new EmbedBuilder()
          .WithTitle("Image spam detected")
          .WithDescription($"The same image was posted across {SpamChannelThreshold} channels within {SpamWindow.TotalMinutes} minute.")
          .AddField("User", user.Mention)
          .AddField("Posts deleted", posts.Count)
          .WithColor(Color.Red)
          .Build();
      await guild.GetTextChannel(757604199159824385).SendMessageAsync(embed: logEmbed).ConfigureAwait(false);

      // Clear cache entries for this user
      foreach (var key in _imagePostLog.Keys.Where(k => k.UserId == user.Id).ToList())
        _imagePostLog.TryRemove(key, out _);
    }

    private static async Task MonitorMentions(SocketUserMessage msg, SocketGuildUser guildUser)
    {
      if (guildUser.IsBot || guildUser.IsWebhook) return;

      var currentUser = (msg.Channel as SocketGuildChannel)?.Guild.CurrentUser;
      if (currentUser == null) return;
      if (!msg.MentionedUsers.Any(u => u.Id == currentUser.Id)) return;

      // History fetching moved into AskGeminiWithContextAsync
      var (answer, emoji) = await AskGeminiWithContextAsync(msg, msg.Author.Username);

      try
      {
        await msg.AddReactionAsync(new Emoji(emoji));
      }
      catch
      {
        await msg.AddReactionAsync(new Emoji("👀"));
      }

      if (answer.Length > 1900) answer = answer[..1900] + "…";
      await msg.ReplyAsync(answer);
    }

    private static async Task<(string Answer, string Emoji)> AskGeminiWithContextAsync(SocketUserMessage msg, string askerName) {
      const string FallbackEmoji = "👀";
      const string NormalModel = "gemini-3.1-flash-lite";
      const string SearchModel = "gemini-2.5-flash";
      const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

      const string SystemPromptJson = """
        You are in the Vita3K Discord server.
        Vita3K is an open-source PlayStation Vita emulator for PC.
        Assume topics relate to Vita3K, PS Vita, or emulation unless clearly otherwise.
        Keep answers short and punchy.
        Never ask follow-up questions. Never suggest continuing the conversation.
        Answer in one shot, done, finito.
        You can only use English. If the question is not in English, respond in English regardless.

        You MUST respond in the following JSON format and nothing else:
        {
          "answer": "your response here",
          "emoji": "single emoji that best reacts to the message"
        }
        """;

      const string SystemPromptSearch = """
        You are in the Vita3K Discord server.
        Vita3K is an open-source PlayStation Vita emulator for PC.
        Assume topics relate to Vita3K, PS Vita, or emulation unless clearly otherwise.
        Keep answers short and punchy.
        Never ask follow-up questions. Never suggest continuing the conversation.
        Answer in one shot, done, finito.
        You can only use English. If the question is not in English, respond in English regardless.
        """;

      try
      {
        // Fetch message history inside the method
        var history = await msg.Channel
            .GetMessagesAsync(msg, Direction.Before, 10)
            .FlattenAsync();

        var historyText = string.Join("\n", history
            .Reverse()
            .Select(m => $"{m.Author.Username}: {m.Content}"));

        var prompt = $"""
            Recent chat history:
            {historyText}

            {askerName} is now asking you: {msg.Content}
            """;

        // Step 1: Classify whether grounding is needed
        var classifyBody = new
        {
          system_instruction = new
          {
            parts = new[] { new { text = "You are a classifier. Reply with only 'SEARCH' or 'NO_SEARCH'." } }
          },
          contents = new[] {
                new { parts = new[] { new { text =
                    $"Does answering this question require up-to-date or real-time or technical information? Question: {msg.Content}" } } }
            }
        };

        var classifyResp = await _httpClient.PostAsync(
            $"{BaseUrl}/{NormalModel}:generateContent?key={GeminiApiKey}",
            new StringContent(JsonSerializer.Serialize(classifyBody), Encoding.UTF8, "application/json")
        );

        var needsSearch = false;
        if (classifyResp.IsSuccessStatusCode)
        {
          using var classifyDoc = JsonDocument.Parse(await classifyResp.Content.ReadAsStringAsync());
          var verdict = classifyDoc.RootElement
              .GetProperty("candidates")[0]
              .GetProperty("content")
              .GetProperty("parts")[0]
              .GetProperty("text")
              .GetString()?.Trim() ?? "";

          needsSearch = verdict.Equals("SEARCH", StringComparison.OrdinalIgnoreCase)
                     || verdict.StartsWith("SEARCH", StringComparison.OrdinalIgnoreCase);
          Console.WriteLine($"[Gemini] needsSearch={needsSearch} ({verdict})");
        }

        // Step 2: Call the appropriate model based on classification
        if (needsSearch)
        {
          var body = new
          {
            system_instruction = new { parts = new[] { new { text = SystemPromptSearch } } },
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            tools = new[] { new { google_search = new { } } }
          };

          var resp = await _httpClient.PostAsync(
              $"{BaseUrl}/{SearchModel}:generateContent?key={GeminiApiKey}",
              new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
          );

          if (!resp.IsSuccessStatusCode)
          {
            var statusCode = (int)resp.StatusCode;
            var err = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[Gemini] Search model {statusCode} (grounding limit?), falling back: {err}");
            needsSearch = false;
          }
          else
          {
            using var searchDoc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var raw = searchDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";

            var searchAnswer = raw.Trim();
            if (searchAnswer.Length > 1900) searchAnswer = searchAnswer[..1900] + "…";
            return (searchAnswer, "🔍");
          }
        }

        if (!needsSearch)
        {
          var body = new
          {
            system_instruction = new { parts = new[] { new { text = SystemPromptJson } } },
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
          };

          var resp = await _httpClient.PostAsync(
              $"{BaseUrl}/{NormalModel}:generateContent?key={GeminiApiKey}",
              new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
          );

          if (!resp.IsSuccessStatusCode)
          {
            var err = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[Gemini] {(int)resp.StatusCode} Error: {err}");
            return ("Seems like the API is taking a nap 😴", FallbackEmoji);
          }

          using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
          var raw = doc.RootElement
              .GetProperty("candidates")[0]
              .GetProperty("content")
              .GetProperty("parts")[0]
              .GetProperty("text")
              .GetString() ?? "";

          var clean = raw.Trim().TrimStart('`');
          if (clean.StartsWith("json")) clean = clean[4..];
          clean = clean.Trim('`').Trim();

          using var parsed = JsonDocument.Parse(clean);
          var answer = parsed.RootElement.GetProperty("answer").GetString() ?? "No response.";
          var emoji = parsed.RootElement.GetProperty("emoji").GetString() ?? FallbackEmoji;
          return (answer, emoji);
        }

        return ("No response.", FallbackEmoji);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Gemini mention error: {ex.Message}");
        return ("The API seems to be having a moment 🤒", FallbackEmoji);
      }
    }

    // ========================
    // Build & Channel Monitors
    // ========================

    private static async Task MonitorNewBuilds(SocketUserMessage msg)
    {
      if (msg.Channel.Name != "github" && msg.Author.ToString() != "GitHub#0000") return;
      var embed = msg.Embeds.FirstOrDefault();

      if (embed != null && embed.Title == "[Vita3K/Vita3K] New release published: continuous")
      {
        var guild = (msg.Channel as SocketGuildChannel).Guild;
        await guild.GetTextChannel(965725409574539274).SendMessageAsync(embed: await GithubClient.GetLatestBuild());
      }
    }

    private static async Task MonitorMediaMessages(SocketUserMessage msg)
    {
      if (msg.Channel.Name == "media" &&
              msg.Attachments.Count == 0 &&
              !msg.Content.Contains("youtube.com") &&
              !msg.Content.Contains("youtu.be") &&
              !msg.Content.Contains("streamable.com") &&
              !msg.Content.Contains("x.com") &&
              !msg.Content.Contains("twitter.com"))
      {
        await msg.DeleteAsync();
      }
    }

    // ========================
    // User Join Handler
    // ========================

    private static async Task HandleUserJoinedAsync(SocketGuildUser j_user)
    {
      if (j_user.IsBot || j_user.IsWebhook) return;
      try
      {
        var dmChannel = await j_user.CreateDMChannelAsync();
        await dmChannel.SendMessageAsync("Welcome to Vita3K server! \n " +
            "Please read the server <#415122640051896321> and <#486173784135696418> thoroughly before posting. \n " + "\n " +
            "For the latest up-to-date guide on game installation and hardware requirements, please visit <https://vita3k.org/quickstart.html>. \n " + "\n " +
            "This emulator is still in it's early stages and some commercial games do not run yet! Feedback is greatly appreciated. \n " +
            "For current issues with the emulator visit the GitHub repo at https://github.com/Vita3K/Vita3K/issues. \n " + "\n" +
            "**__No piracy!__** We do not provide support for pirated games nor do we allow discussions about piracy either.");
      }
      catch (Discord.Net.HttpException ex) when ((int?)ex.DiscordCode == 50278)
      {
        // Ignore users with DMs disabled to keep the log clean
      }
    }

    // ========================
    // Message Entry Point
    // ========================

    // Called by Discord.Net when the bot receives a message
    private static async Task CheckMessage(SocketMessage message)
    {
      if (message is not SocketUserMessage userMessage) return;
      await MonitorNewBuilds(userMessage);
      await MonitorMediaMessages(userMessage);

      if (userMessage.Author is not SocketGuildUser guildUser) return;
      await MonitorImageSpam(userMessage, guildUser);
      await MonitorPiracy(userMessage, guildUser);
      if (message.Channel.Id != 577624167541637158 && !RolesUtils.IsWhitelisted(guildUser)) return; // Only monitor mentions in #bot-spam
      await MonitorMentions(userMessage, guildUser);
    }

    // Initializes the Message Handler, subscribes to events, etc.
    public void Initialize()
    {
      _ = Task.Run(RunCacheCleanupLoop);

      _client.MessageReceived += (msg) => {
        _ = Task.Run(async () => {
          try
          {
            await CheckMessage(msg);
          }
          catch (Exception ex)
          {
            Console.WriteLine($"CheckMessage error: {ex}");
          }
        });
        return Task.CompletedTask;
      };

      _client.UserJoined += (user) => {
        _ = Task.Run(async () => {
          try
          {
            await HandleUserJoinedAsync(user);
          }
          catch (Exception ex)
          {
            Console.WriteLine($"HandleUserJoinedAsync error: {ex}");
          }
        });
        return Task.CompletedTask;
      };
    }

    public MessageHandlingService(IServiceProvider services)
    {
      _client = services.GetRequiredService<DiscordSocketClient>();
      _services = services;
    }
  }
}
