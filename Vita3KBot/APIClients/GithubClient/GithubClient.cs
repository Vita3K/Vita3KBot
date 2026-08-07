using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord;

using Octokit;

namespace Vita3KBot.APIClients.GithubClient
{
  public static partial class GithubClient
  {
    private const string RepoOwner = "Vita3k";

    public class BuildAssets
    {
      public ReleaseAsset Windows_x86_64 { get; init; } = null!;
      public ReleaseAsset Windows_arm64 { get; init; } = null!;
      public ReleaseAsset X86_64_AppImage { get; init; } = null!;
      public ReleaseAsset Arm64_AppImage { get; init; } = null!;
      public ReleaseAsset MacOS { get; init; } = null!;
      public ReleaseAsset Android { get; init; } = null!;
    }

    public static async Task<Embed> GetLatestBuild()
    {
      GitHubClient github = new(new ProductHeaderValue("Vita3KBot"));
      Release latestRelease = await github.Repository.Release.Get(RepoOwner, RepoOwner, "continuous");
      long unixTime = latestRelease.PublishedAt.Value.ToUnixTimeSeconds();

      // Get commit and PR info
      string commit = latestRelease.Body[(latestRelease.Body.IndexOf("commit:") + 7)..].Trim();
      commit = commit[..commit.IndexOf('\n')];
      GitHubCommit REF = await github.Repository.Commit.Get(RepoOwner, RepoOwner, commit);
      Issue prInfo = await GetPRInfo(github, commit);
      string bodyText = !string.IsNullOrWhiteSpace(prInfo.Body) ? prInfo.Body : REF.Commit.Message;

      // Get build assets
      string buildNum = latestRelease.Body[(latestRelease.Body.IndexOf("Build:") + 6)..].Trim();
      BuildAssets assets = await GetReleaseAssets(github, buildNum, latestRelease);

      // Truncate bodyText to fit Discord's 4096 char limit for description
      // Title format: "**{prInfo.Title}**\n\n" takes up space, so account for it
      string title = prInfo != null ? prInfo.Title : "";
      int titleOverhead = $"**{title}**\n\n".Length;
      int maxBodyLength = 4096 - titleOverhead;

      if (bodyText.Length > maxBodyLength)
      {
        bodyText = string.Concat(bodyText.AsSpan(0, maxBodyLength - 3), "...");
      }

      EmbedBuilder LatestBuild = new();
      if (prInfo != null)
      {
        LatestBuild.WithTitle($"PR: #{prInfo.Number} By {prInfo.User.Login}")
          .WithUrl(prInfo.HtmlUrl);
      }
      else
      {
        LatestBuild.WithTitle($"Commit: {REF.Sha} By {REF.Commit.Author.Name}")
          .WithUrl($"https://github.com/vita3k/vita3k/commit/{REF.Sha}");
      }

      LatestBuild.WithDescription($"**{title}**\n\n{bodyText}")
        .WithColor(Color.Orange)
        .AddField("Windows", $"[{assets.Windows_x86_64.Name}]({assets.Windows_x86_64.BrowserDownloadUrl})", true)
        .AddField("Linux", $"[{assets.X86_64_AppImage.Name}]({assets.X86_64_AppImage.BrowserDownloadUrl})", true)
        .AddField("macOS", $"[{assets.MacOS.Name}]({assets.MacOS.BrowserDownloadUrl})", true)
        .AddField("Windows (ARM64)", $"[{assets.Windows_arm64.Name}]({assets.Windows_arm64.BrowserDownloadUrl})", true)
        .AddField("Linux (ARM64)", $"[{assets.Arm64_AppImage.Name}]({assets.Arm64_AppImage.BrowserDownloadUrl})", true)
        .AddField("Android", $"[{assets.Android.Name}]({assets.Android.BrowserDownloadUrl})", true)
        .AddField("\u200B", $"Built on: <t:{unixTime}:F> (<t:{unixTime}:R>)");

      return LatestBuild.Build();
    }

    public static async Task<Embed> GetBuildByNumber(string buildNum)
    {
      GitHubClient github = new(new ProductHeaderValue("Vita3KBot"));

      Release release;
      try
      {
        release = await github.Repository.Release.Get(RepoOwner, "Vita3k-builds", buildNum);
      }
      catch (Octokit.NotFoundException)
      {
        return BuildNotFoundEmbed(buildNum);
      }

      var commitInfo = ParseCommitInfo(release);
      var prInfo = await TryFindPullRequestAsync(github, commitInfo.Message);
      var (embedTitle, embedUrl, headline, bodyText) = ResolveEmbedContent(release, buildNum, prInfo, commitInfo);
      var description = BuildTruncatedDescription(headline, bodyText);

      var embed = new EmbedBuilder()
        .WithTitle(embedTitle)
        .WithUrl(embedUrl)
        .WithColor(Color.Orange);
      if (description != null)
      {
        embed.WithDescription(description);
      }

      AddAssetFields(embed, release);
      AddPublishedDateField(embed, release);

      return embed.Build();
    }

    private static Embed BuildNotFoundEmbed(string buildNum)
    {
      return new EmbedBuilder()
        .WithTitle("Build not found")
        .WithDescription($"No build was found for build number `{buildNum}`.\n" +
          "Check [the releases page](https://github.com/Vita3K/Vita3K-builds/releases) for valid build numbers.")
        .WithColor(Color.Orange)
        .Build();
    }

    private sealed record CommitInfo(string Message, string Url, string Author);

    // Release body format:
    // "Corresponding commit: [<commit message>](<commit url>) (<author>)"
    private static CommitInfo ParseCommitInfo(Release release)
    {
      if (string.IsNullOrWhiteSpace(release.Body))
      {
        return new CommitInfo(null, null, null);
      }

      var match = CommitInfoRegex().Match(release.Body);
      if (!match.Success)
      {
        return new CommitInfo(null, null, null);
      }

      return new CommitInfo(
        match.Groups["message"].Value.Trim(),
        match.Groups["url"].Value.Trim(),
        match.Groups["author"].Value.Trim());
    }

    // The commit message often ends with "(#1234)" when it's a squash-merged PR.
    // If we can find that PR number, fetch it directly for a richer, more accurate embed.
    private static async Task<Issue> TryFindPullRequestAsync(GitHubClient github, string commitMessage)
    {
      if (commitMessage == null)
      {
        return null;
      }

      var prMatch = PrNumberRegex().Match(commitMessage);
      if (!prMatch.Success || !int.TryParse(prMatch.Groups[1].Value, out var prNumber))
      {
        return null;
      }

      try
      {
        return await github.Issue.Get(RepoOwner, RepoOwner, prNumber);
      }
      catch (Octokit.NotFoundException)
      {
        // not actually a PR, fall back to the commit
        return null;
      }
    }

    private static (string EmbedTitle, string EmbedUrl, string Headline, string BodyText) ResolveEmbedContent(
      Release release, string buildNum, Issue prInfo, CommitInfo commitInfo)
    {
      if (prInfo != null)
      {
        var bodyText = !string.IsNullOrWhiteSpace(prInfo.Body) ? prInfo.Body : (commitInfo.Message ?? "");
        return ($"PR: #{prInfo.Number} By {prInfo.User.Login}", prInfo.HtmlUrl, prInfo.Title, bodyText);
      }

      if (commitInfo.Url != null)
      {
        var shaMatch = CommitShaRegex().Match(commitInfo.Url);
        var shortSha = shaMatch.Success ? shaMatch.Groups[1].Value[..7] : buildNum;
        var embedTitle = commitInfo.Author != null ? $"Commit: {shortSha} By {commitInfo.Author}" : $"Commit: {shortSha}";
        return (embedTitle, commitInfo.Url, commitInfo.Message ?? "", "");
      }

      return ($"Build #{buildNum}", release.HtmlUrl, "", "");
    }

    private static string BuildTruncatedDescription(string headline, string bodyText)
    {
      if (string.IsNullOrEmpty(headline))
      {
        return null;
      }

      var description = string.IsNullOrEmpty(bodyText) ? $"**{headline}**" : $"**{headline}**\n\n{bodyText}";
      return description.Length > 4096 ? string.Concat(description.AsSpan(0, 4093), "...") : description;
    }

    private static void AddAssetFields(EmbedBuilder embed, Release release)
    {
      void AddAssetField(string label, string suffix)
      {
        var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(suffix));
        if (asset != null)
        {
          embed.AddField(label, $"[{asset.Name}]({asset.BrowserDownloadUrl})", true);
        }
      }

      AddAssetField("Windows", "windows-x86_64.7z");
      AddAssetField("Windows (ARM64)", "windows-arm64.7z");
      AddAssetField("Linux", "x86_64.AppImage");
      AddAssetField("Linux (ARM64)", "aarch64.AppImage");
      AddAssetField("macOS", "macos-intel.dmg");
      AddAssetField("Android", "android.apk");
    }

    private static void AddPublishedDateField(EmbedBuilder embed, Release release)
    {
      if (!release.PublishedAt.HasValue)
      {
        return;
      }

      long unixTime = release.PublishedAt.Value.ToUnixTimeSeconds();
      embed.AddField("\u200B", $"Built on: <t:{unixTime}:F> (<t:{unixTime}:R>)");
    }

    private static async Task<Issue> GetPRInfo(GitHubClient github, string commit)
    {
      var request = new SearchIssuesRequest(commit)
      {
        Type = IssueTypeQualifier.PullRequest,
        State = ItemState.Closed,
      };
      request.Repos.Add("Vita3K/Vita3K");

      var searchResults = (await github.Search.SearchIssues(request)).Items;

      return searchResults.Count > 0 ? searchResults[0] : null;
    }

    private static async Task<BuildAssets> GetReleaseAssets(GitHubClient github, string buildNum, Release latestRelease)
    {
      try
      {
        var storeRelease = await github.Repository.Release.Get(RepoOwner, "Vita3k-builds", buildNum);
        return new BuildAssets
        {
          Windows_x86_64 = storeRelease.Assets.First(a => a.Name.EndsWith("windows-x86_64.7z")),
          Windows_arm64 = storeRelease.Assets.First(a => a.Name.EndsWith("windows-arm64.7z")),
          X86_64_AppImage = storeRelease.Assets.First(a => a.Name.EndsWith("x86_64.AppImage")),
          Arm64_AppImage = storeRelease.Assets.First(a => a.Name.EndsWith("aarch64.AppImage")),
          MacOS = storeRelease.Assets.First(a => a.Name.EndsWith("macos-intel.dmg")),
          Android = storeRelease.Assets.First(a => a.Name.EndsWith("android.apk"))
        };
      }
      catch (Octokit.NotFoundException)
      {
        return new BuildAssets
        {
          Windows_x86_64 = latestRelease.Assets.First(a => a.Name.StartsWith("windows-latest")),
          Windows_arm64 = latestRelease.Assets.First(a => a.Name.StartsWith("windows-arm64-latest")),
          X86_64_AppImage = latestRelease.Assets.First(a => a.Name.EndsWith("x86_64.AppImage")),
          Arm64_AppImage = latestRelease.Assets.First(a => a.Name.EndsWith("aarch64.AppImage")),
          MacOS = latestRelease.Assets.First(a => a.Name.StartsWith("macos-latest")),
          Android = latestRelease.Assets.First(a => a.Name.StartsWith("android-latest"))
        };
      }
    }

    [GeneratedRegex(@"Corresponding commit:\s*\[(?<message>.+?)\]\((?<url>[^)]+)\)\s*\((?<author>[^()]+)\)", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommitInfoRegex();

    [GeneratedRegex(@"#(\d+)\)?\s*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrNumberRegex();

    [GeneratedRegex(@"/commit/([0-9a-f]+)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommitShaRegex();
  }
}
