using Discord;
using Discord.Commands;
using Discord.Interactions;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Vita3KBot.Commands.Attributes;
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

    internal static async Task<(string Answer, bool UsedSearch)> AskGeminiAsync(string question)
    {
      var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
          ?? throw new InvalidOperationException("GEMINI_API_KEY is not set.");

      const string NormalModel = "gemini-3.1-flash-lite";
      const string SearchModel = "gemini-2.5-flash";
      const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

      const string SystemPrompt = """
        You are a helpful assistant in the Vita3K Discord server.
        Vita3K is an open-source PlayStation Vita emulator for PC.
        When answering questions, assume they are related to Vita3K,
        PS Vita games, or emulation unless the question is clearly about something else.
        Answer in one response only. Do not ask follow-up questions.
        Do not suggest continuing the conversation.
        Be concise and direct.
        You can only use English. If the question is not in English, respond in English regardless.
        """;

      // ① First, have the standard model determine whether grounding is necessary
      var classifyBody = new
      {
        system_instruction = new
        {
          parts = new[] { new { text = "You are a classifier. Reply with only 'SEARCH' or 'NO_SEARCH'." } }
        },
        contents = new[] {
            new { parts = new[] { new { text =
                $"Does answering this question require up-to-date or real-time information? Question: {question}" } } }
        }
      };

      var classifyJson = JsonSerializer.Serialize(classifyBody);
      var classifyUrl = $"{BaseUrl}/{NormalModel}:generateContent?key={apiKey}";
      var classifyResp = await _httpClient.PostAsync(classifyUrl, new StringContent(classifyJson, Encoding.UTF8, "application/json"));

      var needsSearch = false;
      if (classifyResp.IsSuccessStatusCode)
      {
        using var classifyDoc = JsonDocument.Parse(await classifyResp.Content.ReadAsStringAsync());
        var verdict = classifyDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";
        var trimmed = verdict.Trim();
        needsSearch = trimmed.Equals("SEARCH", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("SEARCH", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"[Gemini] needsSearch={needsSearch} ({verdict.Trim()})");
      }

      // ② Switch between models and requests based on the evaluation result
      string answerJson;
      string answerUrl;

      if (needsSearch)
      {
        var body = new
        {
          system_instruction = new { parts = new[] { new { text = SystemPrompt } } },
          contents = new[] { new { parts = new[] { new { text = question } } } },
          tools = new[] { new { google_search = new { } } }
        };
        answerJson = JsonSerializer.Serialize(body);
        answerUrl = $"{BaseUrl}/{SearchModel}:generateContent?key={apiKey}";
      }
      else
      {
        var body = new
        {
          system_instruction = new { parts = new[] { new { text = SystemPrompt } } },
          contents = new[] { new { parts = new[] { new { text = question } } } }
        };
        answerJson = JsonSerializer.Serialize(body);
        answerUrl = $"{BaseUrl}/{NormalModel}:generateContent?key={apiKey}";
      }

      var response = await _httpClient.PostAsync(answerUrl, new StringContent(answerJson, Encoding.UTF8, "application/json"));

      if (!response.IsSuccessStatusCode)
      {
        var errorBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[Gemini] {(int)response.StatusCode} Error: {errorBody}");
        return ("⚠️ Gemini API returned an error. Please try again later.", false);
      }

      var resultJson = await response.Content.ReadAsStringAsync();
      using var doc = JsonDocument.Parse(resultJson);

      return (doc.RootElement
          .GetProperty("candidates")[0]
          .GetProperty("content")
          .GetProperty("parts")[0]
          .GetProperty("text")
          .GetString() ?? "No response.", needsSearch);
    }

    private static readonly System.Net.Http.HttpClient _httpClient = new();

    internal static readonly string[] TimePeriod = {
            "seconds", "minutes", "hours", "days", "months",
            "years",   "decades", "centuries", "millennia"
        };

        internal static readonly string[] RpsHands = { "Rock", "Paper", "Scissors" };
        internal static readonly string[] RpsEmoji = { "✊", "✋", "✌️" };

        internal static string RandomEightBall() =>
            EightBallReplies[Utils.Random.Next(EightBallReplies.Length)];

        internal static string RandomWhen() =>
            $"It will happen in the next {Utils.Random.Next(2, 100)} " +
            $"{TimePeriod[Utils.Random.Next(TimePeriod.Length)]}.";

        internal static (int index, string name, string emoji) RandomRpsHand() {
            int i = Utils.Random.Next(RpsHands.Length);
            return (i, RpsHands[i], RpsEmoji[i]);
        }

        // 0=Rock 1=Paper 2=Scissors
        // Return value: 1=Player wins, 0=Draw, -1=Player loses
        internal static int RpsResult(int player, int bot) {
            if (player == bot) return 0;
            if ((player - bot + 3) % 3 == 1) return 1;
            return -1;
        }

        internal static MessageComponent RpsButtons() =>
            new ComponentBuilder()
                .WithButton("✊ Rock",     "rps:0", ButtonStyle.Primary)
                .WithButton("✋ Paper",    "rps:1", ButtonStyle.Primary)
                .WithButton("✌️ Scissors", "rps:2", ButtonStyle.Primary)
                .Build();

        internal static string RpsResultMessage(int playerIndex, string invokerMention) {
            var bot = RandomRpsHand();
            int result = RpsResult(playerIndex, bot.index);
            string playerEmoji = RpsEmoji[playerIndex];
            string playerName  = RpsHands[playerIndex];
            string outcome = result switch {
                1  => "🎉 You win!",
                0  => "🤝 Draw!",
                -1 => "🤖 Bot wins!",
                _  => ""
            };
            return $"{invokerMention}\n" +
                   $"You: {playerEmoji} **{playerName}** vs Bot: {bot.emoji} **{bot.name}**\n" +
                   $"{outcome}";
        }
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

    [DC.Group("ping")]
    public class PingPrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Command, DC.Name("ping")]
        [DC.Summary("Checks the bot's latency.")]
        public async Task Ping() {
            var sw = Stopwatch.StartNew();
            var msg = await ReplyAsync("Pinging...");
            sw.Stop();
            await msg.ModifyAsync(m =>
                m.Content = $"🏓 Pong! Latency: **{sw.ElapsedMilliseconds}ms**");
        }
    }

    [DC.Group("question")]
    public class QuestionPrefix : DC.ModuleBase<DC.SocketCommandContext>
    {
      [DC.Command, DC.Name("question")]
      [DC.Summary("Ask Gemini AI a question.")]
      [PrefixRequireRoleOrChannel]
    public async Task Ask([DC.Remainder, DC.Summary("The question to ask.")] string question) {
        var typing = Context.Channel.EnterTypingState();
        try
        {
          var (answer, usedSearch) = await FunData.AskGeminiAsync(question);
          // Keep within Discord's 2,000-character limit
          if (answer.Length > 1900)
            answer = answer[..1900] + "…";

          var embed = new EmbedBuilder()
              .WithAuthor("Gemini", "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/webp/google-gemini.webp")
              .WithDescription(answer)
              .WithFooter(usedSearch ? $"Asked by {Context.User.Username} • 🔍 Used Search" : $"Asked by {Context.User.Username}")
              .WithColor(new Color(0x4285F4))
              .WithTimestamp(DateTimeOffset.Now)
              .Build();

          await ReplyAsync(embed: embed);
        }
        finally
        {
          typing.Dispose();
        }
      }
    }

  [DC.Group("rps")]
    public class RpsPrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Command, DC.Name("rps")]
        [DC.Summary("Play rock-paper-scissors against the bot.")]
        public async Task Play()
            => await ReplyAsync("✊✋✌️ Rock, paper, scissors, go! Choose your hand:",
                components: FunData.RpsButtons());
    }

    // ── Slash commands ───────────────────────────────────────────

    public class EightBallSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("8ball", "Accurately answers yes/no questions.")]
        public async Task Predict(
            [Discord.Interactions.Summary("question", "The question you wish to ask.")] string question) {
            await DeferAsync();
            await FollowupAsync(FunData.RandomEightBall());
        }
    }

    public class WhenSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("when", "Determines when some event will happen.")]
        public async Task Predict(
            [Discord.Interactions.Summary("event", "A description of an event to predict.")] string question) {
            await DeferAsync();
            await FollowupAsync(FunData.RandomWhen());
        }
    }

    public class PingSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("ping", "Checks the bot's latency.")]
        public async Task Ping() {
            await DeferAsync();
            var sw = Stopwatch.StartNew();
            await FollowupAsync("Pinging...");
            sw.Stop();
            await ModifyOriginalResponseAsync(m =>
                m.Content = $"🏓 Pong! Latency: **{sw.ElapsedMilliseconds}ms**");
        }
    }

    public class QuestionSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("question", "Ask Gemini AI a question.")]
        [SlashRequireRoleOrChannel]
        public async Task Ask(
            [Discord.Interactions.Summary("question", "The question you want to ask.")] string question) {
            await DeferAsync();
            var (answer, usedSearch) = await FunData.AskGeminiAsync(question);
            if (answer.Length > 1900)
                answer = answer[..1900] + "…";

            var embed = new EmbedBuilder()
                .WithAuthor("Gemini", "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/webp/google-gemini.webp")
                .WithDescription(answer)
                .WithFooter(usedSearch ? $"Asked by {Context.User.Username} • 🔍 Used Search" : $"Asked by {Context.User.Username}")
                .WithColor(new Color(0x4285F4))
                .WithTimestamp(DateTimeOffset.Now)
                .Build();

            await FollowupAsync(embed: embed);
        }
    }

    public class RpsSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("rps", "Play rock-paper-scissors against the bot.")]
        public async Task Play() {
            await DeferAsync();
            await FollowupAsync("✊✋✌️ Rock, paper, scissors, go! Choose your hand:",
                components: FunData.RpsButtons());
        }
    }

    // ── Button interaction handler ───────────────────────────────

    public class RpsButtonHandler : InteractionModuleBase<SocketInteractionContext> {
        [ComponentInteraction("rps:*")]
        public async Task OnRpsButton(string handIndex) {
            int playerIndex = int.Parse(handIndex);
            string result = FunData.RpsResultMessage(playerIndex, Context.User.Mention);
            if (Context.Interaction is IComponentInteraction component) {
                await component.UpdateAsync(m => {
                    m.Content    = result;
                    m.Components = new ComponentBuilder().Build();
                });
            }
        }
    }
}
