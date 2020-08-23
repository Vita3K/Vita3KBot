using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Addons.Interactive;
using Discord.Commands;
using Vita3KBot.Commands.Attributes;
using Victoria.Enums;
using Victoria;
using Victoria.EventArgs;

namespace Vita3KBot.Commands
{
        [Group("seek"), InVoiceChannel]
        public class Seek : InteractiveBase {
            [Command, Name("seek")]
            [Summary("Seeks to a particular position in the song (in sec)")]
            public async Task SeekTask(int position)
            {
                var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                Console.WriteLine(
                    $"Track is seekable: {player.Track.CanSeek}\n" +
                    $"Now at: {player.Track.Position}" +
                    $"/{player.Track.Duration}");
                if (player.Track.CanSeek)
                {
                    Emoji emoji = new Emoji("✅");
                    await player.SeekAsync(TimeSpan.FromSeconds(position));
                    await Context.Message.AddReactionAsync(emoji);
                }
                else
                {
                    await ReplyAndDeleteAsync("❌ Cant seek this track.");
                }
            }
        }

        [Group("volume"), InVoiceChannel]
        [Alias("v")]
        public class Volume : ModuleBase<SocketCommandContext> {
            [Command(RunMode = RunMode.Async), Name("volume")]
            [Summary("Sets the volume for the current playing song 0-150")]
            public async Task VolumeTask(ushort value)
            {
                var player = Bot.lavaNode.GetPlayer(Context.Guild);
                if (value < 0 || value > 150) return; // Should return the volume

                await player.UpdateVolumeAsync(value);
                await ReplyAsync("Volume now is set to " + value + "/150");
            }

            [Command(RunMode = RunMode.Async), Name("volume")]
            [Summary("Gets the current volume level")]
            public async Task VolumeTask() {
                if (Bot.lavaNode.HasPlayer(Context.Guild)) {
                    var player = Bot.lavaNode.GetPlayer(Context.Guild);
                    await ReplyAsync("Volume is: " + player.Volume);
                    return;
                }
                await ReplyAsync("I'm not in a voice channel, do `-join` first");
            }
        }

        [Group("pause"), InVoiceChannel]
        public class Pause : ModuleBase<SocketCommandContext> {
            [Command(RunMode = RunMode.Async), Name("pause")]
            [Summary("Pauses the song")]
            public async Task PauseTask()
            {
                var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                await player.PauseAsync();
                await ReplyAsync("Paused");
            }
        }

        [Group("resume"), InVoiceChannel]
        [Alias("Unpause")]
        public class Resume : ModuleBase<SocketCommandContext> {
            [Command(RunMode = RunMode.Async), Name("resume")]
            [Summary("Resumes the song")]
            public async Task ResumeTask()
            {
                var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                if (player.PlayerState == PlayerState.Playing)
                {
                    await ReplyAsync("Already playing " + player.Track.Title);
                }
                else
                {
                    await ReplyAsync($"Resumed {player.Track.Title}");
                    await player.ResumeAsync();
                }
            }
        }

        [Group("now playing"), InVoiceChannel]
        [Alias("np")]
        public class NowPlaying : ModuleBase<SocketCommandContext> {
            [Command(RunMode = RunMode.Async), Name("now playing")]
            [Summary("Get the current playing song")]
            public async Task NowPlayingTask()
            {
                var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                var playList = Context.Guild.Id.PlayList();
                var my = player.Track.Title;
                if (playList.Any()) my += "\nUp next: " + playList[0].Title;
                var build = new EmbedBuilder
                {
                    Title = "Now Playing",
                    Description = my,
                    Color = new Color(213, 0, 249)
                }.Build();
                await ReplyAsync(string.Empty, false, build);
            }
        }

        [Group("clear"), InVoiceChannel]
        public class Clear : ModuleBase<SocketCommandContext> {
            [Command, Name("clear")]
            [Summary("Clears the queue")]
            public async Task ClearTask()
            {
                Context.Guild.Id.PopAll();
                await ReplyAsync("Queue cleared");
            }
        }

        [Group("stop"), InVoiceChannel]
        public class Stop : ModuleBase<SocketCommandContext> {
            [Command(RunMode = RunMode.Async), Name("stop")]
            [Summary("Stops the current playing song.")]
            public async Task StopTask()
            {
                var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                await player.StopAsync();
                await ReplyAsync(
                    "✅ Stopped playing. Your queue is still intact though. Use `clear` to Destroy Queue");
            }
        }

        [Group("disconnect"), InVoiceChannel]
        [Alias("dc")]
        public class Disconnect : ModuleBase<SocketCommandContext> {
            [Command(RunMode = RunMode.Async), Name("disconnect")]
            [Summary("Disconnects bot from voice channel")]
            public async Task LeaveTask()
            {
                if (Bot.lavaNode.GetPlayer(Context.Guild).PlayerState == PlayerState.Playing) {
                    var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                    await player.StopAsync();
                    await ReplyAsync(
                    "✅ Stopped playing. Your queue is still intact though. Use `clear` to Destroy Queue");
                }
                await Bot.lavaNode.LeaveAsync(Bot.lavaNode.GetPlayer(Context.Guild).VoiceChannel);
            }
        }

        [Group("queue"), InVoiceChannel]
        [Alias("q")]
        public class Queue : ModuleBase<SocketCommandContext> {
            [Command(RunMode = RunMode.Async), Name("queue")]
            [Summary("Prints the current queue")]
            public async Task QueueTask()
            {
                var my = string.Empty;
                var p = Context.Guild.Id.PlayList();
                var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                if (!p.Any() && (player.PlayerState != PlayerState.Playing))
                {
                    await ReplyAsync("The Queue is Empty.");
                }
                else
                {
                    if (player.PlayerState == PlayerState.Playing)
                        my +=
                            $"👉 [{player.Track.Title}]({player.Track.Url}) **{player.Track.Duration}**\n";

                    for (var i = 0; i < Math.Min(p.Count, 10); i++)
                        my += $"**{i + 1}**. [{p[i].Title}]({p[i].Url}) **{p[i].Duration}**\n";
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
        }

        [Group("skip"), InVoiceChannel]
        public class Skip : InteractiveBase {
            [Command(RunMode = RunMode.Async), Name("skip")]
            [Summary("Skips the current playing song. Broken AF.")]
            public async Task SkipTask()
            {
                var player = Bot.lavaNode.GetPlayer(Context.Guild) ??
                             await Bot.lavaNode.JoinAsync((Context.User as IGuildUser)?.VoiceChannel);
                var final = await ReplyAsync("🧐 Searching");
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
                    if (player.PlayerState == PlayerState.Playing) await player.StopAsync();

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
        }

        [Group("play"), InVoiceChannel]
        [Alias("p")]
        public class Play : InteractiveBase {
            [Command(RunMode = RunMode.Async), Name("play")]
            [Summary("Plays a song from tong of sources")]
            public async Task PlayTask([Remainder] string query)
            {
                var final = await ReplyAsync("🧐 Searching");
                var voiceState = Context.User as IVoiceState;
                LavaPlayer player;
                if (Bot.lavaNode.HasPlayer(Context.Guild)) {
                    player = Bot.lavaNode.GetPlayer(Context.Guild);
                } else {
                    player = await Bot.lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                }
                var response = await Bot.lavaNode.SearchYouTubeAsync(query);

                if (response.LoadStatus == LoadStatus.LoadFailed)
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

                if (response.LoadStatus == LoadStatus.NoMatches)
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

                var tracks = response.LoadStatus == LoadStatus.PlaylistLoaded
                    ? allTracks
                    : allTracks.Take(Math.Min(10, allTracks.Count)).ToList();

                if (response.LoadStatus == LoadStatus.PlaylistLoaded)
                {
                    foreach (var track in tracks) Context.Guild.Id.PushTrack(track);

                    if (player.PlayerState != PlayerState.Playing)
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
                        my += $"{i + 1}. [{tracks[i].Title}]({tracks[i].Url})  **Duration: {tracks[i].Duration}**\n";

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

                    if (player.PlayerState != PlayerState.Playing)
                    {
                        var lavalinkTrack = Context.Guild.Id.PopTrack();
                        await player.PlayAsync(lavalinkTrack);
                        await final.ModifyAsync(x =>
                        {
                            x.Embed = null;
                            x.Content = $"✅ Playing **{lavalinkTrack.Title}** now";
                        });
                    }
                    else
                    {
                        await final.ModifyAsync(x =>
                        {
                            x.Embed = null;
                            x.Content = $"✅ Added **{track.Title}** to Queue.";
                        });
                    }
                }
            }
        }
    public class MusicModule {
        public static async Task PlayNextTrack(TrackEndedEventArgs args) {
            var player = args.Player;
            var final = await player.TextChannel.SendMessageAsync("🧐 Playing the next song");
            try {
                var track = player.TextChannel.Guild.Id.PopTrack();
                var playing = new EmbedBuilder {
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
            } catch (Exception) {
                if (player.PlayerState == PlayerState.Playing) await player.StopAsync();

                await final.ModifyAsync(x => x.Content = "Queue has ended");
            }
        }
    }
}
