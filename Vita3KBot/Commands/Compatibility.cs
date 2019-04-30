using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

using Discord;
using Discord.Commands;

using APIClients;
using Octokit;

namespace Vita3KBot.Commands {
    public class Compatibility: ModuleBase<SocketCommandContext> {
        // Config
        private const int MaxItemsToDisplay = 8;

        private const string HomebrewRepo = "homebrew-compatibility";
        private const string CommercialRepo = "compatibility";

        private class TitleInfo {
            private static readonly string[] StatusNames = {
                // Priority, display when possible.
                "Playable",
                "Ingame",
                "Intro",
                "Crash",
                "Nothing",

                // Secondary, display if nothing else.
                "Slow",
                "Black Screen",
                "NID Missing",
                "Module Loading Bug",
                "IO Bug",
                "Softlock Bug",
                "Graphics Bug",
                "Shader Bug",
                "Audio Bug",
                "Input Bug",
                "Touch Bug",
                "Savedata Bug",
                "Trophy Bug",
                "Networking Bug",

                // Invalid
                "Invalid",
                "Unknown",
            };

            private readonly Issue _issue;
            public readonly bool IsHomebrew;
            public readonly string Status;
            public string LatestComment;
            public string LatestProfileImage;

            public async Task FetchCommentInfo(GitHubClient client) {
                if (_issue.Comments == 0) return;

                var comments = await client.Issue.Comment.GetAllForIssue("Vita3K",
                    IsHomebrew ? HomebrewRepo : CommercialRepo, _issue.Number);
                var lastComment = comments[_issue.Comments - 1];
                LatestComment = "**" + lastComment.User.Login + "**: " + lastComment.Body;
                LatestProfileImage = lastComment.User.AvatarUrl;
            }

            public TitleInfo(Issue issue) {
                _issue = issue;
                // Repository object is sometimes null on searches. Just guess the repo by the URL.
                IsHomebrew = issue.Url.Contains(HomebrewRepo);
                Console.WriteLine(issue.CommentsUrl + " " + issue.Comments);
                Status = "Unknown";

                var foundStatus = false;
                foreach (var label in issue.Labels) {
                    foreach (var name in StatusNames) {
                        if (name.ToLower().Equals(label.Name.ToLower())) {
                            Status = name;
                            foundStatus = true;
                            break;
                        }
                    }
                    if (foundStatus) break;
                }

                LatestComment = "*No updates on this title.*";
                LatestProfileImage = "";
            }
        }

        [Command("compat")]
        public async Task Compatability([Remainder]string keyword) {
            var github = new GitHubClient(new ProductHeaderValue("Vita3KBot"));

            var search = new SearchIssuesRequest(keyword) {
                Repos = new RepositoryCollection {
                    "Vita3K/homebrew-compatibility",
                    "Vita3K/compatibility"
                }
            };

            var result = await github.Search.SearchIssues(search);
            switch (result.Items.Count) {
                case 0:
                    await ReplyAsync("No games found for search term " + keyword + ".");
                    break;

                case 1: {
                    var issue = result.Items.First();
                    var info = new TitleInfo(issue);
                    await info.FetchCommentInfo(github);
                    var builder = new EmbedBuilder()
                        .WithTitle("*" + issue.Title + "* (" + (info.IsHomebrew ? "Homebrew" : "Commercial") + ")")
                        .WithDescription("Status: **" + info.Status + "**\n\n" + info.LatestComment)
                        .WithColor(Color.Red)
                        .WithUrl(issue.HtmlUrl)
                        .WithCurrentTimestamp();
                    if (info.LatestProfileImage.Length > 0) builder.WithThumbnailUrl(info.LatestProfileImage);

                    await ReplyAsync("", false, builder.Build());
                    break;
                }

                default: {
                    var description = new StringBuilder();
                    for (var a = 0; a < Math.Min(result.Items.Count, MaxItemsToDisplay); a++) {
                        var issue = result.Items[a];
                        var info = new TitleInfo(issue);
                        description.Append("*" + issue.Title + "* (" + (info.IsHomebrew ? "Homebrew" : "Commercial")
                                           + "): **" + info.Status + "**\n");
                    }
                    if (result.Items.Count > MaxItemsToDisplay) description.Append("...");

                    var builder = new EmbedBuilder()
                        .WithTitle("Found " + result.Items.Count + " issues for search term " + keyword + ".")
                        .WithDescription(description.ToString())
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp();

                    await ReplyAsync("", false, builder.Build());
                    break;
                }
            }
        }

        [Command("latest")]
        private async Task App()
        {
            await ReplyAsync(embed: AppveyorClient.GetLatestBuild().Result);
        }

        [Command("update")]
        private async Task Update([Remainder] string titleid)
        {
            //TODO: filter titleID to match valid IDs (e.g. PCSE00000 or PCSB00000) using a regex
            PSNClient.title_id = titleid.ToUpper();
            await ReplyAsync(embed: PSNClient.GetTitlePatch());
        }
    }
}