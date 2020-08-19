using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Vita3KBot.Preconditions;
using SharpLink;
using SharpLink.Enums;

namespace Vita3KBot.Modules
{
    [InVoiceChannel]
    public class Music : InteractiveBase
    {
        private readonly LavalinkManager _lavalinkManager;

        public Music(LavalinkManager lavalinkManager)
        {
            _lavalinkManager = lavalinkManager;
        }

        [Command("seek")]
        [Summary("Seeks to a particular position in the song (in sec)")]
        public async Task SeekTask(int position)
        {
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            Console.WriteLine(
                $"Track is seekable: {player.CurrentTrack.IsSeekable}\n" +
                $"Now at: {TimeSpan.FromMilliseconds(player.CurrentPosition)}" +
                $"/{TimeSpan.FromMilliseconds(player.CurrentTrack.Length.Milliseconds)}");
            if (player.CurrentTrack.IsSeekable)
            {
                await player.SeekAsync(position * 1000);
                await ReplyAsync("<:check:462378657114226695>");
            }
            else
            {
                await ReplyAndDeleteAsync("<:uncheck:462379632004562965> Cant seek this track.");
            }
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Summary("Sets the volume for the current playing song 0-150")]
        public async Task VolumeTask(uint value = 98450)
        {
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            if (value == 98450) return; // Should return the volume

            await player.SetVolumeAsync(value);
            await ReplyAsync("Volume now is set to " + value + "/150");
        }

        [Command("pause", RunMode = RunMode.Async)]
        [Summary("Pauses the song")]
        public async Task PauseTask()
        {
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            await player.PauseAsync();
            await ReplyAsync("Paused");
        }

        [Command("resume", RunMode = RunMode.Async)]
        [Alias("Unpause")]
        [Summary("Resumes the song")]
        public async Task ResumeTask()
        {
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            if (player.Playing)
            {
                await ReplyAsync("Already playing " + player.CurrentTrack.Title);
            }
            else
            {
                await ReplyAsync($"Resumed {player.CurrentTrack.Title}");
                await player.ResumeAsync();
            }
        }

        [Command("Now playing", RunMode = RunMode.Async)]
        [Summary("Get the current playing song")]
        public async Task NowPlayingTask()
        {
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            var playList = Context.Guild.Id.PlayList();
            var my = player.CurrentTrack.Title;
            if (playList.Any()) my += "\nUp next: " + playList[0].Title;
            var build = new EmbedBuilder
            {
                Title = "Now Playing",
                Description = my,
                Color = new Color(213, 0, 249)
            }.Build();
            await ReplyAsync(string.Empty, false, build);
        }

        [Command("clear")]
        [Summary("Clears the queue")]
        public async Task ClearTask()
        {
            Context.Guild.Id.PopAll();
            await ReplyAsync("Queue cleared");
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Summary("Stops the current playing song.")]
        public async Task StopTask()
        {
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            await player.StopAsync();
            await ReplyAsync(
                "<:check:462378657114226695> Stopped playing. Your queue is still intact though. Use `clear` to Destroy Queue");
        }

        [Command("disconnect", RunMode = RunMode.Async)]
        public async Task LeaveTask()
        {
            if (_lavalinkManager.GetPlayer(Context.Guild.Id).Playing) await StopTask();
            await _lavalinkManager.LeaveAsync(Context.Guild.Id);
        }

        [Command("Queue", RunMode = RunMode.Async)]
        [Summary("Prints the current queue")]
        public async Task QueueTask()
        {
            var my = string.Empty;
            var p = Context.Guild.Id.PlayList();
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            if (!p.Any() && !player.Playing)
            {
                await ReplyAsync("The Queue is Empty.");
            }
            else
            {
                if (player.Playing)
                    my +=
                        $"👉 [{player.CurrentTrack.Title}]({player.CurrentTrack.Url}) **{player.CurrentTrack.Length}**\n";

                for (var i = 0; i < Math.Min(p.Count, 10); i++)
                    my += $"**{i + 1}**. [{p[i].Title}]({p[i].Url}) **{p[i].Length}**\n";
                var build = new EmbedBuilder
                {
                    Title = "Current Queue",
                    Description = my,
                    Color = new Color(213, 0, 249),
                    Footer = new EmbedFooterBuilder
                    {
                        Text = p.Count + " songs in the queue"
                    }
                }.Build();

                await ReplyAsync(string.Empty, false, build);
            }
        }

        [Command("skip", RunMode = RunMode.Async)]
        [Summary("Skips the current playing song. Broken AF.")]
        public async Task SkipTask()
        {
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            var final = await ReplyAsync("<a:loader:461159122575032331> Searching");
            try
            {
                var track = Context.Guild.Id.PopTrack();
                var playing = new EmbedBuilder
                {
                    Title = "Now Playing",
                    Description = track.Title,
                    Color = new Color(213, 0, 249)
                }.Build();

                await player.StopAsync();
                await player.PlayAsync(track);
                await final.ModifyAsync(x =>
                {
                    x.Embed = playing;
                    x.Content = null;
                });
            }
            catch (Exception)
            {
                if (player.Playing) await player.StopAsync();

                await final.ModifyAsync(x => x.Content = "Queue Empty");
            }

            var build = new EmbedBuilder
            {
                Title = "Warning",
                Description =
                    "This command is sorta broken so if you face any issues just `disconnect` and `play` again.",
                Color = new Color(213, 0, 249)
            }.Build();
            await ReplyAndDeleteAsync(string.Empty, false, build, TimeSpan.FromSeconds(6));
        }

        [Command("play", RunMode = RunMode.Async)]
        [Summary("Plays a song from tong of sources")]
        public async Task PlayTask([Remainder] string query)
        {
            var result = Uri.TryCreate(query, UriKind.Absolute, out var uriResult);
            var identifier = result || query.Contains("ytsearch:") || query.Contains("scsearch")
                ? uriResult.ToString()
                : $"ytsearch:{query}";

            var final = await ReplyAsync("<a:loader:461159122575032331> Searching");
            var player = _lavalinkManager.GetPlayer(Context.Guild.Id) ??
                         await _lavalinkManager.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
            var response = await _lavalinkManager.GetTracksAsync(identifier);

            if (response.LoadType == LoadType.LoadFailed)
            {
                await final.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = new EmbedBuilder
                    {
                        Title = "Failed to load responses.",
                        Description =
                            "The url is not playable.",
                        Color = new Color(213, 0, 249),
                        Footer = new EmbedFooterBuilder
                        {
                            Text =
                                "To go specific youtube search try adding `ytsearch:`before your search string." +
                                " Eg: play ytsearch:Allen Walker"
                        }
                    }.Build();
                });
                return;
            }

            if (response.LoadType == LoadType.NoMatches)
            {
                await final.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = new EmbedBuilder
                    {
                        Title = "Failed to load responses.",
                        Description =
                            "The search found no such tracks",
                        Color = new Color(213, 0, 249),
                        Footer = new EmbedFooterBuilder
                        {
                            Text =
                                "To go specific soundcloud search try adding `scsearch:`before your search string." +
                                " Eg: play scsearch:Allen Walker"
                        }
                    }.Build();
                });
                return;
            }

            var allTracks = response.Tracks.ToList();

            var tracks = response.LoadType == LoadType.PlaylistLoaded
                ? allTracks
                : allTracks.Take(Math.Min(10, allTracks.Count)).ToList();

            if (response.LoadType == LoadType.PlaylistLoaded)
            {
                foreach (var track in tracks) Context.Guild.Id.PushTrack(track);

                if (!player.Playing)
                {
                    var lavalinkTrack = Context.Guild.Id.PopTrack();
                    await player.PlayAsync(lavalinkTrack);
                    await final.ModifyAsync(x =>
                    {
                        x.Embed = new EmbedBuilder
                        {
                            Description =
                                $"👉 **{lavalinkTrack.Title}** \nAdded {tracks.Count - 1} tracks to the queue",
                            Color = new Color(213, 0, 249),
                            Title = "Now Playing"
                        }.Build();
                        x.Content = null;
                    });
                }
                else
                {
                    await final.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = $"Added **{tracks.Count}** songs to Queue.";
                    });
                }
            }
            else
            {
                var my = string.Empty;
                for (var i = 0; i < tracks.Count; i++)
                    my += $"{i + 1}. [{tracks[i].Title}]({tracks[i].Url})  **Duration: {tracks[i].Length}**\n";

                var build = new EmbedBuilder
                {
                    Title = "Make your choice",
                    Description = my,
                    Color = new Color(213, 0, 249)
                }.Build();

                await final.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = build;
                });

                var reply = await NextMessageAsync();
                if (!int.TryParse(reply.Content, out var good) || good > tracks.Count)
                {
                    await final.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = "Invalid Response";
                    });
                    return;
                }


                var track = tracks[good - 1];
                Context.Guild.Id.PushTrack(track);

                if (!player.Playing)
                {
                    var lavalinkTrack = Context.Guild.Id.PopTrack();
                    await player.PlayAsync(lavalinkTrack);
                    await final.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = $"<:check:462378657114226695> Playing **{lavalinkTrack.Title}** now";
                    });
                }
                else
                {
                    await final.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = $"<:check:462378657114226695> Added **{track.Title}** to Queue.";
                    });
                }
            }
        }
    }
}