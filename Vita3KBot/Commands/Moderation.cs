using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Vita3KBot.Commands {
    public class ModerationModule : ModuleBase<SocketCommandContext> {

        [Command("kick")]
        [RequireBotPermission(GuildPermission.KickMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task Kick([Remainder] SocketGuildUser member) {
            if (member.GuildPermissions.Administrator) {
                await ReplyAsync("Can't kick an admin :p");
                return;
            }
            await Context.Channel.SendMessageAsync($"Are you sure you want to kick {member}. (reply yes to confirm, or ignore to abort)");
            await Task.Delay(10000);
            var messages = await Context.Channel.GetMessagesAsync(Context.Message as IMessage, Direction.After, 10).FlattenAsync();
            foreach (var message in messages) {
                if (message.Author == Context.Message.Author && message.Content.ToLower() == "yes") {
                    await member.KickAsync();
                    await ReplyAsync($"Successfully kicked {member}");
                    return;
                } else {
                    await ReplyAsync("Aborting kick...");
                    return;
                }
            }
            await ReplyAsync($"Failed to kick {member}");
        }

        [Command("ban")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task Ban([Remainder] SocketGuildUser member) {
            if (member.GuildPermissions.Administrator) {
                await ReplyAsync("Can't ban an admin :p");
                return;
            }
            await Context.Channel.SendMessageAsync($"Are you sure you want to ban {member}. (reply yes to confirm, or ignore to abort)");
            await Task.Delay(10000);
            var messages = await Context.Channel.GetMessagesAsync(Context.Message as IMessage, Direction.After, 10).FlattenAsync();
            foreach (var message in messages)
            {
                if (message.Author == Context.Message.Author && message.Content.ToLower() == "yes")
                {
                    await Context.Guild.AddBanAsync(member);
                    await ReplyAsync($"Successfully banned {member}");
                    return;
                }
                else
                {
                    await ReplyAsync("Aborting ban...");
                    return;
                }
            }
            await ReplyAsync($"Failed to ban user {member}");
        }

        [Command("delete")]
        [Alias("remove", "del")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        private async Task Delete(int numOfMsg)
        {
            var messages = await Context.Channel.GetMessagesAsync(numOfMsg + 1).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
        }
    }
}
