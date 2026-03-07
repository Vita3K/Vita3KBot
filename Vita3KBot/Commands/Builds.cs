using System.Threading.Tasks;
using APIClients;
using Discord.Commands;
using Discord.Interactions;
using DC = Discord.Commands;

namespace Vita3KBot.Commands
{

  // ── Prefix command ───────────────────────────────────────────

  [DC.Group("latest"), DC.Alias("download")]
  public class BuildsPrefix : DC.ModuleBase<DC.SocketCommandContext> {
    [DC.Command, DC.Name("latest")]
    [DC.Summary("Provides a link to Vita3K's current latest build.")]
    private async Task GetBuild()
        => await ReplyAsync(embed: await GithubClient.GetLatestBuild());
  }

  // ── Slash command ────────────────────────────────────────────

  public class BuildsSlash : InteractionModuleBase<SocketInteractionContext> {
    [SlashCommand("latest", "Provides a link to Vita3K's current latest build.")]
    private async Task GetBuild()
        => await RespondAsync(embed: await GithubClient.GetLatestBuild());
  }
}
