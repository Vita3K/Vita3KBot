using System.Threading.Tasks;

using APIClients;

using Discord.Commands;

namespace Vita3KBot.Commands {
    public class Builds : ModuleBase<SocketCommandContext> {
        [Command("latest")]
        [Alias("download")]
        private async Task App()
        {
            await ReplyAsync(embed: await AppveyorClient.GetLatestBuild());
        }
    }
}