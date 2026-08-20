using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Interactions;
using Octokit;

using Vita3KBot.APIClients.PSNClient;
using Vita3KBot.Commands.Attributes;

using DC = Discord.Commands;

namespace Vita3KBot.Commands
{
  // ── Shared logic ─────────────────────────────────────────────

  internal static partial class CompatUtils
  {
    internal const int MaxItemsToDisplay = 8;
    internal const int MaxDescriptionLength = 4096;
    internal const int MaxTitleLength = 256;
    internal const string HomebrewRepo = "homebrew-compatibility";
    internal const string CommercialRepo = "compatibility";

    internal static readonly string[] StatusNames = [
      // Priority, display when possible.
      "Playable", "Ingame", "Ingame +", "Ingame -",
      "Menu", "Intro", "Bootable", "Crash", "Nothing",
      // Secondary, display if nothing else.
      "Slow", "Black Screen", "NID Missing", "Module Loading Bug",
      "IO Bug", "Softlock Bug", "Graphics Bug", "Shader Bug",
      "Audio Bug", "Input Bug", "Touch Bug", "Savedata Bug",
      "Trophy Bug", "Networking Bug",
      // Invalid
      "Invalid", "Unknown",
    ];

    internal static bool IsValidTitleId(string titleId) =>
      TitleIdRegex().IsMatch(titleId);

    internal static string Normalize(string text) =>
      PunctuationRegex().Replace(text, " ").ToLowerInvariant();

    internal static string EscapeMarkdown(string text) =>
      MarkdownRegex().Replace(text, @"\$0");

    internal static string CodeSpan(string text) =>
      "`" + text.Replace("`", "'") + "`";

    internal static string Truncate(string text, int maxLength) =>
      text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";

    // PCS + 1 letter + 5 digits (e.g., PCSE00000)
    [GeneratedRegex(@"^PCS[A-Z]\d{5}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TitleIdRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}\s]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"[\\*_~`|\[\]()]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkdownRegex();

    internal class TitleInfo
    {
      private readonly Issue _issue;

      public readonly bool IsHomebrew;
      public readonly string Status;
      public readonly Color LabelColor;
      public string LatestComment;
      public string LatestProfileImage;

      public TitleInfo(Issue issue)
      {
        _issue = issue;
        // Repository object is sometimes null on searches. Just guess the repo by the URL.
        IsHomebrew = issue.Url.Contains(HomebrewRepo);
        Status = "Unknown";
        LabelColor = Color.Orange;

        foreach (var name in StatusNames)
        {
          var label = issue.Labels
            .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
          if (label != null)
          {
            Status = name;
            LabelColor = new Color(UInt32.Parse(label.Color, NumberStyles.HexNumber));
            break;
          }
        }

        LatestComment = "*No updates on this title.*";
        LatestProfileImage = "";
      }

      public async Task FetchCommentInfo(GitHubClient client)
      {
        if (_issue.Comments == 0)
        {
          return;
        }

        var comments = await client.Issue.Comment.GetAllForIssue(
          "Vita3K", IsHomebrew ? HomebrewRepo : CommercialRepo, _issue.Number);
        if (comments.Count == 0)
        {
          return;
        }

        var lastComment = comments[^1];
        LatestComment = "**" + lastComment.User.Login + "**: " + lastComment.Body;
        LatestProfileImage = lastComment.User.AvatarUrl;
      }
    }

    internal static async Task<(string message, Embed embed)?> SearchCompat(string keyword)
    {
      var keywords = Normalize(keyword).Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (keywords.Length == 0)
      {
        return (NotFoundMessage(keyword), null);
      }

      var github = new GitHubClient(new ProductHeaderValue("Vita3KBot"));
      var searchResults = (await github.Search.SearchIssues(BuildSearchRequest(keywords))).Items;
      var matches = RankMatches(searchResults, keywords);

      if (matches.Count == 0)
      {
        return (NotFoundMessage(keyword), null);
      }

      return matches.Count == 1
        ? (null, await BuildTitleEmbed(github, matches[0]))
        : (null, BuildResultListEmbed(matches, keyword));
    }

    private static string NotFoundMessage(string keyword) =>
      $"No games found for search term {EscapeMarkdown(Truncate(keyword, 200))}.";

    private static SearchIssuesRequest BuildSearchRequest(string[] keywords) =>
      new(string.Join(' ', keywords))
      {
        Repos = ["Vita3K/homebrew-compatibility", "Vita3K/compatibility"],
        State = ItemState.Open,
      };

    private static List<Issue> RankMatches(IReadOnlyList<Issue> searchResults, string[] keywords)
    {
      var scored = searchResults
        .Select(issue =>
        {
          var title = Normalize(issue.Title);
          return (issue, score: keywords.Count(k => title.Contains(k, StringComparison.Ordinal)));
        })
        .Where(x => x.score > 0)
        .ToList();

      if (scored.Count == 0)
      {
        return [];
      }

      var bestScore = scored.Max(x => x.score);
      return [.. scored.Where(x => x.score == bestScore).Select(x => x.issue)];
    }

    private static async Task<Embed> BuildTitleEmbed(GitHubClient github, Issue issue)
    {
      var info = new TitleInfo(issue);
      await info.FetchCommentInfo(github);

      var description = "Status: **" + info.Status + "**\n\n" + info.LatestComment;
      if (description.Length > MaxDescriptionLength)
      {
        description = description[..(MaxDescriptionLength - 3)] + "...";
      }

      var builder = new EmbedBuilder()
        .WithTitle(Truncate($"{issue.Title} ({(info.IsHomebrew ? "Homebrew" : "Commercial")})", MaxTitleLength))
        .WithDescription(description)
        .WithColor(info.LabelColor)
        .WithUrl(issue.HtmlUrl)
        .WithCurrentTimestamp();
      if (info.LatestProfileImage.Length > 0)
      {
        builder.WithThumbnailUrl(info.LatestProfileImage);
      }

      return builder.Build();
    }

    private static Embed BuildResultListEmbed(List<Issue> matches, string keyword)
    {
      var description = new StringBuilder();
      for (var i = 0; i < Math.Min(matches.Count, MaxItemsToDisplay); i++)
      {
        var issue = matches[i];
        var info = new TitleInfo(issue);
        var homebrewText = info.IsHomebrew ? "Homebrew" : "Commercial";
        description.Append($"[{CodeSpan(issue.Title)}]({issue.HtmlUrl}) ({homebrewText}): **{info.Status}**\n");
      }

      if (matches.Count > MaxItemsToDisplay)
      {
        description.Append("...");
      }

      return new EmbedBuilder()
        .WithTitle(Truncate($"Found {matches.Count} issues for search term {keyword}.", MaxTitleLength))
        .WithDescription(description.ToString())
        .WithColor(Color.Orange)
        .WithCurrentTimestamp()
        .Build();
    }
  }

  // ── Prefix commands ──────────────────────────────────────────

  [DC.Group("compat")]
  public class CompatibilityPrefix : DC.ModuleBase<DC.SocketCommandContext>
  {
    [DC.Command, DC.Name("compat")]
    [DC.Summary("Provides a compatibility report of the game.")]
    [PrefixRequireRoleOrChannel]
    public async Task Compatibility([DC.Remainder, DC.Summary("Game name to search")] string keyword)
    {
      var result = await CompatUtils.SearchCompat(keyword);
      if (result == null)
      {
        return;
      }

      var (message, embed) = result.Value;
      await ReplyAsync(message ?? "", false, embed);
    }
  }

  [DC.Group("update")]
  public class UpdatePrefix : DC.ModuleBase<DC.SocketCommandContext>
  {
    [DC.Command, DC.Name("update")]
    [DC.Summary("Provides PSN update information for the game.")]
    [PrefixRequireRoleOrChannel]
    public async Task GetUpdate([DC.Remainder, DC.Summary("Title ID of the game or English game title")] string titleId)
    {
      var normalized = titleId.ToUpper();
      if (!CompatUtils.IsValidTitleId(normalized) && normalized.StartsWith("PCS"))
      {
        await ReplyAsync("❌ Invalid title ID. Please enter it in the format `PCSE12345` (PCS + 1 letter + 5 digits).");
        return;
      }

      var (embed, components) = PSNClient.GetTitlePatch(normalized);
      await ReplyAsync(embed: embed, components: components);
    }
  }

  // ── Slash commands ───────────────────────────────────────────

  public class CompatibilitySlash : InteractionModuleBase<SocketInteractionContext>
  {
    [SlashCommand("compat", "Provides a compatibility report of the game.")]
    [SlashRequireRoleOrChannel]
    public async Task Compatibility(
      [Discord.Interactions.Summary("keyword", "Game name to search")] string keyword)
    {
      await DeferAsync();
      var result = await CompatUtils.SearchCompat(keyword);
      if (result == null)
      {
        return;
      }

      var (message, embed) = result.Value;
      await FollowupAsync(message ?? "", embed: embed);
    }
  }

  public class UpdateSlash : InteractionModuleBase<SocketInteractionContext>
  {
    [SlashCommand("update", "Provides PSN update information for the game.")]
    [SlashRequireRoleOrChannel]
    public async Task GetUpdate(
      [Discord.Interactions.Summary("title_id", "Title ID of the game (e.g. PCSE00000) or English game title")] string titleId)
    {
      await DeferAsync();
      var normalized = titleId.ToUpper();
      if (!CompatUtils.IsValidTitleId(normalized) && normalized.StartsWith("PCS"))
      {
        await FollowupAsync("❌ Invalid title ID. Please enter it in the format `PCSE12345` (PCS + 1 letter + 5 digits).", ephemeral: true);
        return;
      }

      var (embed, components) = PSNClient.GetTitlePatch(normalized);
      await FollowupAsync(embed: embed, components: components);
    }
  }

  public class UpdateButtonHandler : InteractionModuleBase<SocketInteractionContext>
  {
    [ComponentInteraction("update:*")]
    public async Task OnUpdateButton(string selectedId)
    {
      await DeferAsync();
      var (embed, components) = PSNClient.GetTitlePatch(selectedId);
      if (Context.Interaction is IComponentInteraction component)
      {
        await component.UpdateAsync(m =>
        {
          m.Embed = embed;
          m.Components = components ?? new ComponentBuilder().Build();
        });
      }
    }
  }
}
