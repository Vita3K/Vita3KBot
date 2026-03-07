using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using APIClients;
using Octokit;
using DC = Discord.Commands;

namespace Vita3KBot.Commands {

    // ── Shared logic ─────────────────────────────────────────────

    internal static class CompatUtils {
        internal const int MaxItemsToDisplay = 8;
        internal const int MaxDescriptionLength = 4096;
        internal const string HomebrewRepo = "homebrew-compatibility";
        internal const string CommercialRepo = "compatibility";

        internal static readonly string[] StatusNames = {
            // Priority, display when possible.
            "Playable", "Ingame", "Ingame +", "Ingame -",
            "Menu", "Intro", "Bootable", "Crash", "Nothing",
            // Secondary, display if nothing else.
            "Slow", "Black Screen", "NID Missing", "Module Loading Bug",
            "IO Bug", "Softlock Bug", "Graphics Bug", "Shader Bug",
            "Audio Bug", "Input Bug", "Touch Bug", "Savedata Bug",
            "Trophy Bug", "Networking Bug",
            // Invalid
            "Invalid", "Unknown",
        };

        internal class TitleInfo {
            private readonly Issue _issue;
            public readonly bool IsHomebrew;
            public readonly string Status;
            public readonly Color LabelColor;
            public string LatestComment;
            public string LatestProfileImage;

            public async Task FetchCommentInfo(GitHubClient client) {
                if (_issue.Comments == 0) return;
                var comments = await client.Issue.Comment.GetAllForIssue(
                    "Vita3K", IsHomebrew ? HomebrewRepo : CommercialRepo, _issue.Number);
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

        internal static async Task<(string message, Embed embed)?> SearchCompat(string keyword) {
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
            var filteredResults = searchResults
                .Where(x => keywords.Any(y => x.Title.ToLower().Contains(y))).ToList();

            switch (filteredResults.Count) {
                case 0:
                    return ($"No games found for search term {keyword}.", null);

                case 1: {
                    var issue = filteredResults.First();
                    var info = new TitleInfo(issue);
                    await info.FetchCommentInfo(github);
                    var description = "Status: **" + info.Status + "**\n\n" + info.LatestComment;
                    if (description.Length > MaxDescriptionLength)
                        description = description[..(MaxDescriptionLength - 3)] + "...";

                    var builder = new EmbedBuilder()
                        .WithTitle($"*{issue.Title}* ({(info.IsHomebrew ? "Homebrew" : "Commercial")})")
                        .WithDescription(description)
                        .WithColor(info.LabelColor)
                        .WithUrl(issue.HtmlUrl)
                        .WithCurrentTimestamp();
                    if (info.LatestProfileImage.Length > 0)
                        builder.WithThumbnailUrl(info.LatestProfileImage);

                    return (null, builder.Build());
                }

                default: {
                    var description = new StringBuilder();
                    for (var i = 0; i < Math.Min(filteredResults.Count, MaxItemsToDisplay); i++) {
                        var issue = filteredResults[i];
                        var info = new TitleInfo(issue);
                        var homebrewText = info.IsHomebrew ? "Homebrew" : "Commercial";
                        description.Append($"*[{issue.Title}]({issue.HtmlUrl})* ({homebrewText}): **{info.Status}**\n");
                    }
                    if (filteredResults.Count > MaxItemsToDisplay) description.Append("...");

                    var builder = new EmbedBuilder()
                        .WithTitle($"Found {filteredResults.Count} issues for search term {keyword}.")
                        .WithDescription(description.ToString())
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp();

                    return (null, builder.Build());
                }
            }
        }
    }

    // ── Prefix commands ──────────────────────────────────────────

    [DC.Group("compat")]
    public class CompatibilityPrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Command, DC.Name("compat")]
        [DC.Summary("Provides a compatibility report of the game.")]
        public async Task Compatibility([DC.Remainder, DC.Summary("Game name to search")] string keyword) {
            var result = await CompatUtils.SearchCompat(keyword);
            if (result == null) return;
            var (message, embed) = result.Value;
            await ReplyAsync(message ?? "", false, embed);
        }
    }

    [DC.Group("update")]
    public class UpdatePrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Command, DC.Name("update")]
        [DC.Summary("Provides PSN update information for the game.")]
        public async Task GetUpdate([DC.Remainder, DC.Summary("Title ID of the game")] string titleId)
            => await ReplyAsync(embed: PSNClient.GetTitlePatch(titleId.ToUpper()));
    }

    // ── Slash commands ───────────────────────────────────────────

    public class CompatibilitySlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("compat", "Provides a compatibility report of the game.")]
        public async Task Compatibility(
                [Discord.Interactions.Summary("keyword", "Game name to search")] string keyword) {
            // Defer since GitHub search may take a moment
            await DeferAsync();
            var result = await CompatUtils.SearchCompat(keyword);
            if (result == null) return;
            var (message, embed) = result.Value;
            await FollowupAsync(message ?? "", embed: embed);
        }
    }

    public class UpdateSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("update", "Provides PSN update information for the game.")]
        public async Task GetUpdate(
                [Discord.Interactions.Summary("title_id", "Title ID of the game (e.g. PCSE00000)")] string titleId)
            => await RespondAsync(embed: PSNClient.GetTitlePatch(titleId.ToUpper()));
    }
}
