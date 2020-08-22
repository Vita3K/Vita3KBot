using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Addons.Interactive;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;
using Victoria;

namespace Vita3KBot {
    public class MessageHandler {
        // Config
        private const char Prefix = '-';
        private const bool ShowStackTrace = true;

        private readonly DiscordSocketClient _client;
        
        private readonly CommandService _commands;
        private readonly ServiceProvider _services;
        
        // Called for each user message. Use it to collect stats, or silently observe stuff, etc.
        private static async Task MonitorMessage(SocketUserMessage message) {
            if (!(message.Author is SocketGuildUser user) || message.Author.IsBot) return;
            
            //TODO: Put Persona 4 Golden monitoring here.
        }

        // User join event
        private async Task HandleUserJoinedAsync(SocketGuildUser j_user) {
            if (j_user.IsBot || j_user.IsWebhook) return;
            var dmChannel = await j_user.GetOrCreateDMChannelAsync();
            await dmChannel.SendMessageAsync("Welcome to Vita3k! \n " +
                "Please read the server <#415122640051896321> and <#486173784135696418> thoroughly before posting. \n " + "\n " +
                "For the latest up-to-date guide on game installation and hardware requirements, please visit <https://vita3k.org/quickstart.html> \n " + "\n " +
                "This emulator is still in it's early stages and most commercial games do not run yet! Feedback is greatly appreciated. \n " +
                "For current issues with the emulator visit the GitHub repo at https://github.com/Vita3K/Vita3K/issues");
        }

        // Called by Discord.Net when it wants to log something.
        private static Task Log(LogMessage message) {
            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }
        
        // Called by Discord.Net when the bot receives a message.
        private async Task CheckMessage(SocketMessage message) {
            if (!(message is SocketUserMessage userMessage)) return;

            await MonitorMessage(userMessage);

            var prefixStart = 0;

            if (userMessage.HasCharPrefix(Prefix, ref prefixStart)) {
                // Create Context and Execute Commands
                var context = new SocketCommandContext(_client, userMessage);
                var result = await _commands.ExecuteAsync(context, prefixStart, _services);
                
                // Handle any errors.
                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand) {
                    if (ShowStackTrace && result.Error == CommandError.Exception
                                       && result is ExecuteResult execution) {
                        await userMessage.Channel.SendMessageAsync(
                            Utils.Code(execution.Exception.Message + "\n\n" + execution.Exception.StackTrace));
                    } else {
                        var currentCommand = _commands.Commands.Where(s => {
                            return userMessage.Content.Contains(s.Name);
                        }).First();
                        await userMessage.Channel.SendMessageAsync(
                            "Halt! We've hit an error." + Utils.Code(result.ErrorReason));
                        if (result.ErrorReason == "The input text has too few parameters.") {
                            await userMessage.Channel.SendMessageAsync($"Try `-help {currentCommand.Name}` for the command's usage");
                        }
                    }
                }
            }
        }

        // Initializes the Message Handler, subscribe to events, etc.
        public async Task Init() {
            _client.Log += Log;
            _client.MessageReceived += CheckMessage;
            _client.UserJoined += HandleUserJoinedAsync;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }
        
        public MessageHandler(DiscordSocketClient client) {
            _client = client;
            
            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton(new InteractiveService(_client))
                .AddLavaNode(x => {
                    x.SelfDeaf = false;
                })
                .BuildServiceProvider();
        }
    }
}