using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Commands;
using Vita3KBot.Commands.Attributes;

namespace Vita3KBot.Commands {
    [Group("explain")]
    public class ExplainModule : ModuleBase<SocketCommandContext> {
        private static readonly DirectoryInfo ExplanationDirectory = new DirectoryInfo("explanations");

        [Group("list")]
        public class List : ModuleBase<SocketCommandContext> {
            [Command, Name("list"), Priority(1)] // higher number takes priority
            [Summary("Lists all topics/terms that can be explained")]
            public async Task ExplainList() {
                List<string> explanations = new List<string>();
                foreach (var explanation in ExplanationDirectory.GetFiles()) {
                    explanations.Add($"`{explanation.Name.Split(".")[0]}`");
                }
                var ExplainListEmbed = new EmbedBuilder()
                .WithTitle("All explanations")
                .WithColor(Color.Orange)
                .WithDescription(string.Join(", ", explanations))
                .Build();
                await ReplyAsync(embed: ExplainListEmbed);
            }
        }

        [Group("edit"), RequireWhitelistedRole]
        public class Add : ModuleBase<SocketCommandContext> {
            [Command, Name("edit"), Priority(1)]
            [Summary("Adds/edits a topic's/term's explanation.")]
            public async Task AddExplain([Summary("Topic name")]string topic, [Remainder, Summary("Topic explanation")]string contents) {
                var path = Path.Combine(ExplanationDirectory.FullName, topic + ".txt");
                if (!File.Exists(path)) {
                    File.WriteAllText(path, contents);
                    await ReplyAsync($"Created new explanation for `{topic}`. Run it with `-explain {topic}`.");
                }
                else {
                    File.WriteAllText(path, contents);
                    await ReplyAsync($"Edited explanation for {topic}.");
                }
            }
        }

        [Group("delete"), RequireWhitelistedRole]
        public class Delete : ModuleBase<SocketCommandContext> {
            [Command, Name("delete"), Priority(1)]
            [Summary("Deletes the chosen topic/term")]
            public async Task DeleteExplanation([Remainder, Summary("Topic to delete")]string topic) {
                var file = ExplanationDirectory.GetFiles().FirstOrDefault(f => f.Name == topic + ".txt");
                if (file == null) {
                    await ReplyAsync($"`{topic}` doesn't exist, check your spelling.");
                    return;
                }
                file.Delete();
                await ReplyAsync($"Successfully deleted `{topic}`.");
            }
        }

        [Command, Name("explain"), Priority(0)]
        [Summary("Explains a topic or a term")]
        public async Task Explain([Remainder, Summary("Topic to explain")]string topic) {
            var file = ExplanationDirectory.GetFiles().FirstOrDefault(x => x.Name == topic + ".txt");
            if (file == null) {
                await ReplyAsync($"No explanation listed for `{topic}`.");
            return;
        }
        var explanation = File.ReadAllText(file.FullName);
        await ReplyAsync(explanation);
        }
    }
}