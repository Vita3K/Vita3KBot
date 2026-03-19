using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Vita3KBot.Services
{
    public class CommandHandlingService {
        // Config
        private const char Prefix = '-';
        private const bool ShowStackTrace = false;

        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;

        public CommandHandlingService(IServiceProvider services) {
          _client = services.GetRequiredService<DiscordSocketClient>();
          _commands = services.GetRequiredService<CommandService>();
          _interactions = services.GetRequiredService<InteractionService>();
          _services = services;
        }

        // ── Initialization ───────────────────────────────────────
        public async Task InitializeAsync() {
            // Prefix commands
            _client.MessageReceived += HandlePrefixCommandAsync;
            _commands.CommandExecuted += CommandExecutedAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Slash commands
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.Ready += RegisterSlashCommandsAsync;
            _client.InteractionCreated += HandleInteractionAsync;
        }

        // ── Prefix command handling ──────────────────────────────
        private async Task HandlePrefixCommandAsync(SocketMessage message) {
            if (message is not SocketUserMessage userMessage) return;

            var prefixStart = 0;
            if (userMessage.HasCharPrefix(Prefix, ref prefixStart)) {
                var context = new SocketCommandContext(_client, userMessage);
                await _commands.ExecuteAsync(context, prefixStart, _services);
            }
        }

        // Triggered when a command finishes executing (whether successful or not)
        private async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, Discord.Commands.IResult result) {
            if (result.IsSuccess || result.Error == CommandError.UnknownCommand) return;

            if (ShowStackTrace && result.Error == CommandError.Exception && result is Discord.Commands.ExecuteResult execution) {
                await context.Channel.SendMessageAsync(Utils.Code(execution.Exception.Message + "\n\n" + execution.Exception.StackTrace));
            } else {
                var currentCommand = command.GetValueOrDefault();
                await context.Channel.SendMessageAsync("Halt! We've hit an error." + Utils.Code(result.ErrorReason));
                if (result.ErrorReason == "The input text has too few parameters.") {
                    await context.Channel.SendMessageAsync($"Try `-help {currentCommand.Name}` for the command's usage");
                }
            }
        }

        private const ulong DevGuildId = 1060230577384587404L; // change this to your guild ID for testing, or use RegisterCommandsGloballyAsync() for production

        // ── Slash command handling ───────────────────────────────
        private async Task RegisterSlashCommandsAsync() {
#if DEBUG
            // Delete all global commands
            await _client.Rest.DeleteAllGlobalCommandsAsync();
            Console.WriteLine("Deleted all global commands.");

            // Use RegisterCommandsToGuildAsync(YOUR_GUILD_ID) for instant updates during development
            var commands = await _interactions.RegisterCommandsToGuildAsync(DevGuildId);
            Console.WriteLine($"Registered {commands.Count} slash command(s) to guild {DevGuildId}.");
#else
          // Use RegisterCommandsGloballyAsync() for production (may take up to 1 hour to propagate)
          await _interactions.RegisterCommandsGloballyAsync();
#endif
        }

        private async Task HandleInteractionAsync(SocketInteraction interaction) {
            // Skip select menus, handled separately in Bot.cs
            if (interaction is SocketMessageComponent component &&
                component.Data.CustomId == "update_select") return;

            var context = new SocketInteractionContext(_client, interaction);
            var result = await _interactions.ExecuteCommandAsync(context, _services);

            // Reply with an ephemeral error message visible only to the user
            if (!result.IsSuccess)
                await interaction.RespondAsync($"Halt! We've hit an error.{Utils.Code(result.ErrorReason)}", ephemeral: true);
        }
    }
}
