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

    // ========================
    // Piracy Detection
    // ========================
    private static readonly HttpClient _httpClient = new();
    private static readonly string GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new InvalidOperationException("GEMINI_API_KEY environment variable is not set.");
    private static readonly ConcurrentDictionary<ulong, int> _piracyWarningCount = new();

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
                    piracy (illegal downloading, cracking, ROM distribution requests, etc.)
                    of games, movies, or music.

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
          .WithTimestamp(DateTimeOffset.Now)
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
