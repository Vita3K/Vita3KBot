using System;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Collections.Generic;

using Discord;
using Newtonsoft.Json;

using AppVeyor.POCOs;
using Octokit;
using Vita3KBot;

namespace APIClients {
    public static class AppveyorClient {
        private const string JobURL = "https://ci.appveyor.com/api/projects/Vita3k/vita3k/branch/master";
        private const string WindowsBuildPath = @"./build-win-cache.txt";
        
        public static async Task<Embed> GetLatestBuild() {
            bool cachedLastBuild = false;
            string[] cache = null;

            //Appveyor
            string JobIdValue = await Utils.HttpGet(JobURL);
            GetJobID JobJSON = JsonConvert.DeserializeObject<GetJobID>(JobIdValue);
            string JobId = JobJSON.Build.Jobs[0].JobId;
            string FileNameURL = "";
            string FileName = "";
            //Skip unnecessary HTTP GET requests
            //We do this comparing the latest appveyor build ID to the cached one, using the cache and not the HTTP one
            if (File.Exists(WindowsBuildPath))
            {
                cache = File.ReadAllLines(WindowsBuildPath);
                if (cache.Last().Contains(JobId))
                {
                    string latest = cache.Last();
                    JobId = latest.Split(" ")[0];
                    FileName = latest.Split(" ")[1];
                    cachedLastBuild = true;
                }
            }
            if (!cachedLastBuild) { 
                FileNameURL = $"https://ci.appveyor.com/api/buildjobs/{JobId}/artifacts/";
                string FileNameValue = await Utils.HttpGet(FileNameURL);
                GetFilesFromJob[] Files = JsonConvert.DeserializeObject<GetFilesFromJob[]>(FileNameValue);
                FileName = Files.FirstOrDefault()?.FileName;
            }

            //Github
            GitHubClient github = new GitHubClient(new ProductHeaderValue("Vita3KBot"));
            Release latestRelease = await github.Repository.Release.GetLatest("Vita3K", "Vita3K");
            ReleaseAsset linuxRelease = latestRelease.Assets.Where(release => {
                return release.Name.StartsWith("master-linux");
            }).Last();
            ReleaseAsset macosRelease = latestRelease.Assets.Where(release => {
                return release.Name.StartsWith("master-osx");
            }).Last();

            //If the windows build isn't over then change the latest build to the latest successfull one
            //If we don't have it cached, just send that it is building
            if (FileName == null) {
                if (cache != null)
                {
                    JobId = cache.Last().Split(" ")[0];
                    FileName = cache.Last().Split(" ")[1];
                }
                else
                {
                    EmbedBuilder BuildingEmbed = new EmbedBuilder()
                        .WithTitle("Current Build is building")
                        .WithDescription("Please try again later");
                    return BuildingEmbed.Build();
                }

                EmbedBuilder CachedBuild = new EmbedBuilder();
                CachedBuild.WithTitle("Current Build is building")
                    .WithDescription("But we cached the latest successfull build, Here you go")
                    .WithColor(Color.DarkOrange)
                    .AddField("Windows", $"[{FileName}](https://ci.appveyor.com/api/buildjobs/{JobId}/artifacts/{FileName})")
                    .AddField("Linux", $"[{linuxRelease.Name}]({linuxRelease.BrowserDownloadUrl})")
                    .AddField("Mac", $"[{macosRelease.Name}]({macosRelease.BrowserDownloadUrl})");
                return CachedBuild.Build();
            }

            //If execution gets here that means that we'll send the appveyor link, not  the cached one

            //The cache consists of a txt file containing the JobID and the file name of it, to clear the cache just delete the file completly
            //DON'T JUST DELETE THE LINES, DELETE THE FILE COMPLETLY I AM TOO LAZY TO MANAGE THAT
            //The code below manages the cache, IT DOES NOT RETRIEVE THE LATEST BUILD, I CAN'T BELIEVE I HAVE CONFUSED WITH THIS PIECE OF CODE LIKE 40 TIMES
            //Check if the cache even exists
            if (File.Exists(WindowsBuildPath))
            {
                List<string> lines = new List<string>(File.ReadAllLines(WindowsBuildPath));
                //The cache file has the latest build? if so do nothing otherwise add it to the cache
                //If the JobID is already cached, then that means that everything is up-to-date
                if (!lines.Contains($"{JobId} {FileName}"))
                {
                    lines.Add($"{JobId} {FileName}");
                    File.WriteAllLines(WindowsBuildPath, lines.ToArray());
                }
            }
            else
            {
                //If the cache doesn't exists create one
                File.WriteAllText(WindowsBuildPath, $"{JobId} {FileName}");
            }

            EmbedBuilder LatestBuild = new EmbedBuilder();
            Issue prInfo = await GetPRInfo(github, JobJSON.Build);
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
            .AddField("Linux", $"[{linuxRelease.Name}]({linuxRelease.BrowserDownloadUrl})")
            .AddField("Mac", $"[{macosRelease.Name}]({macosRelease.BrowserDownloadUrl})");

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
