using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

using Discord;

using Octokit;

namespace APIClients {
    public static class GithubClient {
        public class BuildAssets {
            public ReleaseAsset Windows_x86_64 { get; init; } = null!;
            public ReleaseAsset Windows_arm64 { get; init; } = null!;
            public ReleaseAsset x86_64_AppImage { get; init; } = null!;
            public ReleaseAsset arm64_AppImage { get; init; } = null!;
            public ReleaseAsset MacOS { get; init; } = null!;
            public ReleaseAsset Android { get; init; } = null!;
        }

        public static async Task<Embed> GetLatestBuild() {

            GitHubClient github = new(new ProductHeaderValue("Vita3KBot"));
            Release latestRelease = await github.Repository.Release.Get("Vita3k", "Vita3k", "continuous");
            long unixTime = latestRelease.PublishedAt.Value.ToUnixTimeSeconds();

            // Get commit and PR info
            string commit = latestRelease.Body.Substring(latestRelease.Body.IndexOf("commit:") + 7).Trim();
            commit = commit.Substring(0, commit.IndexOf("\n"));
            GitHubCommit REF = await github.Repository.Commit.Get("Vita3k", "Vita3k", commit);
            Issue prInfo = await GetPRInfo(github, commit);
            string bodyText = !string.IsNullOrWhiteSpace(prInfo.Body) ? prInfo.Body : REF.Commit.Message;

           // Get build assets
            string buildNum = latestRelease.Body.Substring(latestRelease.Body.IndexOf("Build:") + 6).Trim();
            BuildAssets assets = await GetReleaseAssets(github, buildNum, latestRelease);

            // Truncate bodyText to fit Discord's 4096 char limit for description
            // Title format: "**{prInfo.Title}**\n\n" takes up space, so account for it
            string title = prInfo != null ? prInfo.Title : "";
            int titleOverhead = $"**{title}**\n\n".Length;
            int maxBodyLength = 4096 - titleOverhead;

            if (bodyText.Length > maxBodyLength) {
              bodyText = bodyText.Substring(0, maxBodyLength - 3) + "...";
            }

            EmbedBuilder LatestBuild = new();
            if (prInfo != null) {
                LatestBuild.WithTitle($"PR: #{prInfo.Number} By {prInfo.User.Login}")
                .WithUrl(prInfo.HtmlUrl);
            } else {
                LatestBuild.WithTitle($"Commit: {REF.Sha} By {REF.Commit.Author.Name}")
                .WithUrl($"https://github.com/vita3k/vita3k/commit/{REF.Sha}");
            }

            LatestBuild.WithDescription($"**{title}**\n\n{bodyText}")
            .WithColor(Color.Orange)
            .AddField("Windows", $"[{assets.Windows_x86_64.Name}]({assets.Windows_x86_64.BrowserDownloadUrl})", true)
            .AddField("Linux", $"[{assets.x86_64_AppImage.Name}]({assets.x86_64_AppImage.BrowserDownloadUrl})", true)
            .AddField("macOS", $"[{assets.MacOS.Name}]({assets.MacOS.BrowserDownloadUrl})", true)
            .AddField("Windows (ARM64)", $"[{assets.Windows_arm64.Name}]({assets.Windows_arm64.BrowserDownloadUrl})", true)
            .AddField("Linux (ARM64)", $"[{assets.arm64_AppImage.Name}]({assets.arm64_AppImage.BrowserDownloadUrl})", true)
            .AddField("Android", $"[{assets.Android.Name}]({assets.Android.BrowserDownloadUrl})", true)
            .AddField("\u200B", $"Built on: <t:{unixTime}:F> (<t:{unixTime}:R>)");

            return LatestBuild.Build();
        }

        private static async Task<Issue> GetPRInfo(GitHubClient github, string commit) {

            var request = new SearchIssuesRequest(commit) {
                Type = IssueTypeQualifier.PullRequest,
                State = ItemState.Closed,
            };
            request.Repos.Add("Vita3K/Vita3K");

            var searchResults = (await github.Search.SearchIssues(request)).Items;

            return searchResults.FirstOrDefault();
        }

        private static async Task<BuildAssets> GetReleaseAssets(GitHubClient github,string buildNum, Release latestRelease) {
            try {
                var storeRelease = await github.Repository.Release.Get("Vita3k","Vita3k-builds",buildNum);
                return new BuildAssets
                {
                    Windows_x86_64 = storeRelease.Assets.First(a => a.Name.EndsWith("windows-x86_64.7z")),
                    Windows_arm64 = storeRelease.Assets.First(a => a.Name.EndsWith("windows-arm64.7z")),
                    x86_64_AppImage = storeRelease.Assets.First(a => a.Name.EndsWith("x86_64.AppImage")),
                    arm64_AppImage = storeRelease.Assets.First(a => a.Name.EndsWith("aarch64.AppImage")),
                    MacOS = storeRelease.Assets.First(a => a.Name.EndsWith("macos-intel.dmg")),
                    Android = storeRelease.Assets.First(a => a.Name.EndsWith("android.apk"))
                };
            }
            catch (Octokit.NotFoundException) {
                return new BuildAssets
                {
                    Windows_x86_64 = latestRelease.Assets.First(a => a.Name.StartsWith("windows-latest")),
                    Windows_arm64 = latestRelease.Assets.First(a => a.Name.StartsWith("windows-arm64-latest")),
                    x86_64_AppImage = latestRelease.Assets.First(a => a.Name.EndsWith("x86_64.AppImage")),
                    arm64_AppImage = latestRelease.Assets.First(a => a.Name.EndsWith("aarch64.AppImage")),
                    MacOS = latestRelease.Assets.First(a => a.Name.StartsWith("macos-latest")),
                    Android = latestRelease.Assets.First(a => a.Name.StartsWith("android-latest"))
                };
            }
        }
        public static async Task<Embed> GetBuildByNumber(string buildNum) {
            GitHubClient github = new(new ProductHeaderValue("Vita3KBot"));

            Release release;
            try {
                release = await github.Repository.Release.Get("Vita3k", "Vita3k-builds", buildNum);
            } catch (Octokit.NotFoundException) {
                return new EmbedBuilder()
                    .WithTitle("Build not found")
                    .WithDescription($"No build was found for build number `{buildNum}`.\n" +
                        "Check [the releases page](https://github.com/Vita3K/Vita3K-builds/releases) for valid build numbers.")
                    .WithColor(Color.Orange)
                    .Build();
            }

            // Release body format:
            // "Corresponding commit: [<commit message>](<commit url>) (<author>)"
            string commitMessage = null;
            string commitAuthor  = null;
            string commitUrl     = null;

            if (!string.IsNullOrWhiteSpace(release.Body)) {
                var match = Regex.Match(release.Body,
                    @"Corresponding commit:\s*\[(?<message>.+?)\]\((?<url>[^)]+)\)\s*\((?<author>[^()]+)\)",
                    RegexOptions.Singleline);
                if (match.Success) {
                    commitMessage = match.Groups["message"].Value.Trim();
                    commitUrl     = match.Groups["url"].Value.Trim();
                    commitAuthor  = match.Groups["author"].Value.Trim();
                }
            }

            // The commit message often ends with "(#1234)" when it's a squash-merged PR.
            // If we can find that PR number, fetch it directly for a richer, more accurate embed.
            Issue prInfo = null;
            if (commitMessage != null) {
                var prMatch = Regex.Match(commitMessage, @"#(\d+)\)?\s*$");
                if (prMatch.Success && int.TryParse(prMatch.Groups[1].Value, out var prNumber)) {
                    try { prInfo = await github.Issue.Get("Vita3k", "Vita3k", prNumber); }
                    catch (Octokit.NotFoundException) { /* not actually a PR, fall back to the commit */ }
                }
            }

            string embedTitle, embedUrl, headline, bodyText;

            if (prInfo != null) {
                embedTitle = $"PR: #{prInfo.Number} By {prInfo.User.Login}";
                embedUrl   = prInfo.HtmlUrl;
                headline   = prInfo.Title;
                bodyText   = !string.IsNullOrWhiteSpace(prInfo.Body) ? prInfo.Body : (commitMessage ?? "");
            } else if (commitUrl != null) {
                var shaMatch = Regex.Match(commitUrl, @"/commit/([0-9a-f]+)$");
                var shortSha = shaMatch.Success ? shaMatch.Groups[1].Value.Substring(0, 7) : buildNum;
                embedTitle = commitAuthor != null ? $"Commit: {shortSha} By {commitAuthor}" : $"Commit: {shortSha}";
                embedUrl   = commitUrl;
                headline   = commitMessage ?? "";
                bodyText   = "";
            } else {
                embedTitle = $"Build #{buildNum}";
                embedUrl   = release.HtmlUrl;
                headline   = "";
                bodyText   = "";
            }

            var description = string.IsNullOrEmpty(headline)
                ? null
                : (string.IsNullOrEmpty(bodyText) ? $"**{headline}**" : $"**{headline}**\n\n{bodyText}");

            if (description != null && description.Length > 4096)
                description = description.Substring(0, 4093) + "...";

            var embed = new EmbedBuilder()
                .WithTitle(embedTitle)
                .WithUrl(embedUrl)
                .WithColor(Color.Orange);
            if (description != null) embed.WithDescription(description);

            void AddAssetField(string label, string suffix) {
                var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(suffix));
                if (asset != null)
                    embed.AddField(label, $"[{asset.Name}]({asset.BrowserDownloadUrl})", true);
            }

            AddAssetField("Windows", "windows-x86_64.7z");
            AddAssetField("Windows (ARM64)", "windows-arm64.7z");
            AddAssetField("Linux", "x86_64.AppImage");
            AddAssetField("Linux (ARM64)", "aarch64.AppImage");
            AddAssetField("macOS", "macos-intel.dmg");
            AddAssetField("Android", "android.apk");

            if (release.PublishedAt.HasValue) {
                long unixTime = release.PublishedAt.Value.ToUnixTimeSeconds();
                embed.AddField("\u200B", $"Built on: <t:{unixTime}:F> (<t:{unixTime}:R>)");
            }

            return embed.Build();
        }
    }
}
