using System;
using System.Threading.Tasks;
using System.Linq;

using Discord;
using Newtonsoft.Json;

using AppVeyor.POCOs;
using Octokit;
using Vita3KBot;

namespace APIClients {
    public static class AppveyorClient {
        private const string JobURL = "https://ci.appveyor.com/api/projects/Vita3k/vita3k/branch/master";

        public static async Task<Embed> GetLatestBuild() {
            string JobIdValue = await Utils.HttpGet(JobURL);

            var JobJSON = JsonConvert.DeserializeObject<GetJobID>(JobIdValue);
            var JobId = JobJSON.Build.Jobs[0].JobId;

            string FileNameURL = $"https://ci.appveyor.com/api/buildjobs/{JobId}/artifacts/";
            string FileNameValue = await Utils.HttpGet(FileNameURL);

            GetFilesFromJob[] Files = JsonConvert.DeserializeObject<GetFilesFromJob[]>(FileNameValue);
            //Appveyor returns null when it's building, so i'm getting by with this hack until we cache the last build
            //TODO: cache the last build
            var FileName = Files?[0].FileName;
            if (FileName == null) {
                var DummyEmbed = new EmbedBuilder()
                    .WithTitle("Current Build is building")
                    .WithDescription("Please try again later");
                return DummyEmbed.Build();
            }

            var github = new GitHubClient(new ProductHeaderValue("Vita3KBot"));

            var latestRelease = await github.Repository.Release.GetLatest("Vita3K", "Vita3K");
            var prInfo = await GetPRInfo(github, JobJSON.Build);
            var LatestBuild = new EmbedBuilder();

            if (prInfo != null) {
                LatestBuild.WithTitle($"PR: #{prInfo.Number} By {JobJSON.Build.AuthorUserName}")
                .WithUrl(prInfo.HtmlUrl);
            } else {
                LatestBuild.WithTitle($"Commit: {JobJSON.Build.CommitId} By {JobJSON.Build.AuthorUserName}")
                .WithUrl($"https://github.com/vita3k/vita3k/commit/{JobJSON.Build.CommitId}");
            }

            LatestBuild.WithDescription($"{JobJSON.Build.Message}")
            .WithColor(Color.Orange)
            .AddField("Windows", $"[{FileName}](https://ci.appveyor.com/api/buildjobs/{JobId}/artifacts/{FileName})")
            .AddField("Linux", $"[{latestRelease.Assets[0].Name}]({latestRelease.Assets[0].BrowserDownloadUrl})")
            .AddField("Mac", $"[{latestRelease.Assets[1].Name}]({latestRelease.Assets[1].BrowserDownloadUrl})");

            return LatestBuild.Build();
        }

        public static async Task<Issue> GetPRInfo(GitHubClient github, Build build) {

            var request = new SearchIssuesRequest(build.CommitId) {
                Type = IssueTypeQualifier.PullRequest,
                State = ItemState.Closed,
            };
            request.Repos.Add("Vita3K/Vita3K");

            var searchResults = (await github.Search.SearchIssues(request)).Items;

            return searchResults.FirstOrDefault();
        }
    }
}
