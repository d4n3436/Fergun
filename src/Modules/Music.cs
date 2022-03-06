using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.APIs.Genius;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;
using Fergun.Services;
using Fergun.Utils;
using Victoria;
using Victoria.Enums;

namespace Fergun.Modules
{
    [Order(3)]
    [RequireBotPermission(Constants.MinimumRequiredPermissions)]
    [Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    [UserMustBeInVoice("lyrics", "spotify")]
    public class Music : FergunBase
    {
        private static GeniusApi _geniusApi;
        private readonly MusicService _musicService;
        private readonly LogService _logService;
        private readonly MessageCacheService _messageCache;
        private readonly LavaNode _lavaNode;

        public Music(MusicService musicService, LogService logService, MessageCacheService messageCache, LavaNode lavaNode)
        {
            _musicService = musicService;
            _logService = logService;
            _messageCache = messageCache;
            _lavaNode = lavaNode;
            _geniusApi ??= new GeniusApi(FergunClient.Config.GeniusApiToken);
        }

        [RequireBotPermission(GuildPermission.Connect, ErrorMessage = "BotRequireConnect")]
        [Command("join", RunMode = RunMode.Async)]
        [Summary("joinSummary")]
        public async Task<RuntimeResult> Join()
        {
            await SendEmbedAsync(await _musicService.JoinAsync(Context.Guild, ((SocketGuildUser)Context.User).VoiceChannel, Context.Channel as ITextChannel), DisplayRewriteWarning);
            return FergunResult.FromSuccess();
        }

        [Command("leave")]
        [Summary("leaveSummary")]
        [Alias("disconnect", "quit", "exit")]
        public async Task Leave()
        {
            var user = (SocketGuildUser)Context.User;
            bool connected = await _musicService.LeaveAsync(Context.Guild, user.VoiceChannel);
            await SendEmbedAsync(!connected ? Locate("BotNotConnected") : string.Format(Locate("LeftVC"), Format.Bold(user.VoiceChannel.Name)), DisplayRewriteWarning);
        }

        [Command("loop")]
        [Summary("loopSummary")]
        [Example("10")]
        public async Task Loop([Summary("loopParam1")] uint? count = null)
        {
            await SendEmbedAsync(_musicService.Loop(count, Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [LongRunning]
        [Command("lyrics", RunMode = RunMode.Async)]
        [Summary("lyricsSummary")]
        [Alias("l")]
        [Example("never gonna give you up")]
        public async Task<RuntimeResult> Lyrics([Remainder, Summary("lyricsParam1")] string query = null)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.GeniusApiToken))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.GeniusApiToken)));
            }

            bool keepHeaders = false;
            if (string.IsNullOrWhiteSpace(query))
            {
                bool hasPlayer = _lavaNode.TryGetPlayer(Context.Guild, out var player);
                if (hasPlayer && player.PlayerState == PlayerState.Playing)
                {
                    query = player.Track.Title.Contains(player.Track.Author, StringComparison.OrdinalIgnoreCase)
                        ? player.Track.Title
                        : $"{player.Track.Author} - {player.Track.Title}";
                }
                else
                {
                    var spotify = Context.User.Activities?.OfType<SpotifyGame>().FirstOrDefault();
                    if (spotify == null)
                    {
                        return FergunResult.FromError(Locate("LyricsQueryNotPassed"));
                    }
                    query = $"{string.Join(", ", spotify.Artists)} - {spotify.TrackTitle}";
                }
            }
            else if (query.EndsWith("-headers", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Substring(0, query.Length - 8);
                keepHeaders = true;
            }

            query = query.Trim();
            GeniusResponse genius;
            try
            {
                genius = await _geniusApi.SearchAsync(query);
            }
            catch (HttpRequestException e)
            {
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException)
            {
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }
            if (genius.Meta.Status != 200)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }
            if (genius.Response.Hits.Count == 0)
            {
                return FergunResult.FromError(string.Format(Locate("LyricsNotFound"), Format.Code(query.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase))));
            }

            var result = genius.Response.Hits[0].Result;
            string lyrics = await CommandUtils.ParseGeniusLyricsAsync(result.Url, keepHeaders);

            if (string.IsNullOrWhiteSpace(lyrics))
            {
                return FergunResult.FromError(string.Format(Locate("ErrorParsingLyrics"), Format.Code(query.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase))));
            }

            var splitLyrics = lyrics.SplitBySeparatorWithLimit('\n', EmbedBuilder.MaxDescriptionLength).ToArray();
            string links = $"{Format.Url("Genius", result.Url)} - {Format.Url(Locate("ArtistPage"), genius.Response.Hits[0].Result.PrimaryArtist.Url)}";
            string paginatorFooter = $"{Locate("LyricsByGenius")} - {Locate("PaginatorFooter")}";

            Task<PageBuilder> GeneratePageAsync(int index)
            {
                var pageBuilder = new PageBuilder()
                    .WithAuthor(Context.User)
                    .WithColor(new Color(FergunClient.Config.EmbedColor))
                    .WithTitle(result.FullTitle)
                    .WithDescription(splitLyrics[index].Truncate(EmbedBuilder.MaxDescriptionLength))
                    .AddField("Links", links)
                    .WithFooter(string.Format(paginatorFooter, index + 1, splitLyrics.Length));

                return Task.FromResult(pageBuilder);
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                .WithMaxPageIndex(splitLyrics.Length - 1)
                .WithPageFactory(GeneratePageAsync)
                .WithFooter(PaginatorFooter.None)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .WithDeletion(DeletionOptions.Valid)
                .Build();

            _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);

            return FergunResult.FromSuccess();
        }

        [Command("move")]
        [Summary("moveSummary")]
        public async Task Move()
        {
            await SendEmbedAsync(await _musicService.MoveAsync(Context.Guild, ((SocketGuildUser)Context.User).VoiceChannel, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("nowplaying")]
        [Summary("nowplayingSummary")]
        [Alias("np")]
        public async Task NowPlaying()
        {
            await SendEmbedAsync(_musicService.GetCurrentTrack(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("pause")]
        [Summary("pauseSummary")]
        public async Task Pause()
        {
            await SendEmbedAsync(await _musicService.PauseOrResumeAsync(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [RequireBotPermission(GuildPermission.Speak, ErrorMessage = "BotRequireSpeak")]
        [LongRunning]
        [Command("play", RunMode = RunMode.Async)]
        [Summary("playSummary")]
        [Alias("p")]
        [Example("darude sandstorm")]
        public async Task<RuntimeResult> Play([Remainder, Summary("playParam1")] string query)
        {
            var user = (SocketGuildUser)Context.User;
            string response;
            IReadOnlyList<LavaTrack> tracks;
            try
            {
                (response, tracks) = await _musicService.PlayAsync(query, Context.Guild, user.VoiceChannel, Context.Channel as ITextChannel);
            }
            catch (NullReferenceException e) // Catch nullref caused by bug in Victoria
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error playing music", e));
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            if (tracks == null)
            {
                await SendEmbedAsync(response, DisplayRewriteWarning);
            }
            else
            {
                IUserMessage message = null;
                LavaTrack selectedTrack;
                bool trackSelection = GetGuildConfig()?.TrackSelection ?? Constants.TrackSelectionDefault;
                if (trackSelection)
                {
                    string list = "";
                    int count = Math.Min(Constants.MaxTracksToDisplay, tracks.Count);

                    for (int i = 0; i < count; i++)
                    {
                        list += $"{i + 1}. {tracks[i].ToTrackLink()}\n";
                    }

                    var builder = new EmbedBuilder()
                        .WithAuthor(user)
                        .WithTitle(Locate("SelectTrack"))
                        .WithDescription(list)
                        .WithColor(FergunClient.Config.EmbedColor);

                    var warningBuilder = new EmbedBuilder()
                        .WithColor(FergunClient.Config.EmbedColor)
                        .WithDescription($"\u26a0 {Locate("ReplyTimeout")}");

                    var selectionBuilder = new SelectionBuilder<int>()
                        .WithOptions(Enumerable.Range(0, count).ToArray())
                        .WithStringConverter(x => (x + 1).ToString())
                        .WithSelectionPage(PageBuilder.FromEmbedBuilder(builder))
                        .WithTimeoutPage(PageBuilder.FromEmbedBuilder(warningBuilder))
                        .WithActionOnTimeout(ActionOnStop.ModifyMessage | ActionOnStop.DisableInput)
                        .AddUser(Context.User);

                    var result = await SendSelectionAsync(selectionBuilder.Build(), TimeSpan.FromMinutes(1));

                    if (!result.IsSuccess)
                    {
                        return FergunResult.FromError(Locate("ReplyTimeout"), true);
                    }

                    selectedTrack = tracks[result.Value];
                    message = result.Message;
                }
                else
                {
                    selectedTrack = tracks[0];
                }
                var result2 = await _musicService.PlayTrack(Context.Guild, user.VoiceChannel, Context.Channel as ITextChannel, selectedTrack);
                var builder2 = new EmbedBuilder()
                    .WithDescription(result2)
                    .WithColor(FergunClient.Config.EmbedColor);

                if (DisplayRewriteWarning)
                {
                    builder2.AddField(Locate("CommandRemovalWarning"), Locate("MusicRemovalWarning"));
                }

                if (message == null)
                {
                    await ReplyAsync(embed: builder2.Build());
                }
                else
                {
                    await message.ModifyOrResendAsync(embed: builder2.Build(), cache: _messageCache);
                }
            }
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("spotify", RunMode = RunMode.Async)]
        [Summary("spotifySummary")]
        [Example("Fergun#6839")]
        public async Task<RuntimeResult> Spotify([Remainder, Summary("spotifyParam1")] IUser user = null)
        {
            if (!FergunClient.Config.PresenceIntent)
            {
                return FergunResult.FromError(Locate("NoPresenceIntent"));
            }

            user ??= Context.User;
            var spotify = user.Activities?.OfType<SpotifyGame>().FirstOrDefault();
            if (spotify == null)
            {
                return FergunResult.FromError(string.Format(Locate("NoSpotifyStatus"), user));
            }

            string lyricsUrl = "?";
            if (!string.IsNullOrEmpty(FergunClient.Config.GeniusApiToken))
            {
                try
                {
                    var genius = await _geniusApi.SearchAsync($"{string.Join(", ", spotify.Artists)} - {spotify.TrackTitle}");
                    if (genius.Meta.Status == 200 && genius.Response.Hits.Count > 0)
                    {
                        lyricsUrl = Format.Url(Locate("ClickHere"), genius.Response.Hits[0].Result.Url);
                    }
                }
                catch (HttpRequestException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Spotify: Error calling Genius API", e));
                }
            }

            var builder = new EmbedBuilder()
                .WithAuthor("Spotify", Constants.SpotifyLogoUrl)
                .WithThumbnailUrl(spotify.AlbumArtUrl)

                .AddField(Locate("Title"), spotify.TrackTitle, true)
                .AddField(Locate("Artist"), string.Join(", ", spotify.Artists), true)
                .AddField("\u200b", "\u200b", true)

                .AddField(Locate("Album"), spotify.AlbumTitle, true)
                .AddField(Locate("Duration"), spotify.Duration?.ToShortForm() ?? "?", true)
                .AddField("\u200b", "\u200b", true)

                .AddField(Locate("Lyrics"), lyricsUrl, true)
                .AddField(Locate("TrackUrl"), Format.Url(Locate("ClickHere"), spotify.TrackUrl), true)
                .WithColor(FergunClient.Config.EmbedColor);

            if (DisplayRewriteWarning)
            {
                builder.AddField(Locate("CommandRemovalWarning"), Locate("MusicRemovalWarning"));
            }

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("queue")]
        [Summary("queueSummary")]
        [Alias("q")]
        public async Task Queue()
        {
            await SendEmbedAsync(_musicService.GetQueue(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("remove")]
        [Summary("removeSummary")]
        [Alias("delete")]
        [Example("2")]
        public async Task Remove([Summary("removeParam1")] int index)
        {
            await SendEmbedAsync(_musicService.RemoveAt(Context.Guild, Context.Channel as ITextChannel, index), DisplayRewriteWarning);
        }

        [Command("replay")]
        [Summary("replaySummary")]
        public async Task Replay()
        {
            await SendEmbedAsync(await _musicService.ReplayAsync(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("resume")]
        [Summary("resumeSummary")]
        public async Task Resume()
        {
            await SendEmbedAsync(await _musicService.ResumeAsync(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("seek")]
        [Summary("seekSummary")]
        [Alias("skipto", "goto")]
        [Example("3:14")]
        public async Task Seek([Summary("seekParam1")] string time)
        {
            await SendEmbedAsync(await _musicService.SeekAsync(Context.Guild, Context.Channel as ITextChannel, time), DisplayRewriteWarning);
        }

        [Command("shuffle")]
        [Summary("shuffleSummary")]
        public async Task Shuffle()
        {
            await SendEmbedAsync(_musicService.Shuffle(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("skip")]
        [Summary("skipSummary")]
        [Alias("s")]
        public async Task Skip()
        {
            await SendEmbedAsync(await _musicService.SkipAsync(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("stop")]
        [Summary("stopSummary")]
        public async Task Stop()
        {
            await SendEmbedAsync(await _musicService.StopAsync(Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }

        [Command("volume")]
        [Summary("volumeSummary")]
        [Example("70")]
        public async Task Volume([Summary("volumeParam1")] int volume)
        {
            await SendEmbedAsync(await _musicService.SetVolumeAsync(volume, Context.Guild, Context.Channel as ITextChannel), DisplayRewriteWarning);
        }
    }
}