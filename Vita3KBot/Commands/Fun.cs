using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using DC = Discord.Commands;

namespace Vita3KBot.Commands {
    internal static class FunData {
        internal static readonly string[] EightBallReplies = {
            "It is certain.",             "It is decidedly so.",
            "Without a doubt.",           "Yes - definitely.",
            "You may rely on it.",        "As I see it, yes.",
            "Most likely.",               "Outlook good.",
            "Yes.",                       "Signs point to yes.",
            "Reply hazy, try again.",     "Ask again later.",
            "Better not tell you now.",   "Cannot predict now.",
            "Concentrate and ask again.", "Don't count on it.",
            "My reply is no.",            "My sources say no.",
            "Outlook not so good.",       "Very doubtful."
        };

        internal static readonly string[] TimePeriod = {
            "seconds", "minutes", "hours", "days", "months",
            "years",   "decades", "centuries", "millennia"
        };

        internal static string RandomEightBall() =>
            EightBallReplies[Utils.Random.Next(EightBallReplies.Length)];

        internal static string RandomWhen() =>
            $"It will happen in the next {Utils.Random.Next(2, 100)} " +
            $"{TimePeriod[Utils.Random.Next(TimePeriod.Length)]}.";
    }

    // ── Prefix commands ──────────────────────────────────────────

    [DC.Group("8ball")]
    public class EightBallPrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Command, DC.Name("8ball")]
        [DC.Summary("Accurately answers yes/no questions.")]
        public async Task Predict(
            [DC.Remainder, DC.Summary("The question you wish to ask.")] string question)
            => await ReplyAsync(FunData.RandomEightBall());
    }

    [DC.Group("when")]
    public class WhenPrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Command, DC.Name("when")]
        [DC.Summary("Determines when some event will happen.")]
        public async Task Predict(
            [DC.Remainder, DC.Summary("A description of an event to predict.")] string question)
            => await ReplyAsync(FunData.RandomWhen());
    }

    // ── Slash commands ───────────────────────────────────────────

    public class EightBallSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("8ball", "Accurately answers yes/no questions.")]
        public async Task Predict(
            [Discord.Interactions.Summary("question", "The question you wish to ask.")] string question)
            => await RespondAsync(FunData.RandomEightBall());
    }

    public class WhenSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("when", "Determines when some event will happen.")]
        public async Task Predict(
            [Discord.Interactions.Summary("event", "A description of an event to predict.")] string question)
            => await RespondAsync(FunData.RandomWhen());
    }
}
