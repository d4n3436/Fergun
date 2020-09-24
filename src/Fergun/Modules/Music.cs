using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Services;
using Victoria;

namespace Fergun.Modules
{
    [RequireBotPermission(Constants.MinimunRequiredPermissions)]
    [Ratelimit(3, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    [UserMustBeInVoice("lyrics", ErrorMessage = "UserNotInVC")]
    public class Music : FergunBase
    {
        private readonly MusicService _musicService;

        public Music(MusicService musicService)
        {
            _musicService = musicService;
        }

        [RequireBotPermission(GuildPermission.Connect, ErrorMessage = "BotRequireConnect")]
        [Command("join", RunMode = RunMode.Async)]
        [Summary("joinSummary")]
        public async Task<RuntimeResult> Join()
        {
            var user = Context.User as SocketGuildUser;
            await SendEmbedAsync(await _musicService.JoinAsync(Context.Guild, user.VoiceChannel, Context.Channel as ITextChannel));
            return FergunResult.FromSuccess();
        }

        [Command("leave")]
        [Summary("leaveSummary")]
        [Alias("disconnect", "quit", "exit")]
        public async Task Leave()
        {
            var user = Context.User as SocketGuildUser;
            bool connected = await _musicService.LeaveAsync(Context.Guild, user.VoiceChannel);
            await SendEmbedAsync(!connected ? Locate("BotNotConnected") : string.Format(Locate("LeftVC"), Format.Bold(user.VoiceChannel.Name)));
        }

        [Command("loop")]
        [Summary("loopSummary")]
        [Example("10")]
        public async Task Loop([Summary("loopParam1")] uint? count = null)
        {
            await SendEmbedAsync(_musicService.Loop(count, Context.Guild, Context.Channel as ITextChannel));
        }

        [LongRunning]
        [Command("lyrics", RunMode = RunMode.Async)]
        [Summary("lyricsSummary")]
        [Alias("l")]
        [Example("never gonna give you up")]
        public async Task<RuntimeResult> Lyrics([Remainder, Summary("lyricsParam1")] string query = null)
        {
            bool keepHeaders = false;
            if (string.IsNullOrWhiteSpace(query) || query.ToLowerInvariant().Trim() == "-headers")
            {
                query = null;
            }
            else if (query.EndsWith("-headers", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Substring(0, query.Length - 8);
                keepHeaders = true;
            }
            (var error, var lyrics) = await _musicService.GetLyricsAsync(query, keepHeaders, Context.Guild,
                Context.Channel as ITextChannel, Context.User.Activities?.OfType<SpotifyGame>()?.FirstOrDefault());

            if (error != null)
            {
                return FergunResult.FromError(error);
            }
            var pages = new List<PaginatedMessage.Page>();
            foreach (var item in lyrics.Skip(2))
            {
                pages.Add(new PaginatedMessage.Page
                {
                    Description = item,
                    Fields = new List<EmbedFieldBuilder>()
                    {
                        new EmbedFieldBuilder { Name = "Links", Value = lyrics.ElementAt(1) }
                    },
                });
            }

            var pager = new PaginatedMessage
            {
                Color = new Color(FergunConfig.EmbedColor),
                Title = lyrics.First().Truncate(EmbedBuilder.MaxTitleLength),
                Pages = pages,
                Options = new PaginatedAppearanceOptions
                {
                    FooterFormat = $"{Locate("LyricsByGenius")} - {Locate("PaginatorFooter")}",
                    Timeout = TimeSpan.FromMinutes(10),
                    ActionOnTimeout = ActionOnTimeout.DeleteReactions
                }
            };

            await PagedReplyAsync(pager, ReactionList.Default);

            return FergunResult.FromSuccess();
        }

        [Command("move")]
        [Summary("moveSummary")]
        public async Task Move()
        {
            var user = Context.User as SocketGuildUser;
            await SendEmbedAsync(await _musicService.MoveAsync(Context.Guild, user.VoiceChannel, Context.Channel as ITextChannel));
        }

        [Command("nowplaying")]
        [Summary("nowplayingSummary")]
        [Alias("np")]
        public async Task Nowplaying()
        {
            await SendEmbedAsync(_musicService.GetCurrentTrack(Context.Guild, Context.Channel as ITextChannel));
        }

        [Command("pause")]
        [Summary("pauseSummary")]
        public async Task Pause()
        {
            await SendEmbedAsync(await _musicService.PauseOrResumeAsync(Context.Guild, Context.Channel as ITextChannel));
        }

        [RequireBotPermission(GuildPermission.Speak, ErrorMessage = "BotRequireSpeak")]
        [LongRunning]
        [Command("play", RunMode = RunMode.Async)]
        [Summary("playSummary")]
        [Alias("p")]
        [Example("darude sandstorm")]
        public async Task<RuntimeResult> Play([Remainder, Summary("playParam1")] string query)
        {
            var user = Context.User as SocketGuildUser;
            //await ReplyAsync(await _musicService.PlayAsync(query, Context.GuildId));
            var (result, tracks) = await _musicService.PlayAsync(query, Context.Guild, user.VoiceChannel, Context.Channel as ITextChannel);
            if (tracks == null)
            {
                await SendEmbedAsync(result);
            }
            else
            {
                LavaTrack selectedTrack;
                bool trackSelection = GetGuildConfig()?.TrackSelection ?? FergunConfig.TrackSelectionDefault;
                if (trackSelection)
                {
                    string list = "";
                    // Limit to 10, for now
                    int count = Math.Min(10, tracks.Count);

                    for (int i = 0; i < count; i++)
                    {
                        list += $"{i + 1}. {tracks[i].ToTrackLink()}\n";
                    }

                    var builder = new EmbedBuilder()
                        .WithAuthor(user)
                        .WithTitle(Locate("SelectTrack"))
                        .WithDescription(list)
                        .WithColor(FergunConfig.EmbedColor);

                    await ReplyAsync(embed: builder.Build());

                    var response = await NextMessageAsync(true, true, TimeSpan.FromMinutes(1));

                    if (response == null)
                    {
                        return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("SearchCanceled")}");
                    }
                    if (!int.TryParse(response.Content, out int option))
                    {
                        return FergunResult.FromError($"{Locate("InvalidOption")} {Locate("SearchCanceled")}");
                    }
                    if (option < 1 || option > count)
                    {
                        return FergunResult.FromError($"{Locate("OutOfIndex")} {Locate("SearchCanceled")}");
                    }
                    selectedTrack = tracks[option - 1];
                }
                else
                {
                    // people don't know how to select a track...
                    selectedTrack = tracks[0];
                }
                var result2 = await _musicService.PlayTrack(Context.Guild, user.VoiceChannel, Context.Channel as ITextChannel, selectedTrack);
                await SendEmbedAsync(result2);
            }
            return FergunResult.FromSuccess();
        }

        [Command("queue")]
        [Summary("queueSummary")]
        [Alias("q")]
        public async Task Queue()
        {
            await SendEmbedAsync(_musicService.GetQueue(Context.Guild, Context.Channel as ITextChannel));
        }

        [Command("remove")]
        [Summary("removeSummary")]
        [Alias("delete")]
        [Example("2")]
        public async Task Remove([Summary("removeParam1")] int index)
        {
            await SendEmbedAsync(_musicService.RemoveAt(Context.Guild, Context.Channel as ITextChannel, index));
        }

        [Command("replay")]
        [Summary("replaySummary")]
        public async Task Replay()
        {
            await SendEmbedAsync(await _musicService.ReplayAsync(Context.Guild, Context.Channel as ITextChannel));
        }

        [Command("resume")]
        [Summary("resumeSummary")]
        public async Task Resume()
        {
            await SendEmbedAsync(await _musicService.ResumeAsync(Context.Guild, Context.Channel as ITextChannel));
        }

        [Command("seek")]
        [Summary("seekSummary")]
        [Alias("skipto", "goto")]
        [Example("3:14")]
        public async Task Seek([Summary("seekParam1")] string time)
        {
            await SendEmbedAsync(await _musicService.SeekAsync(Context.Guild, Context.Channel as ITextChannel, time));
        }

        [Command("shuffle")]
        [Summary("shuffleSummary")]
        public async Task Shuffle()
        {
            await SendEmbedAsync(_musicService.Shuffle(Context.Guild, Context.Channel as ITextChannel));
        }

        [Command("skip")]
        [Summary("skipSummary")]
        [Alias("s")]
        public async Task Skip()
        {
            await SendEmbedAsync(await _musicService.SkipAsync(Context.Guild, Context.Channel as ITextChannel));
        }

        [Command("stop")]
        [Summary("stopSummary")]
        public async Task Stop()
        {
            await SendEmbedAsync(await _musicService.StopAsync(Context.Guild, Context.Channel as ITextChannel));
        }

        [Command("volume")]
        [Summary("volumeSummary")]
        [Example("70")]
        public async Task Volume([Summary("volumeParam1")] int volume)
        {
            await SendEmbedAsync(await _musicService.SetVolumeAsync(volume, Context.Guild, Context.Channel as ITextChannel));
        }

        //[Command("artwork")]
        //[Summary("artworkSummary")]
        //public async Task Artwork()
        //{
        //
        //    (bool success, string message) = await _musicService.GetArtworkAsync(Context.Guild);
        //    if (!success)
        //    {
        //        await SendEmbedAsync(message);
        //    }
        //    else
        //    {
        //        EmbedBuilder builder = new EmbedBuilder()
        //            .WithTitle("Artwork")
        //            .WithImageUrl(message)
        //            .WithColor(FergunConfig.EmbedColor);

        //        await ReplyAsync(embed: builder.Build());
        //    }
        //}
    }
}