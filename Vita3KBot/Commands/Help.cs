using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using DC = Discord.Commands;

namespace Vita3KBot.Commands {

    // ── Prefix command ───────────────────────────────────────────

    [DC.Group("help")]
    public class HelpPrefix : DC.ModuleBase<DC.SocketCommandContext> {
        private readonly DC.CommandService _commands;

        public HelpPrefix(DC.CommandService commands) {
            _commands = commands;
        }

        [DC.Command, DC.Name("help")]
        [DC.Summary("Lists all commands.")]
        public async Task Help([DC.Remainder, DC.Summary("Name of command")] string command) {
            var result = _commands.Search(command);
            if (!result.IsSuccess) {
                await ReplyAsync("Couldn't find the command you're looking for.");
                return;
            }
            var match = result.Commands.FirstOrDefault();
            var whitelisted = RolesUtils.IsWhitelisted(Context, Context.Guild);
            var moderator = RolesUtils.IsModerator(Context, Context.Guild);
            await ReplyAsync(embed: HelpUtils.BuildCommandEmbed(match, whitelisted, moderator).Build());
        }

        [DC.Command]
        public async Task Help() {
            var whitelisted = RolesUtils.IsWhitelisted(Context, Context.Guild);
            var moderator = RolesUtils.IsModerator(Context, Context.Guild);
            await ReplyAsync(embed: HelpUtils.BuildListEmbed(_commands.Modules, whitelisted, moderator).Build());
        }
    }

    // ── Slash command ────────────────────────────────────────────

    public class HelpSlash : InteractionModuleBase<SocketInteractionContext> {
        private readonly DC.CommandService _commands;

        public HelpSlash(DC.CommandService commands) {
            _commands = commands;
        }

        [SlashCommand("help", "Lists all commands, or shows details about a specific command.")]
        public async Task Help(
                [Discord.Interactions.Summary("command", "Name of command (leave empty for full list)")] string command = "") {
            // RolesUtils requires a DC.ICommandContext, so we check roles via the raw socket user instead
            var whitelisted = RolesUtils.IsWhitelisted(Context.User);
            var moderator = RolesUtils.IsModerator(Context.User);

            if (string.IsNullOrWhiteSpace(command)) {
                await RespondAsync(embed: HelpUtils.BuildListEmbed(_commands.Modules, whitelisted, moderator).Build());
                return;
            }

            var result = _commands.Search(command);
            if (!result.IsSuccess) {
                await RespondAsync("Couldn't find the command you're looking for.", ephemeral: true);
                return;
            }

            var match = result.Commands.FirstOrDefault();
            await RespondAsync(embed: HelpUtils.BuildCommandEmbed(match, whitelisted, moderator).Build());
        }
    }

    // ── Shared embed builders ────────────────────────────────────

    internal static class HelpUtils {
        internal static EmbedBuilder BuildCommandEmbed(DC.CommandMatch match, bool whitelisted, bool moderator) {
            var embed = new EmbedBuilder()
                .WithTitle("Help")
                .WithColor(Color.Orange)
                .WithDescription($"`{match.Command.Name}`: {match.Command.Summary}");

            if (match.Command.Aliases.Count > 1)
                embed.AddField("Aliases", $"`{string.Join(", ", match.Command.Aliases)}`");

            if (match.Command.Parameters.Count > 0) {
                var parameters = new StringBuilder();
                var tempParams = new StringBuilder();
                foreach (var param in match.Command.Parameters)
                    tempParams.Append($"[{param.Name}] ");

                parameters.Append($"`{match.Command.Module.Name} {tempParams}`");
                foreach (var param in match.Command.Parameters)
                    parameters.AppendLine().Append($"`{param.Name} ({param.Type.Name})`: {param.Summary}");

                embed.AddField("Parameters", parameters);
            }

            if (match.Command.Module.Submodules.Count > 0) {
                var subcommandsList = new List<string>();
                foreach (var sub in match.Command.Module.Submodules) {
                    if (!CommandsUtils.CommandRequiresModerator(sub) && !CommandsUtils.CommandRequiresWhitelistedRole(sub)) {
                        subcommandsList.Add(sub.Name);
                    } else if (moderator && CommandsUtils.CommandRequiresModerator(sub)) {
                        subcommandsList.Add(sub.Name);
                    } else if (whitelisted && CommandsUtils.CommandRequiresWhitelistedRole(sub)) {
                        subcommandsList.Add(sub.Name);
                    }
                }
                embed.AddField("Subcommands", $"`{string.Join(", ", subcommandsList)}`");
            }

            if (match.Command.Module.IsSubmodule)
                embed.AddField("Parent command", $"`{match.Command.Module.Parent.Name}`");

            return embed;
        }

        internal static EmbedBuilder BuildListEmbed(IEnumerable<DC.ModuleInfo> modules, bool whitelisted, bool moderator) {
            var commandList = new List<string>();
            foreach (var module in modules) {
                if (module.IsSubmodule) continue;
                if (!CommandsUtils.CommandRequiresModerator(module) && !CommandsUtils.CommandRequiresWhitelistedRole(module)) {
                    commandList.Add($"`{module.Name}`");
                } else if (moderator && CommandsUtils.CommandRequiresModerator(module)) {
                    commandList.Add($"`{module.Name}`");
                } else if (whitelisted && CommandsUtils.CommandRequiresWhitelistedRole(module)) {
                    commandList.Add($"`{module.Name}`");
                }
            }

            return new EmbedBuilder()
                .WithTitle("Help")
                .WithDescription("These are all the available commands, specify a command for more information about it.")
                .WithColor(Color.Orange)
                .AddField("Commands", string.Join(", ", commandList));
        }
    }
}
