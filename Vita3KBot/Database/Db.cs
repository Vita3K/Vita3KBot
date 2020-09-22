using Microsoft.EntityFrameworkCore;

namespace Vita3KBot.Database {
    internal class BotDb : DbContext {
        public DbSet<BlacklistTerm> blacklistTerms { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options) {
            options.UseSqlite("Data Source=bot.db");
        }
    }

    internal class BlacklistTerm {
        public int Id { get; set; }
        public string BlacklistedText { get; set; }
    }
}