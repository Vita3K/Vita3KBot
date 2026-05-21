using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using Vita3KBot.Commands.Attributes;

namespace Vita3KBot.Commands {
    [Group("probe-role"), RequireModeratorRole]
    public class Debug : ModuleBase<SocketCommandContext> {

        // Get id for a role. Helpful when creating commands that might query, give, remove roles.
        [Command, Name("probe-role")]
        [Summary("Gets the ID for the provided role")]
        public async Task ProbeRole([Remainder, Summary("Role to get ID for")] string roleName) {
            await ReplyAsync(roleName + ": " + Context.Guild.Roles.First(x => x.Name == roleName).Id);
        }
    }
}