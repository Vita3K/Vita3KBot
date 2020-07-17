using System.Threading.Tasks;

using Discord.Commands;

namespace Vita3KBot.Commands {
    [Group ("8ball")]
    public class EightBall : ModuleBase<SocketCommandContext> {
        private static readonly string[] EightBallReplies = {
            "It is certain.",
            "It is decidedly so.",
            "Without a doubt.",
            "Yes - definitely.",
            "You may rely on it.",
            "As I see it, yes.",
            "Most likely.",
            "Outlook good.",
            "Yes.",
            "Signs point to yes.",
            "Reply hazy, try again.",
            "Ask again later.",
            "Better not tell you now.",
            "Cannot predict now.",
            "Concentrate and ask again.",
            "Don't count on it.",
            "My reply is no.",
            "My sources say no.",
            "Outlook not so good.",
            "Very doubtful."
        };
        
        // Question parameter needs to be specified by the user so bot can reply
    
        [Command, Name("8ball")]
        [Summary("Accurately answers yes/no questions.")]
        public async Task Predict([Remainder, Summary("The question you wish to ask.")] string question) {
            await ReplyAsync(EightBallReplies[Utils.Random.Next(EightBallReplies.Length - 1)]);
        }
    }

    [Group("when")]
    public class When : ModuleBase<SocketCommandContext> {
        private static readonly string[] TimePeriod = {
            "seconds",
            "minutes",
            "hours",
            "days",
            "months",
            "years",
            "decades",
            "centuries",
            "mellenia"
        };
        
        [Command, Name("when")]
        [Summary("Determines when some event will happen.")]
        public async Task Predict([Remainder, Summary("A description of an event to predict.")] string question) {
            await ReplyAsync(
                $"It will happen in the next {Utils.Random.Next(2, 100)} " +
                $"{TimePeriod[Utils.Random.Next(TimePeriod.Length - 1)]}.");
        }
    }
}
