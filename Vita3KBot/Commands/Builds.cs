using System.Threading.Tasks;

using APIClients;

using Discord.Commands;

namespace Vita3KBot.Commands {
    [Group("latest"), Alias("download")]
    public class Builds : ModuleBase<SocketCommandContext> {
        [Command, Name("latest")]
        [Summary("Provides a link to Vita3K's current latest build.")]
        private async Task GetBuild()
        {
            await ReplyAsync(embed: await GithubClient.GetLatestBuild());
        }
    }
}