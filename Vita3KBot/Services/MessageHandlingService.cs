using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Discord;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;
using APIClients;

namespace Vita3KBot.Services {
    public class MessageHandlingService {
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
 
        // spam detection
        private static readonly ConcurrentDictionary<(ulong UserId, string Hash), List<(ulong ChannelId, ulong MessageId, DateTime PostedAt)>>
            _imagePostLog = new();
 
        private const int SpamChannelThreshold = 3;   // Number of channels before applying spam role
        private static readonly TimeSpan SpamWindow = TimeSpan.FromMinutes(1); // Detection time window
        private static readonly TimeSpan CacheCleanupInterval = TimeSpan.FromMinutes(5); // How often to sweep stale cache entries
 
        private static string GetImageHash(IAttachment attachment) =>
            $"{attachment.Filename}:{attachment.Size}";

        // Periodically removes stale entries from the image post cache.
        private static async Task RunCacheCleanupLoop() {
            while (true) {
                await Task.Delay(CacheCleanupInterval);
                var cutoff = DateTime.UtcNow - SpamWindow;
                foreach (var key in _imagePostLog.Keys.ToList()) {
                    if (!_imagePostLog.TryGetValue(key, out var posts)) continue;
                    lock (posts) {
                        posts.RemoveAll(p => p.PostedAt < cutoff);
                        if (posts.Count == 0)
                            _imagePostLog.TryRemove(key, out _);
                    }
                }
            }
        }
 
        private static async Task MonitorImageSpam(SocketUserMessage msg, SocketGuildUser guildUser) {
            if (msg.Attachments.Count == 0) return;
            // Ignore bots, webhooks and administrators
            if (guildUser.IsBot || guildUser.IsWebhook) return;
            if (guildUser.GuildPermissions.Administrator) return;
 
            var now = DateTime.UtcNow;
 
            foreach (var attachment in msg.Attachments) {
                // Only process image files
                var ext = System.IO.Path.GetExtension(attachment.Filename).ToLower();
                if (ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp")) continue;
 
                var key = (guildUser.Id, GetImageHash(attachment));
 
                var posts = _imagePostLog.GetOrAdd(key, _ => new List<(ulong, ulong, DateTime)>());
 
                lock (posts) {
                    // Remove entries outside the time window
                    posts.RemoveAll(p => now - p.PostedAt > SpamWindow);
                    // Record this post
                    posts.Add((msg.Channel.Id, msg.Id, now));
 
                    var distinctChannels = posts.Select(p => p.ChannelId).Distinct().Count();
                    if (distinctChannels < SpamChannelThreshold) return;
                }
 
                // Threshold reached — apply spam action
                await ExecuteImageSpamAction(guildUser, posts.ToList(), msg);
                return;
            }
        }
 
        private static async Task ExecuteImageSpamAction(
                SocketGuildUser user,
                List<(ulong ChannelId, ulong MessageId, DateTime PostedAt)> posts,
                SocketUserMessage triggerMsg) {
 
            var guild = user.Guild;
 
            // 1. Delete all detected posts
            foreach (var (channelId, messageId, _) in posts) {
                try {
                    if (guild.GetTextChannel(channelId) is { } ch)
                        await ch.DeleteMessageAsync(messageId);
                } catch {
                    // Ignore deletion failures and continue
                }
            }
 
            // 2. Kick user
            try {
                var guildUser = user as IGuildUser ?? guild.GetUser(user.Id);
                if (guildUser != null)
                    await guildUser.KickAsync($"Image spam: same image posted to {SpamChannelThreshold}+ channels within {SpamWindow.TotalMinutes} minute(s).");
                else
                    Console.WriteLine($"Could not resolve guild user {user.Id} for kick.");
            } catch {
                Console.WriteLine($"Failed to kick user {user.Id} from guild {guild.Id}.");
            }
 
            // 3. Notify user via DM
            try {
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync(
                    $"⚠️ **You have been kicked for spam.**\n" +
                    $"Reason: The same image was posted across multiple channels in a short period of time.\n" +
                    $"If you believe this is a mistake, please contact a server moderator.");
            } catch {
                Console.WriteLine($"Could not send DM to user {user.Id}: DMs may be disabled.");
            }
 
            // 4. Log to moderation channel
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

        private static async Task MonitorNewBuilds(SocketUserMessage msg) {
            if (msg.Channel.Name != "github" && msg.Author.ToString() != "GitHub#0000") return;
            var embed = msg.Embeds.FirstOrDefault();

            if (embed != null && embed.Title == "[Vita3K/Vita3K] New release published: continuous") {
                var guild = (msg.Channel as SocketGuildChannel).Guild;
                await guild.GetTextChannel(965725409574539274).SendMessageAsync(embed: await GithubClient.GetLatestBuild());
            }
        }

        private static async Task MonitorMediaMessages(SocketUserMessage msg) {
            if (msg.Channel.Name == "media" &&
                    msg.Attachments.Count == 0 &&
                    !msg.Content.Contains("youtube.com") &&
                    !msg.Content.Contains("youtu.be") &&
                    !msg.Content.Contains("streamable.com") &&
                    !msg.Content.Contains("x.com") &&
                    !msg.Content.Contains("twitter.com")) {
                await msg.DeleteAsync();
            }
        }

        // User join event
        private static async Task HandleUserJoinedAsync(SocketGuildUser j_user) {
            if (j_user.IsBot || j_user.IsWebhook) return;
            try {
                var dmChannel = await j_user.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync("Welcome to Vita3K! \n " +
                    "Please read the server <#415122640051896321> and <#486173784135696418> thoroughly before posting. \n " + "\n " +
                    "For the latest up-to-date guide on game installation and hardware requirements, please visit <https://vita3k.org/quickstart.html>. \n " + "\n " +
                    "This emulator is still in it's early stages and some commercial games do not run yet! Feedback is greatly appreciated. \n " +
                    "For current issues with the emulator visit the GitHub repo at https://github.com/Vita3K/Vita3K/issues.");
            } catch (Discord.Net.HttpException ex) when ((int?)ex.DiscordCode == 50278) {
                // To keep the log clean, ignore users with DMs disabled
            }
        }

        // Called by Discord.Net when the bot receives a message.
        private static async Task CheckMessage(SocketMessage message) {
            if (message is not SocketUserMessage userMessage) return;
            await MonitorNewBuilds(userMessage);
            await MonitorMediaMessages(userMessage);

            if (userMessage.Author is not SocketGuildUser guildUser) return;
            await MonitorImageSpam(userMessage, guildUser);
        }

        // Initializes the Message Handler, subscribe to events, etc.
        public void Initialize() {
            _ = Task.Run(RunCacheCleanupLoop);

            _client.MessageReceived += (msg) => {
                _ = Task.Run(async () => {
                    try {
                        await CheckMessage(msg);
                    } catch (Exception ex) {
                        Console.WriteLine($"CheckMessage error: {ex}");
                    }
                });
                return Task.CompletedTask;
            };

            _client.UserJoined += (user) => {
                _ = Task.Run(async () => {
                    try {
                        await HandleUserJoinedAsync(user);
                    } catch (Exception ex) {
                        Console.WriteLine($"HandleUserJoinedAsync error: {ex}");
                    }
                });
                return Task.CompletedTask;
            };
        }

        public MessageHandlingService(IServiceProvider services) {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
        }
    }
}
