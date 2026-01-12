using System.Threading.Tasks;
using System.Linq;

using Discord;

using Octokit;

namespace APIClients {
    public static class GithubClient {
    public class BuildAssets
    {
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

            EmbedBuilder LatestBuild = new();
            if (prInfo != null) {
                LatestBuild.WithTitle($"PR: #{prInfo.Number} By {prInfo.User.Login}")
                .WithUrl(prInfo.HtmlUrl);
            } else {
                LatestBuild.WithTitle($"Commit: {REF.Sha} By {REF.Commit.Author.Name}")
                .WithUrl($"https://github.com/vita3k/vita3k/commit/{REF.Sha}");
            }

            LatestBuild.WithDescription($"**{prInfo.Title}**\n\n{bodyText}")
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
    }
}
