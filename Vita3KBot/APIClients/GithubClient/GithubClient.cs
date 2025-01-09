using System.Threading.Tasks;
using System.Linq;

using Discord;

using Octokit;

namespace APIClients {
    public static class GithubClient {
        
        public static async Task<Embed> GetLatestBuild() {

            GitHubClient github = new GitHubClient(new ProductHeaderValue("Vita3KBot"));
            Release latestRelease = await github.Repository.Release.Get("Vita3k", "Vita3k", "continuous");
            string releaseTime = $"Published at {latestRelease.PublishedAt:u}";
            ReleaseAsset windowsRelease = latestRelease.Assets.Where(release => {
                return release.Name.StartsWith("windows-latest");
            }).First();
            ReleaseAsset linuxRelease = latestRelease.Assets.Where(release => {
                return release.Name.StartsWith("ubuntu-22.04");
            }).First();
            ReleaseAsset appimageRelease = latestRelease.Assets.Where(release => {
                return release.Name.StartsWith("Vita3K-x86_64.AppImage");
            }).First();
            ReleaseAsset macosRelease = latestRelease.Assets.Where(release => {
                return release.Name.StartsWith("macos-latest");
            }).First();

            string commit = latestRelease.Body.Substring(latestRelease.Body.IndexOf(":") + 1).Trim();
            commit = commit.Substring(0, commit.IndexOf("\n"));
            GitHubCommit REF = await github.Repository.Commit.Get("Vita3k", "Vita3k", commit);
            Issue prInfo = await GetPRInfo(github, commit);

            EmbedBuilder LatestBuild = new EmbedBuilder();
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
