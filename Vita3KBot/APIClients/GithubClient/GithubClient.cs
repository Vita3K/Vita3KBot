using System.Threading.Tasks;
using System.Linq;

using Discord;

using Octokit;

namespace APIClients {
    public static class GithubClient {
        
        public static async Task<Embed> GetLatestBuild() {

            GitHubClient github = new GitHubClient(new ProductHeaderValue("Vita3KBot"));
            Release latestRelease = await github.Repository.Release.Get("Vita3K", "Vita3K-builds", "latest");
            string releaseTime = $"Published at {latestRelease.PublishedAt:u}";
            ReleaseAsset windowsRelease = latestRelease.Assets.Where(release => {
                return release.Name.EndsWith("windows.7z");
            }).First();
            ReleaseAsset linuxRelease = latestRelease.Assets.Where(release => {
                return release.Name.EndsWith("ubuntu.7z");
            }).First();
            ReleaseAsset appimageRelease = latestRelease.Assets.Where(release => {
                return release.Name.EndsWith("Vita3K-x86_64.AppImage");
            }).First();
            ReleaseAsset macosRelease = latestRelease.Assets.Where(release => {
                return release.Name.EndsWith("macos.dmg");
            }).First();

            string commitSha = latestRelease.Body.Substring(latestRelease.Body.IndexOf("https://github.com/Vita3K/Vita3K/commit/") + 46).Trim();
            commitSha = commitSha.Substring(0, commitSha.IndexOf(")"));
            GitHubCommit REF = await github.Repository.Commit.Get("Vita3k", "Vita3k", commitSha);
            Issue prInfo = await GetPRInfo(github, commitSha);

            EmbedBuilder LatestBuild = new();
            if (prInfo != null) {
                LatestBuild.WithTitle($"PR: #{prInfo.Number} By {prInfo.User.Login}")
                .WithUrl(prInfo.HtmlUrl);
            } else {
                LatestBuild.WithTitle($"Commit: {REF.Sha} By {REF.Commit.Author.Name}")
                .WithUrl($"https://github.com/vita3k/vita3k/commit/{REF.Sha}");
            }
            LatestBuild.WithDescription($"{REF.Commit.Message}")
            .WithColor(Color.Orange)
            .AddField("Windows", $"[{windowsRelease.Name}]({windowsRelease.BrowserDownloadUrl})")
            .AddField("Linux", $"[{linuxRelease.Name}]({linuxRelease.BrowserDownloadUrl}), [{appimageRelease.Name}]({appimageRelease.BrowserDownloadUrl})")
            .AddField("Mac", $"[{macosRelease.Name}]({macosRelease.BrowserDownloadUrl})")
            .WithFooter(releaseTime);

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
    }
}
