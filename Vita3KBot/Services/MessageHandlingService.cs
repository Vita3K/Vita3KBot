using System;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;
using Vita3KBot.Database;

using Microsoft.Extensions.DependencyInjection;
using APIClients;

namespace Vita3KBot.Services {
    public class MessageHandlingService {
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        
        // Called for each user message. Use it to collect stats, or silently observe stuff, etc.
        private static async Task MonitorMessage(SocketUserMessage message) {
            if (!(message.Author is SocketGuildUser user) || message.Author.IsBot) return;
            try {
                using var db = new BotDb();
                if (!RolesUtils.IsModerator(message.Author) && db.blacklistTerms.Any(term => message.Content.Contains(term.BlacklistedText))) {
                    var embed = new EmbedBuilder()
                    .WithTitle("Scam link detected and message deleted")
                    .WithDescription(message.Content)
                    .AddField("Sent by", message.Author.Mention)
                    .AddField("In Channel", message.Channel)
                    .WithColor(Color.Red)
                    .Build();
                    var channel = message.Channel as SocketGuildChannel;
                    await channel.Guild.GetTextChannel(757604199159824385).SendMessageAsync(embed: embed).ConfigureAwait(false);
                    await message.DeleteAsync().ConfigureAwait(false);
                }
            } catch(Exception ex) {
                Console.WriteLine(ex);
            }
            //You can put some monitoring here.
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
        private async Task HandleUserJoinedAsync(SocketGuildUser j_user) {
            if (j_user.IsBot || j_user.IsWebhook) return;
            var dmChannel = await j_user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync("Welcome to Vita3K! \n " +
                "Please read the server <#415122640051896321> and <#486173784135696418> thoroughly before posting. \n " + "\n " +
                "For the latest up-to-date guide on game installation and hardware requirements, please visit <https://vita3k.org/quickstart.html>. \n " + "\n " +
                "This emulator is still in it's early stages and some commercial games do not run yet! Feedback is greatly appreciated. \n " +
                "For current issues with the emulator visit the GitHub repo at https://github.com/Vita3K/Vita3K/issues.");
        }

        // Called by Discord.Net when the bot receives a message.
        private async Task CheckMessage(SocketMessage message) {
            if (!(message is SocketUserMessage userMessage)) return;

            await MonitorMessage(userMessage);
            await MonitorNewBuilds(userMessage);
            await MonitorMediaMessages(userMessage);
        }

        // Initializes the Message Handler, subscribe to events, etc.
        public void Initialize() {
            _client.MessageReceived += CheckMessage;
            _client.UserJoined += HandleUserJoinedAsync;
        }

        public MessageHandlingService(IServiceProvider services) {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
        }
    }
}
