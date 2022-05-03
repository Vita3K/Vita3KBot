using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord.Commands;
using Vita3KBot.Commands.Attributes;
using Vita3KBot.Database;

namespace Vita3KBot.Commands {
    [Group("blacklist"), RequireModeratorRole]
    public class BlacklistModule : ModuleBase<SocketCommandContext> {
        [Group("remove")]
        public class Remove : ModuleBase<SocketCommandContext> {
            [Command, Name("remove"), Priority(1)]
            [Summary("Removes the provided term from the blacklist")]
            public async Task BlacklistRemove(string termToRemove) {
                using (var db = new BotDb()) {
                    var foundTerm = db.blacklistTerms.FirstOrDefault(t => t.BlacklistedText == termToRemove);
                    if (foundTerm != null) {
                        db.blacklistTerms.Remove(foundTerm);
                        db.SaveChanges();
                        await ReplyAsync("Term removed from blacklist").ConfigureAwait(false);
                        return;
                    }
                }
                await ReplyAsync("Term isn't blacklisted or you have a spelling mistake").ConfigureAwait(false);
            }
        }
        
        [Group("list")]
        public class List : ModuleBase<SocketCommandContext> {
            [Command, Name("list"), Priority(1)]
            [Summary("Lists all blacklisted terms")]
            public async Task BlacklistList() {
                List<string> terms = new List<string>();
                using (var db = new BotDb()) {
                    foreach(var term in db.blacklistTerms.ToList()) {
                        terms.Add(term.BlacklistedText);
                    }
                }
                if (terms.Count != 0) {
                    await ReplyAsync($"`{string.Join(", ", terms)}`").ConfigureAwait(false);
                }
            }
        }
        
        [Command, Name("blacklist"), Priority(0)]
        [Summary("Blacklists the provided term")]
        public async Task Blacklist(string termToAdd) {
            using (var db = new BotDb()) {
                if (db.blacklistTerms.AsQueryable().Where(t => t.BlacklistedText == termToAdd).FirstOrDefault() != null) {
                    await ReplyAsync("Term already blacklisted").ConfigureAwait(false);
                    return;
                }
                db.blacklistTerms.Add(new BlacklistTerm { BlacklistedText = termToAdd });
                db.SaveChanges();
            }
            await ReplyAsync("Term blacklisted successfully").ConfigureAwait(false);
        }
    }
}
