using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Commands;

namespace Vita3KBot.Commands {
    [Group("help")]
    public class HelpModule : ModuleBase<SocketCommandContext> {
        private readonly CommandService _commands;
        public HelpModule(CommandService commands) {
            _commands = commands;
        }

        [Command, Name("help")]
        [Summary("Lists all commands.")]
        public async Task Help([Remainder, Summary("Name of command")]string command) {
            var result = _commands.Search(command);
            if (!result.IsSuccess) {
                await ReplyAsync("Couldn't find the command you're looking for.");
                return;
            }

            var match = result.Commands.FirstOrDefault();

            var whitelisted = RolesUtils.IsWhitelisted(Context, Context.Guild);
            var moderator = RolesUtils.IsModerator(Context, Context.Guild);

            EmbedBuilder helpEmbed = new EmbedBuilder()
            .WithTitle("Help")
            .WithColor(Color.Orange)
            .WithDescription($"`{match.Command.Name}`: {match.Command.Summary}");

            if (match.Command.Aliases.Count > 1)
                helpEmbed.AddField("Aliases", $"`{string.Join(", ", match.Command.Aliases)}`");
            if (match.Command.Parameters.Count > 0) {
                var parameters = new StringBuilder();
                var tempParams = new StringBuilder();
                foreach(var param in match.Command.Parameters) {
                    tempParams.Append($"[{param.Name}]").Append(" ");
                }
                parameters.Append($"`{match.Command.Module.Name} {tempParams}`");
                foreach(var param in match.Command.Parameters) {
                    parameters.AppendLine();
                    parameters.Append($"`{param.Name} ({param.Type.Name})`: {param.Summary}");
                }
                helpEmbed.AddField("Parameters", parameters);
            }
            if (match.Command.Module.Submodules.Count > 0) {
                List<string> subcommandsList = new List<string>();
                string subcommands = "";

                foreach(var subcommand in match.Command.Module.Submodules) {
                    if (!CommandsUtils.CommandRequiresModerator(subcommand) && !CommandsUtils.CommandRequiresWhitelistedRole(subcommand)) {
                        subcommandsList.Add($"{subcommand.Name}");
                    }
                    else if (whitelisted || moderator) {
                        if (CommandsUtils.CommandRequiresModerator(subcommand) && moderator) {
                            System.Console.WriteLine("Mod command");
                            System.Console.WriteLine(subcommand.Name);
                            subcommandsList.Add($"{subcommand.Name}");
                        }
                        if (CommandsUtils.CommandRequiresWhitelistedRole(subcommand) && whitelisted) {
                            System.Console.WriteLine("Whitelisted role command");
                            System.Console.WriteLine(subcommand.Name);
                            subcommandsList.Add($"{subcommand.Name}");
                        }
                    }
                }
                subcommands = string.Join(", ", subcommandsList);
                helpEmbed.AddField("Subcommands", $"`{subcommands}`");
            }
            if (match.Command.Module.IsSubmodule) {
                helpEmbed.AddField("Parent command", $"`{match.Command.Module.Parent.Name}`");
            }

            await ReplyAsync(embed: helpEmbed.Build());
        }

        [Command]
        public async Task Help() {
            List<string> commandList = new List<string>();
            List<string> commandPreconditions = new List<string>();
            string commands = "";

            var whitelisted = RolesUtils.IsWhitelisted(Context, Context.Guild);
            var moderator = RolesUtils.IsModerator(Context, Context.Guild);

            EmbedBuilder helpEmbed = new EmbedBuilder()
            .WithTitle("Help")
            .WithDescription("These are all the available commands, specify a command for more information about it.")
            .WithColor(Color.Orange);
            foreach(var command in _commands.Modules) {
                if (!command.IsSubmodule) {
                    if (!CommandsUtils.CommandRequiresModerator(command) && !CommandsUtils.CommandRequiresWhitelistedRole(command)) {
                        commandList.Add($"`{command.Name}`");
                    }
                    else if (whitelisted || moderator) {
                        if (CommandsUtils.CommandRequiresModerator(command) && moderator) {
                            System.Console.WriteLine("Mod command");
                            System.Console.WriteLine(command.Name);
                            commandList.Add($"`{command.Name}`");
                        }
                        if (CommandsUtils.CommandRequiresWhitelistedRole(command) && whitelisted) {
                            System.Console.WriteLine("Whitelisted role command");
                            System.Console.WriteLine(command.Name);
                            commandList.Add($"`{command.Name}`");
                        }
                    }
                }
            }

            commands = string.Join(", ", commandList);
            helpEmbed.AddField("Commands", commands);
            await ReplyAsync(embed: helpEmbed.Build());
        }
    }
}