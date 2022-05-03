using System;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

namespace Vita3KBot.Services {
    public class CommandHandlingService {
        // Config
        private const char Prefix = '-';
        private const bool ShowStackTrace = false;

        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        // Called by Discord.Net when the bot receives a message.
        private async Task HandleCommand(SocketMessage message) {
            if (!(message is SocketUserMessage userMessage)) return;

            var prefixStart = 0;

            if (userMessage.HasCharPrefix(Prefix, ref prefixStart)) {
                // Create Context and Execute Commands
                var context = new SocketCommandContext(_client, userMessage);
                await _commands.ExecuteAsync(context, prefixStart, _services);
            }
        }

        // This event is triggered when a command finishes executing (whether successful or not)
        private async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result) {
            // Handle any errors.
            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand) {
                if (ShowStackTrace && result.Error == CommandError.Exception
                        && result is Discord.Commands.ExecuteResult execution) {
                    await context.Channel.SendMessageAsync(
                            Utils.Code(execution.Exception.Message + "\n\n" + execution.Exception.StackTrace));
                } else {
                    var currentCommand = command.GetValueOrDefault();
                    await context.Channel.SendMessageAsync(
                            "Halt! We've hit an error." + Utils.Code(result.ErrorReason));
                    if (result.ErrorReason == "The input text has too few parameters.") {
                        await context.Channel.SendMessageAsync($"Try `-help {currentCommand.Name}` for the command's usage");
                    }
                }
            }
        }

        // Initializes the Message Handler, subscribe to events, etc.
        public async Task InitializeAsync() {
            _client.MessageReceived += HandleCommand;
            _commands.CommandExecuted += CommandExecutedAsync;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public CommandHandlingService(IServiceProvider services) {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _commands = services.GetRequiredService<CommandService>();
            _services = services;
        }
    }
}
