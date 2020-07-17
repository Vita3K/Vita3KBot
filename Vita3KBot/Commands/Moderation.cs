using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Vita3KBot.Commands.Attributes;

namespace Vita3KBot.Commands {
    [Group("delete"), Alias("remove", "del"), RequireModeratorRole]
    public class ModerationModule : ModuleBase<SocketCommandContext> {

        [Command, Name("delete")]
        [Summary("Deletes the last _numberOfMessages_ from the current channel")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        private async Task Delete([Summary("Number of messages to delete")] int numberOfMessages)
        {
            var messages = await Context.Channel.GetMessagesAsync(numberOfMessages + 1).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
        }
    }
}
