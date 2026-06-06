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

    private record PiracyVerdict(bool IsPiracy, double Confidence, string Reason);

    private const double AutoActionThreshold = 0.90;

    private static async Task MonitorPiracy(SocketUserMessage msg, SocketGuildUser guildUser)
    {
      if (guildUser.IsBot || guildUser.IsWebhook) return;
      if (RolesUtils.IsWhitelisted(guildUser)) return;

      var content = msg.Content;
      if (string.IsNullOrWhiteSpace(content)) return;

      var lower = content.ToLower();
      bool hasTrigger = TriggerKeywords.Any(k => lower.Contains(k));
      if (!hasTrigger) return;

      var verdict = await CheckPiracyWithGeminiAsync(content);
      Console.WriteLine($"[Piracy] is_piracy={verdict.IsPiracy} confidence={verdict.Confidence:F2} reason=\"{verdict.Reason}\" user={guildUser.Username}");

      if (verdict.IsPiracy && verdict.Confidence >= AutoActionThreshold)
      {
        await HandlePiracyViolationAsync(msg, guildUser);
      }
    }

    private static async Task<PiracyVerdict> CheckPiracyWithGeminiAsync(string content)
    {
      try
      {
        var prompt = $$"""
            You are a strict moderator for the Vita3K Discord server.
            Vita3K is a LEGAL open-source PS Vita emulator. Emulation itself is legal and on-topic.

            Classify the message as PIRACY only if it clearly does one of these:
            - requests, offers, or links illegally obtained game files (ROM, ISO, .pkg, decrypted dumps)
            - asks where to download commercial games for free
            - shares/requests license files (.rif, work.bin, act.dat) for games the user does not own

            It is NOT piracy if it is any of:
            - buying games from official stores, or dumping/backing up games the user legally owns
            - general questions about the emulator, official firmware, controllers, performance
            - merely using the words "game" / "download" / "free" / "link" in a legal context
            - statements that discourage or condemn piracy
            - jokes, vague, or ambiguous messages

            Respond ONLY with JSON, no markdown:
            {"is_piracy": true|false, "confidence": 0.0-1.0, "reason": "short reason"}

            Examples:
            "where can I download Persona 4 for free?" -> {"is_piracy": true, "confidence": 1.00, "reason": "asking to download a commercial game for free"}
            "how do I dump my own cartridge?" -> {"is_piracy": false, "confidence": 1.00, "reason": "dumping legally owned game"}
            "this game runs great!" -> {"is_piracy": false, "confidence": 1.00, "reason": "general comment"}
            "How to download attack on titan 2 pls help me" -> {"is_piracy": true, "confidence": 0.90, "reason": "asking to download a commercial game from online"}
            "I need a file .bin / .rif or zRIF key I've reached this point and I can't go any further. I've done everything, but this is what's missing. I would be grateful if anyone could help me in any way." -> {"is_piracy": true, "confidence": 1.00, "reason": "Because they appear to lack technical knowledge, and demanding a license constitutes piracy"}
            "Where can I download the God of War ROMs?" -> {"is_piracy": true, "confidence": 0.95, "reason": "asking to download illegally obtained game files"}
            "where do i download ps vita games" -> {"is_piracy": false, "confidence": 0.95, "reason": "asking to download illegally obtained game files"}
            "Please give me link doenload sao hollow realization support vita3k" -> {"is_piracy": true, "confidence": 1.00, "reason": "asking for illegal game downloads"}
            "question, how do i get games" -> {"is_piracy": false, "confidence": 0.5, "reason": "maybe user is asking about legal ways to obtain games"}
            "How can I find the license" -> {"is_piracy": true, "confidence": 0.95, "reason": "asking for illegal license files"}
            "Free read only memory download dot seven zip" -> {"is_piracy": false, "confidence": 0.90, "reason": "not meaningful comment"}
            "Rom" -> {"is_piracy": false, "confidence": 0.95, "reason": "Just a word"}
            "guys, fix the game on emu, i wanna do another run but in glorious hd" -> {"is_piracy": false, "confidence": 0.95, "reason": "general comment about the emulator"}
            "Hello friends, what exactly does the "NoNpDrm installation failed, deleting data!" error mean and how can it be fixed? I got through trying to install on Windows through a vpk made from a dumped game." -> {"is_piracy": false, "confidence": 0.95, "reason": "asking for help with a technical error, likely related to legally obtained games"}

            Message: "{{content}}"
            """;

        var requestBody = new
        {
          contents = new[] { new { parts = new[] { new { text = prompt } } } },
          generationConfig = new { temperature = 0.0, responseMimeType = "application/json" }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var response = await _httpClient.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={GeminiApiKey}",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode) return new PiracyVerdict(false, 0, "api error");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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
        var isPiracy = parsed.RootElement.GetProperty("is_piracy").GetBoolean();
        var confidence = parsed.RootElement.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0;
        var reason = parsed.RootElement.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
        return new PiracyVerdict(isPiracy, confidence, reason);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Gemini API error: {ex.Message}");
        return new PiracyVerdict(false, 0, "exception");
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
