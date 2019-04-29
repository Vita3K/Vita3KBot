using System;
using System.Linq;
using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;

namespace Vita3KBot.Commands {
    public class Debug : ModuleBase<SocketCommandContext> {
        
        // Get id for a role. Helpful when creating commands that might query, give, remove roles.
        [Command("probe-role")]
        public async Task ProbeRole([Remainder] string roleName) {
            await ReplyAsync(roleName + ": " + Context.Guild.Roles.First(x => x.Name == roleName).Id);
        }
    }
}