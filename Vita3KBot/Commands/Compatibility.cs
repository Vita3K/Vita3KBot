using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

using Discord;
using Discord.Commands;

using APIClients;
using Octokit;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Vita3KBot.Commands {
    [Group("compat")]
    public class Compatibility: ModuleBase<SocketCommandContext> {
        // Config
        private const int MaxItemsToDisplay = 8;
        private const int MaxDescriptionLength = 4096;

        private const string HomebrewRepo = "homebrew-compatibility";
        private const string CommercialRepo = "compatibility";

        private class TitleInfo {
            private static readonly string[] StatusNames = {
                // Priority, display when possible.
                "Playable",
                "Ingame",
                "Ingame +",
                "Ingame -",
                "Menu",
                "Intro",
                "Bootable",
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
            public readonly Color LabelColor;
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
                Status = "Unknown";
                LabelColor = Color.Orange;

                var foundStatus = false;
                foreach (var name in StatusNames) {
                    foreach (var label in issue.Labels) {
                        if (name.ToLower().Equals(label.Name.ToLower())) {
                            Status = name;
                            LabelColor = new Color(UInt32.Parse(label.Color, NumberStyles.HexNumber));
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

        [Command, Name("compat")]
        [Summary("Provides a compatibility report of the game.")]
        public async Task Compatability([Remainder, Summary("Game name to search")]string keyword) {
            var github = new GitHubClient(new ProductHeaderValue("Vita3KBot"));
            var sanitized = Regex.Replace(keyword, @"[^a-zA-Z0-9\s]", " ");

            var search = new SearchIssuesRequest(sanitized) {
                Repos = new RepositoryCollection {
                    "Vita3K/homebrew-compatibility",
                    "Vita3K/compatibility"
                },
                State = ItemState.Open,
            };
            
            var keywords = sanitized.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var searchResults = (await github.Search.SearchIssues(search)).Items;
            // The following makes sure all the keywords are contained in each title, and removes the ones that don't.
            var filteredResults = searchResults.Where(
                x => keywords.Any(y => x.Title.ToLower().Contains(y))).ToList();
            switch (filteredResults.Count) {
                case 0:
                    await ReplyAsync("No games found for search term " + keyword + ".");
                    break;

                case 1: {
                    var issue = filteredResults.First();
                    var info = new TitleInfo(issue);
                    await info.FetchCommentInfo(github);
                    var description = "Status: **" + info.Status + "**\n\n" + info.LatestComment;
                    if (description.Length > 4096) {
                        description = description.Substring(0, MaxDescriptionLength - 3) + "...";
                    }
                    var builder = new EmbedBuilder()
                        .WithTitle("*" + issue.Title + "* (" + (info.IsHomebrew ? "Homebrew" : "Commercial") + ")")
                        .WithDescription(description)
                        .WithColor(info.LabelColor)
                        .WithUrl(issue.HtmlUrl)
                        .WithCurrentTimestamp();
                    if (info.LatestProfileImage.Length > 0) builder.WithThumbnailUrl(info.LatestProfileImage);

                    await ReplyAsync("", false, builder.Build());
                    break;
                }

                default: {
                    var description = new StringBuilder();
                    for (var a = 0; a < Math.Min(filteredResults.Count, MaxItemsToDisplay); a++) {
                        var issue = filteredResults[a];
                        var info = new TitleInfo(issue);
                        var homebrewText = info.IsHomebrew ? "Homebrew" : "Commercial";
                        description.Append($"*[{issue.Title}]({issue.HtmlUrl})* ({homebrewText}): **{info.Status}**\n");
                    }
                    if (filteredResults.Count > MaxItemsToDisplay) description.Append("...");

                    var builder = new EmbedBuilder()
                        .WithTitle("Found " + filteredResults.Count + " issues for search term " + keyword + ".")
                        .WithDescription(description.ToString())
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp();

                    await ReplyAsync("", false, builder.Build());
                    break;
                }
            }
        }

    }
    [Group("update")]
    public class Update : ModuleBase<SocketCommandContext> {
        [Command, Name("update")]
        [Summary("Provides PSN update information for the game.")]
        private async Task GetUpdate([Remainder, Summary("Title ID of the game")] string titleId)
        {
            //TODO: filter titleID to match valid IDs (e.g. PCSE00000 or PCSB00000) using a regex
            await ReplyAsync(embed: PSNClient.GetTitlePatch(titleId.ToUpper()));
        }
    }
}
