using System;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;
using Vita3KBot.Database;

using Microsoft.Extensions.DependencyInjection;

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
            //TODO: Put Persona 4 Golden monitoring here.
        }

        // User join event
        private async Task HandleUserJoinedAsync(SocketGuildUser j_user) {
            if (j_user.IsBot || j_user.IsWebhook) return;
            var dmChannel = await j_user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync("Welcome to Vita3k! \n " +
                "Please read the server <#415122640051896321> and <#486173784135696418> thoroughly before posting. \n " + "\n " +
                "For the latest up-to-date guide on game installation and hardware requirements, please visit <https://vita3k.org/quickstart.html> \n " + "\n " +
                "This emulator is still in it's early stages and most commercial games do not run yet! Feedback is greatly appreciated. \n " +
                "For current issues with the emulator visit the GitHub repo at https://github.com/Vita3K/Vita3K/issues");
        }

        // Called by Discord.Net when the bot receives a message.
        private async Task CheckMessage(SocketMessage message) {
            if (!(message is SocketUserMessage userMessage)) return;

            await MonitorMessage(userMessage);
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
