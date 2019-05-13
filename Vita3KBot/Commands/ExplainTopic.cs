using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace Vita3KBot.Commands {
    public class ExplainTopic : ModuleBase<SocketCommandContext> {
        private static readonly DirectoryInfo ExplanationDirectory = new DirectoryInfo("explanations");
        
        [Command("explain")]
        public async Task Explain([Remainder]string name) {
            var file = ExplanationDirectory.GetFiles().FirstOrDefault(x => x.Name == name + ".txt");
            if (file == null) {
                await ReplyAsync($"No explanation listed for {name}.");
                return;
            }

            var explanation = File.ReadAllText(file.FullName);
            await ReplyAsync(explanation);
        }

        [Command("edit")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AddExplain(string name, [Remainder] string contents) {
            var path = Path.Combine(ExplanationDirectory.FullName, name + ".txt");
            File.WriteAllText(path, contents);
            if (File.Exists(path))
                await ReplyAsync($"Created new explanation for {name}. Run it with `-explain {name}`.");
            else
                await ReplyAsync($"Edited explanation for {name}.");
        }
        
        [Command("help")]
        public async Task Help() {
            await Explain("help");
        }
    }
}