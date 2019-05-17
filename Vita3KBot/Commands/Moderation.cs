using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Vita3KBot.Commands {
    public class ModerationModule : ModuleBase<SocketCommandContext> {

        [Command("delete"), Alias("remove", "del")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        private async Task Delete(int numOfMsg)
        {
            var messages = await Context.Channel.GetMessagesAsync(numOfMsg + 1).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
        }
    }
}
