using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using Vita3KBot.Commands;
using Vita3KBot.Utils;

using static Vita3KBot.Utils.Utils;

namespace Vita3KBot.Services
{
  public class CommandHandlingService
  {
    // Config
    private const char Prefix = '-';
    private const bool ShowStackTrace = false;
    private const ulong DevGuildId = 1060230577384587404L; // change this to your guild ID for testing, or use RegisterCommandsGloballyAsync() for production

    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;

    public CommandHandlingService(IServiceProvider services)
    {
      _client = services.GetRequiredService<DiscordSocketClient>();
      _commands = services.GetRequiredService<CommandService>();
      _interactions = services.GetRequiredService<InteractionService>();
      _services = services;
    }

    // ── Initialization ───────────────────────────────────────
    public async Task InitializeAsync()
    {
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
    private async Task HandlePrefixCommandAsync(SocketMessage message)
    {
      if (message is not SocketUserMessage userMessage)
      {
        return;
      }

      var prefixStart = 0;
      if (userMessage.HasCharPrefix(Prefix, ref prefixStart))
      {
        var context = new SocketCommandContext(_client, userMessage);
        // Show typing indicator while the command is processing
        await context.Channel.TriggerTypingAsync();
        await _commands.ExecuteAsync(context, prefixStart, _services);
      }

      // Execute the update only when the first word is in title ID format
      var firstWord = userMessage.Content.Split(' ', 2)[0].ToUpper();
      if (CompatUtils.IsValidTitleId(firstWord))
      {
        var context = new SocketCommandContext(_client, userMessage);
        await context.Channel.TriggerTypingAsync();
        await _commands.ExecuteAsync(context, "update " + userMessage.Content, _services);
      }
    }

    // Triggered when a command finishes executing (whether successful or not)
    private async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, Discord.Commands.IResult result)
    {
      if (result.IsSuccess || result.Error == CommandError.UnknownCommand || result.Error == CommandError.UnmetPrecondition)
      {
        return;
      }

      if (ShowStackTrace && result.Error == CommandError.Exception && result is Discord.Commands.ExecuteResult execution)
      {
        await context.Channel.SendMessageAsync(Code(execution.Exception.Message + "\n\n" + execution.Exception.StackTrace));
      }
      else
      {
        if (command.IsSpecified && result.Error is CommandError.BadArgCount or CommandError.ParseFailed or CommandError.ObjectNotFound or CommandError.MultipleMatches)
        {
          // Wrong or missing parameters: explain what went wrong and show the command's usage
          var cmd = command.Value;
          var embed = HelpUtils.BuildCommandEmbed(new CommandMatch(cmd, cmd.Aliases[0]), RolesUtils.IsWhitelisted(context), RolesUtils.IsModerator(context));
          var usage = $"-{cmd.Aliases[0]} {string.Join(" ", cmd.Parameters.Select(p => p.IsOptional ? $"[{p.Name}]" : $"<{p.Name}>"))}".TrimEnd();
          var reason = result.Error == CommandError.BadArgCount ? "Missing or too many parameters." : result.ErrorReason;
          embed.WithTitle("Invalid usage").WithDescription($"{reason}\nUsage: `{usage}`\n\n{cmd.Summary}");
          await context.Channel.SendMessageAsync(embed: embed.Build());
          return;
        }

        var message = "Halt! We've hit an error.";
        if (result is Discord.Commands.ExecuteResult { Exception: { } ex })
        {
          var cause = ex is CommandException { InnerException: { } inner } ? inner : ex;
          Console.WriteLine(ex);
          message += Code($"{cause.GetType().Name}: {cause.Message}");
        }
        else
        {
          message += Code(result.ErrorReason);
        }

        if (command.IsSpecified)
        {
          message += $"\nTry `-help {command.Value.Aliases[0]}` for the command's usage.";
        }

        await context.Channel.SendMessageAsync(message);
      }
    }

    // ── Slash command handling ───────────────────────────────
    private async Task RegisterSlashCommandsAsync()
    {
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

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
      // Skip select menus, handled separately in Bot.cs
      if (interaction is SocketMessageComponent component &&
        component.Data.CustomId == "update_select")
      {
        return;
      }

      var context = new SocketInteractionContext(_client, interaction);
      var result = await _interactions.ExecuteCommandAsync(context, _services);

      // Reply with an ephemeral error message visible only to the user
      if (!result.IsSuccess)
      {
        await interaction.FollowupAsync($"Halt! We've hit an error.{Code(result.ErrorReason)}", ephemeral: true);
      }
    }
  }
}
