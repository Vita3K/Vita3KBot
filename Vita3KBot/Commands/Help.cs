using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Vita3KBot;

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
                await ReplyAsync("couldn't find the command you're looking for");
                return;
            }

            bool isAdmin = false;
            if (Context.Channel.GetType().Name != "SocketDMChannel") {
                var user = Context.User as IGuildUser;
                if (user.GuildPermissions.Administrator)
                    isAdmin = true;
            }

            var match = result.Commands.FirstOrDefault();

            EmbedBuilder helpEmbed = new EmbedBuilder();
            helpEmbed.WithTitle("Help");
            helpEmbed.WithColor(Color.Orange);
            helpEmbed.WithDescription($"`{match.Command.Name}`: {match.Command.Summary}");

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
                    if (subcommand.Remarks != "Admin")
                        subcommandsList.Add(subcommand.Name);
                    else if (subcommand.Remarks == "Admin" && isAdmin)
                        subcommandsList.Add(subcommand.Name);
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
            string commands = "";

            bool isAdmin = false;
            if (Context.Channel.GetType().Name != "SocketDMChannel") {
                var user = Context.User as IGuildUser;
                if (user.GuildPermissions.Administrator)
                    isAdmin = true;
            }

            EmbedBuilder helpEmbed = new EmbedBuilder();
            helpEmbed.WithTitle("Help");
            helpEmbed.WithDescription("These are all the available commands, specify a command for more information about it.");
            helpEmbed.WithColor(Color.Orange);
            foreach(var command in _commands.Modules) {
                if (!command.IsSubmodule) {
                    if (command.Remarks != "Admin")
                        commandList.Add($"`{command.Name}`");
                    else if (command.Remarks == "Admin" && isAdmin)
                        commandList.Add($"`{command.Name}`");
                }
            }

            commands = string.Join(", ", commandList);
            helpEmbed.AddField("Commands", commands);
            await ReplyAsync(embed: helpEmbed.Build());
        }
    }
}