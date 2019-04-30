using System;
using System.Threading.Tasks;

using Discord;
using Newtonsoft.Json;

using AppVeyor.POCOs;
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

            var LatestBuild = new EmbedBuilder()
                .WithTitle($"PR: {JobJSON.Build.Message.Substring(JobJSON.Build.Message.IndexOf("#"), 4)} By {JobJSON.Build.AuthorUserName}")
                .WithUrl($"https://github.com/vita3k/vita3k/pull/{JobJSON.Build.Message.Substring(JobJSON.Build.Message.IndexOf("#") + 1, 3)}")
                .WithDescription($"{JobJSON.Build.Message}")
                .WithColor(Color.Orange)
                .AddField("Windows", $"[{FileName}](https://ci.appveyor.com/api/buildjobs/{JobId}/artifacts/{FileName})")
                .AddField("Linux", "[Vita3K-linux-nightly.zip](https://github.com/Vita3K/Vita3K-builds/raw/master/Vita3K-linux-nightly.zip)")
                .AddField("Mac", "[Vita3K-mac-nightly.zip](https://github.com/Vita3K/Vita3K-builds/raw/master/Vita3K-mac-nightly.zip)");

            return LatestBuild.Build();
        }
    }
}
