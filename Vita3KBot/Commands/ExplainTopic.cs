using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Commands;

namespace Vita3KBot.Commands {
    [Group("explain")]
    public class ExplainTopic : ModuleBase<SocketCommandContext> {
        private static readonly DirectoryInfo ExplanationDirectory = new DirectoryInfo("explanations");

        [Command("list"), Priority(1)] // higher number takes priority
        public async Task ExplainList() {
            List<string> explanations = new List<string>();
            foreach (var explanation in ExplanationDirectory.GetFiles()) {
                explanations.Add(explanation.Name.Split(".")[0]);
            }
            var ExplainListEmbed = new EmbedBuilder()
            .WithTitle("All explanations")
            .WithColor(Color.Orange)
            .WithDescription($"`{string.Join(", ", explanations)}`")
            .Build();
            await ReplyAsync(embed: ExplainListEmbed);
        }

        [Command("edit"), Priority(1)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AddExplain(string name, [Remainder]string contents) {
            var path = Path.Combine(ExplanationDirectory.FullName, name + ".txt");
            if (!File.Exists(path)) {
                File.WriteAllText(path, contents);
                await ReplyAsync($"Created new explanation for `{name}`. Run it with `-explain {name}`.");
            }
            else {
                File.WriteAllText(path, contents);
                await ReplyAsync($"Edited explanation for {name}.");
            }
        }

        [Command("delete"), Priority(1)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeleteExplanation([Remainder]string explanation) {
            var file = ExplanationDirectory.GetFiles().FirstOrDefault(f => f.Name == explanation + ".txt");
            if (file == null) {
                await ReplyAsync($"`{explanation}` doesn't exist, check your spelling.");
                return;
            }
            file.Delete();
            await ReplyAsync($"Successfully deleted `{explanation}`.");
        }

        [Command, Priority(0)]
        public async Task Explain([Remainder]string name) {
            var file = ExplanationDirectory.GetFiles().FirstOrDefault(x => x.Name == name + ".txt");
            if (file == null) {
                await ReplyAsync($"No explanation listed for {name}.");
            return;
        }
        var explanation = File.ReadAllText(file.FullName);
        await ReplyAsync(explanation);
        }

        [Command("help")]
        public async Task Help() {
            await Explain("help");
        }
    }
}