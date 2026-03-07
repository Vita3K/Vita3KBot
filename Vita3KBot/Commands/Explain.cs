using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using DC = Discord.Commands;

namespace Vita3KBot.Commands {

    // ── Shared logic ─────────────────────────────────────────────

    internal static class ExplainUtils {
        internal static readonly DirectoryInfo ExplanationDirectory = new DirectoryInfo("explanations");

        internal static Embed BuildListEmbed() {
            var explanations = ExplanationDirectory.GetFiles()
                .Select(f => $"`{f.Name.Split(".")[0]}`");
            return new EmbedBuilder()
                .WithTitle("All explanations")
                .WithColor(Color.Orange)
                .WithDescription(string.Join(", ", explanations))
                .Build();
        }

        internal static string GetExplanation(string topic) {
            var file = ExplanationDirectory.GetFiles().FirstOrDefault(x => x.Name == topic + ".txt");
            return file == null ? null : File.ReadAllText(file.FullName);
        }
    }

    // ── Prefix commands ──────────────────────────────────────────

    [DC.Group("explain")]
    public class ExplainPrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Group("list")]
        public class List : DC.ModuleBase<DC.SocketCommandContext> {
            [DC.Command, DC.Name("list"), DC.Priority(1)]
            [DC.Summary("Lists all topics/terms that can be explained")]
            public async Task ExplainList()
                => await ReplyAsync(embed: ExplainUtils.BuildListEmbed());
        }

        [DC.Command, DC.Name("explain"), DC.Priority(0)]
        [DC.Summary("Explains a topic or a term")]
        public async Task Explain([DC.Remainder, DC.Summary("Topic to explain")] string topic) {
            var explanation = ExplainUtils.GetExplanation(topic);
            await ReplyAsync(explanation ?? $"No explanation listed for `{topic}`.");
        }
    }

    // ── Slash commands ───────────────────────────────────────────

    public class ExplainSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("explain", "Explains a topic or a term")]
        public async Task Explain(
                [Discord.Interactions.Summary("topic", "Topic to explain")] string topic) {
            var explanation = ExplainUtils.GetExplanation(topic);
            await RespondAsync(explanation ?? $"No explanation listed for `{topic}`.", ephemeral: explanation == null);
        }

        [SlashCommand("explain-list", "Lists all topics/terms that can be explained")]
        public async Task ExplainList()
            => await RespondAsync(embed: ExplainUtils.BuildListEmbed());
    }
}
