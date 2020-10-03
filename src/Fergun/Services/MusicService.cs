using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using Discord;
using Discord.WebSocket;
using Fergun.APIs.Genius;
using Fergun.Extensions;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Fergun.Services
{
    public class MusicService
    {
        public LavaNode LavaNode { get; private set; }

        private readonly DiscordSocketClient _client;
        private readonly LogService _logService;
        private readonly LavaConfig _lavaConfig;
        private readonly GeniusApi _geniusApi;

        private static readonly ConcurrentDictionary<ulong, uint> _loopDict = new ConcurrentDictionary<ulong, uint>();

        public MusicService(DiscordSocketClient client, LogService logService)
        {
            _client = client;
            _logService = logService;

            _lavaConfig = new LavaConfig
            {
                LogSeverity = LogSeverity.Info
            };
            LavaNode = new LavaNode(_client, _lavaConfig);
            _geniusApi = new GeniusApi(FergunConfig.GeniusApiToken);
        }

        public async Task InitializeAsync()
        {
            _client.Ready += ClientReadyAsync;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdatedAsync;
            LavaNode.OnLog += LogAsync;
            LavaNode.OnTrackEnded += OnTrackEndedAsync;
            LavaNode.OnTrackStuck += OnTrackStuckAsync;
            LavaNode.OnTrackException += OnTrackExceptionAsync;
            LavaNode.OnWebSocketClosed += OnWebSocketClosed;
            await Task.CompletedTask;
        }

        private async Task ClientReadyAsync()
        {
            if (!LavaNode.IsConnected)
            {
                await LavaNode.ConnectAsync();
            }
        }

        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState beforeState, SocketVoiceState afterState)
        {
            if (user is SocketGuildUser guildUser && LavaNode.TryGetPlayer(guildUser.Guild, out var player))
            {
                // Someone has left a voice channel
                if (player.VoiceChannel != null && afterState.VoiceChannel == null)
                {
                    // Fergun has left a voice channel that has a player
                    if (user.Id == _client.CurrentUser.Id)
                    {
                        if (_loopDict.ContainsKey(player.VoiceChannel.GuildId))
                        {
                            _loopDict.TryRemove(player.VoiceChannel.GuildId, out _);
                        }
                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Left the voice channel \"{player.VoiceChannel.Name}\" in guild \"{player.VoiceChannel.Guild.Name}\" because I got kicked out."));

                        await LavaNode.LeaveAsync(player.VoiceChannel);
                    }
                    // There are no users (only bots) in the voice channel
                    else if ((player.VoiceChannel as SocketVoiceChannel).Users.All(x => x.IsBot))
                    {
                        if (_loopDict.ContainsKey(player.VoiceChannel.GuildId))
                        {
                            _loopDict.TryRemove(player.VoiceChannel.GuildId, out _);
                        }

                        var builder = new EmbedBuilder()
                            .WithDescription("\u26A0 " + Localizer.Locate("LeftVCInactivity", player.TextChannel))
                            .WithColor(FergunConfig.EmbedColor);

                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Left the voice channel \"{player.VoiceChannel.Name}\" in guild \"{player.VoiceChannel.Guild.Name}\" because there are no users."));
                        await player.TextChannel.SendMessageAsync(embed: builder.Build());

                        await LavaNode.LeaveAsync(player.VoiceChannel);
                    }
                }
            }
        }

        private async Task OnWebSocketClosed(WebSocketClosedEventArgs args)
        {
            if (_loopDict.ContainsKey(args.GuildId))
            {
                _loopDict.TryRemove(args.GuildId, out _);
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Websocket closed the connection on guild ID {args.GuildId} with code {args.Code} and reason: {args.Reason}"));
        }

        private async Task LogAsync(LogMessage logMessage)
        {
            await _logService.LogAsync(logMessage);
        }

        private async Task OnTrackEndedAsync(TrackEndedEventArgs args)
        {
            if (!VictoriaExtensions.ShouldPlayNext(args.Reason))
                return;

            var builder = new EmbedBuilder();
            ulong guildId = args.Player.TextChannel.GuildId;
            if (_loopDict.ContainsKey(guildId))
            {
                if (_loopDict[guildId] == 0)
                {
                    _loopDict.TryRemove(guildId, out _);
                    var builder2 = new EmbedBuilder()
                        .WithDescription(string.Format(Localizer.Locate("LoopEnded", args.Player.TextChannel), args.Track.ToTrackLink(false)))
                        .WithColor(FergunConfig.EmbedColor);
                    await args.Player.TextChannel.SendMessageAsync(null, false, builder2.Build());
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Loop for track {args.Track.Title} ({args.Track.Url}) ended in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
                }
                else
                {
                    await args.Player.PlayAsync(args.Track);
                    _loopDict[guildId]--;
                    return;
                }
            }
            if (!args.Player.Queue.TryDequeue(out var item) || !(item is LavaTrack nextTrack))
            {
                builder.WithDescription(Localizer.Locate("NoTracks", args.Player.TextChannel))
                    .WithColor(FergunConfig.EmbedColor);

                await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Queue now empty in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
                return;
            }

            await args.Player.PlayAsync(nextTrack);
            builder.WithTitle(Localizer.Locate("NowPlaying", args.Player.TextChannel))
                .WithDescription(nextTrack.ToTrackLink())
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Now playing: {nextTrack.Title} ({nextTrack.Url}) in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
        }

        private async Task OnTrackExceptionAsync(TrackExceptionEventArgs args)
        {
            var builder = new EmbedBuilder()
                .WithDescription($"\u26a0 {Localizer.Locate("PlayerError", args.Player.TextChannel)}:```{args.ErrorMessage}```")
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            // The current track is auto-skipped
        }

        private async Task OnTrackStuckAsync(TrackStuckEventArgs args)
        {
            var builder = new EmbedBuilder()
                .WithDescription($"\u26a0 {string.Format(Localizer.Locate("PlayerStuck", args.Player.TextChannel), args.Player.Track.Title, args.Threshold.TotalSeconds)}")
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            // The current track is auto-skipped
        }

        public async Task<string> JoinAsync(IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            if (LavaNode.HasPlayer(guild))
                return Localizer.Locate("AlreadyConnected", textChannel);
            await LavaNode.JoinAsync(voiceChannel, textChannel);
            return string.Format(Localizer.Locate("NowConnected", textChannel), Format.Bold(voiceChannel.Name));
        }

        public async Task<bool> LeaveAsync(IGuild guild, SocketVoiceChannel voiceChannel)
        {
            bool hasPlayer = LavaNode.HasPlayer(guild);
            if (hasPlayer)
            {
                if (_loopDict.ContainsKey(guild.Id))
                {
                    _loopDict.TryRemove(guild.Id, out _);
                }
                await LavaNode.LeaveAsync(voiceChannel);
            }
            return hasPlayer;
        }

        public async Task<string> MoveAsync(IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            var oldChannel = player.VoiceChannel;
            if (voiceChannel.Id == oldChannel.Id)
                return Localizer.Locate("MoveSameChannel", textChannel);
            await LavaNode.MoveChannelAsync(voiceChannel);
            return string.Format(Localizer.Locate("PlayerMoved", textChannel), oldChannel, voiceChannel);
        }

        public async Task<(string, IReadOnlyList<LavaTrack>)> PlayAsync(string query, IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            var search = await LavaNode.SearchYouTubeAsync(query);

            if (search.LoadStatus == LoadStatus.NoMatches || search.LoadStatus == LoadStatus.LoadFailed)
            {
                search = await LavaNode.SearchAsync(query);
                if (search.LoadStatus == LoadStatus.NoMatches || search.LoadStatus == LoadStatus.LoadFailed)
                {
                    return (Localizer.Locate("PlayerNoMatches", textChannel), null);
                }
            }

            LavaPlayer player;

            if (search.Playlist.Name != null)
            {
                if (!LavaNode.TryGetPlayer(guild, out player))
                {
                    await LavaNode.JoinAsync(voiceChannel, textChannel);
                    player = LavaNode.GetPlayer(guild);
                }

                TimeSpan time = TimeSpan.Zero;
                if (player.PlayerState == PlayerState.Playing)
                {
                    int trackCount = Math.Min(10, search.Tracks.Count);
                    foreach (LavaTrack track in search.Tracks.Take(10))
                    {
                        player.Queue.Enqueue(track);
                        time += track.Duration;
                    }
                    return (string.Format(Localizer.Locate("PlayerPlaylistAdded", textChannel), search.Playlist.Name, trackCount, time.ToShortForm()), null);
                }
                else
                {
                    int trackCount = Math.Min(9, search.Tracks.Count);
                    foreach (LavaTrack track in search.Tracks.Take(10).Skip(1))
                    {
                        player.Queue.Enqueue(track);
                        time += track.Duration;
                    }
                    // if player wasnt playing anything
                    await player.PlayAsync(search.Tracks[0]);
                    return (string.Format(Localizer.Locate("PlayerEmptyPlaylistAdded", textChannel), trackCount, time.ToShortForm(), search.Tracks[0].ToTrackLink()), null);
                }
            }
            else
            {
                LavaTrack track;
                if (search.Tracks.Count == 0)
                {
                    return (Localizer.Locate("PlayerNoMatches", textChannel), null);
                }
                if (search.Tracks.Count == 1)
                {
                    track = search.Tracks[0];
                }
                else
                {
                    return (null, search.Tracks);
                }

                if (!LavaNode.TryGetPlayer(guild, out player))
                {
                    await LavaNode.JoinAsync(voiceChannel, textChannel);
                    player = LavaNode.GetPlayer(guild);
                }
                if (player.PlayerState == PlayerState.Playing)
                {
                    player.Queue.Enqueue(track);
                    return (string.Format(Localizer.Locate("PlayerTrackAdded", textChannel), track.ToTrackLink()), null);
                }
                else
                {
                    await player.PlayAsync(track);
                    return (string.Format(Localizer.Locate("PlayerNowPlaying", textChannel), track.ToTrackLink()), null);
                }
            }
        }

        public async Task<string> PlayTrack(IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel, LavaTrack track)
        {
            if (!LavaNode.TryGetPlayer(guild, out var player))
            {
                await LavaNode.JoinAsync(voiceChannel, textChannel);
                player = LavaNode.GetPlayer(guild);
            }
            if (track == null)
                return Localizer.Locate("InvalidTrack", textChannel);
            if (player.PlayerState == PlayerState.Playing)
            {
                player.Queue.Enqueue(track);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Added {track.Title} ({track.Url}) to the queue in {textChannel.Guild.Name}/{textChannel.Name}"));
                return string.Format(Localizer.Locate("PlayerTrackAdded", textChannel), track.ToTrackLink());
            }
            else
            {
                await player.PlayAsync(track);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Now playing: {track.Title} ({track.Url}) in {textChannel.Guild.Name}/{textChannel.Name}"));
                return string.Format(Localizer.Locate("PlayerNowPlaying", textChannel), track.ToTrackLink());
            }
        }

        public async Task<string> ReplayAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (player == null)
                return Localizer.Locate("EmptyQueue", textChannel);
            else if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            await player.SeekAsync(TimeSpan.Zero);
            return string.Format(Localizer.Locate("Replaying", textChannel), player.Track.ToTrackLink());
        }

        public async Task<string> SeekAsync(IGuild guild, ITextChannel textChannel, string time)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            if (player?.Track?.Duration == null || !player.Track.CanSeek)
                return Localizer.Locate("CannotSeek", textChannel);

            if (uint.TryParse(time, out uint second))
            {
                if (second >= player.Track.Duration.TotalSeconds)
                {
                    return string.Format(Localizer.Locate("SeekHigherOrEqual", textChannel), second, player.Track.Duration.TotalSeconds);
                }
                await player.SeekAsync(TimeSpan.FromSeconds(second));

                return string.Format(Localizer.Locate("SeekComplete", textChannel), second, TimeSpan.FromSeconds(second).ToShortForm(), player.Track.Duration.ToShortForm());
            }
            // Assume its a string with the formats below
            string[] timeformats = { @"m\:ss", @"mm\:ss", @"h\:mm\:ss", @"hh\:mm\:ss" };
            if (!TimeSpan.TryParseExact(time, timeformats, CultureInfo.InvariantCulture, out TimeSpan span))
            {
                return Localizer.Locate("SeekInvalidFormat", textChannel);
            }
            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero;
            }
            if (span >= player.Track.Duration)
            {
                return string.Format(Localizer.Locate("SeekTimeHigherOrEqual", textChannel), span.ToShortForm(), player.Track.Duration.ToShortForm());
            }
            await player.SeekAsync(span);

            return string.Format(Localizer.Locate("SeekTimeComplete", textChannel), span.ToShortForm(), player.Track.Duration.ToShortForm());
        }

        public async Task<string> StopAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            if (player == null)
                return Localizer.Locate("PlayerError", textChannel);
            await player.StopAsync();
            if (_loopDict.ContainsKey(guild.Id))
            {
                _loopDict.TryRemove(guild.Id, out _);
            }
            return Localizer.Locate("PlayerStopped", textChannel);
        }

        public async Task<string> SkipAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            if (player == null)
                return Localizer.Locate("PlayerError", textChannel);
            if (player.Queue.Count == 0)
                return Localizer.Locate("EmptyQueue", textChannel);

            var oldTrack = player.Track;
            await player.SkipAsync();
            return string.Format(Localizer.Locate("PlayerTrackSkipped", textChannel), oldTrack.ToTrackLink(false), player.Track.ToTrackLink());
        }

        public async Task<string> SetVolumeAsync(int volume, IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            volume = Math.Min(volume, 150);
            if (volume <= 2)
            {
                return Localizer.Locate("VolumeOutOfIndex", textChannel);
            }

            await player.UpdateVolumeAsync((ushort)volume);
            return string.Format(Localizer.Locate("VolumeSet", textChannel), volume);
        }

        public async Task<string> PauseOrResumeAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            if (player.PlayerState == PlayerState.Playing)
            {
                await player.PauseAsync();
                return Localizer.Locate("PlayerPaused", textChannel);
            }
            else
            {
                await player.ResumeAsync();
                return Localizer.Locate("PlaybackResumed", textChannel);
            }
        }

        public async Task<string> ResumeAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            if (player.PlayerState == PlayerState.Paused)
            {
                await player.ResumeAsync();
                return Localizer.Locate("PlaybackResumed", textChannel);
            }

            return Localizer.Locate("PlayerNotPaused", textChannel);
        }

        public string GetCurrentTrack(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            return string.Format(Localizer.Locate("CurrentlyPlaying", textChannel), player.Track.ToTrackLink(false), player.Track.Position.ToShortForm(), player.Track.Duration.ToShortForm());
        }

        public string GetQueue(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            string queue = string.Format(Localizer.Locate("CurrentlyPlaying", textChannel), player.Track.ToTrackLink(false), player.Track.Position.ToShortForm(), player.Track.Duration.ToShortForm()) + "\n\n";
            if (player.Queue.Count == 0)
            {
                return queue + Localizer.Locate("EmptyQueue", textChannel);
            }

            queue += $"{Localizer.Locate("MusicInQueue", textChannel)}\n";
            //return "Music in queue:\n" + string.Join("\n", player.Queue.Select(x => (x as LavaTrack).Title));
            int tracksToShow = Math.Min(10, player.Queue.Count);
            int excess = player.Queue.Count - 10;

            for (int i = 0; i < tracksToShow; i++)
            {
                var current = player.Queue.ElementAt(i);
                queue += $"{i + 1}. {current.ToTrackLink()}\n";
            }
            if (excess > 0)
            {
                queue += "\n" + string.Format(Localizer.Locate("QueueExcess", textChannel), excess);
            }
            return queue;
        }

        public string Shuffle(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            if (player.Queue.Count == 0)
            {
                return Localizer.Locate("EmptyQueue", textChannel);
            }
            else if (player.Queue.Count == 1)
            {
                return Localizer.Locate("Queue1Item", textChannel);
            }

            player.Queue.Shuffle();

            return Localizer.Locate("QueueShuffled", textChannel);
        }

        public string RemoveAt(IGuild guild, ITextChannel textChannel, int index)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            if (player.Queue.Count == 0)
            {
                return Localizer.Locate("EmptyQueue", textChannel);
            }
            if (index < 1 || index > player.Queue.Count)
            {
                return Localizer.Locate("IndexOutOfRange", textChannel);
            }
            var track = player.Queue.ElementAt(index - 1);

            player.Queue.RemoveAt(index - 1);
            return string.Format(Localizer.Locate("TrackRemoved", textChannel), track.ToTrackLink(false), index);
        }

        public async Task<(string, IEnumerable<string>)> GetLyricsAsync(string query, bool keepHeaders, IGuild guild, ITextChannel textChannel, SpotifyGame spotify)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (string.IsNullOrWhiteSpace(query))
            {
                if (hasPlayer && player.PlayerState == PlayerState.Playing)
                {
                    if (player.Track.Title.Contains(player.Track.Author, StringComparison.OrdinalIgnoreCase))
                    {
                        query = player.Track.Title;
                    }
                    else
                    {
                        query = $"{player.Track.Author} - {player.Track.Title}";
                    }
                }
                else
                {
                    if (spotify == null)
                    {
                        return (Localizer.Locate("LyricsQueryNotPassed", textChannel), null);
                    }
                    query = $"{string.Join(", ", spotify.Artists)} - {spotify.TrackTitle}";
                }
            }
            query = query.Trim();
            GeniusResponse genius;
            try
            {
                genius = await _geniusApi.SearchAsync(query);
            }
            catch (HttpRequestException)
            {
                return (Localizer.Locate("AnErrorOccurred", textChannel), null);
            }
            if (genius.Meta.Status != 200)
            {
                return (Localizer.Locate("AnErrorOccurred", textChannel), null);
            }
            if (genius.Response.Hits.Count == 0)
            {
                return (string.Format(Localizer.Locate("LyricsNotFound", textChannel), Format.Code(query.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase))), null);
            }
            string title = genius.Response.Hits[0].Result.FullTitle;
            Uri uri = genius.Response.Hits[0].Result.Url;

            string lyrics = await ParseGeniusLyricsAsync(uri, keepHeaders);
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                return (string.Format(Localizer.Locate("ErrorParsingLyrics", textChannel), Format.Code(query.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase))), null);
            }
            var split = lyrics.SplitBySeparatorWithLimit('\n', EmbedFieldBuilder.MaxFieldValueLength);

            string links = Format.Url("Genius", uri.AbsoluteUri);
            links += $" - {Format.Url(Localizer.Locate("ArtistPage", textChannel), genius.Response.Hits[0].Result.PrimaryArtist.Url.AbsoluteUri)}";
            split = split.Prepend(links);
            split = split.Prepend(title);
            return (null, split);
        }

        public async Task<(bool, string)> GetArtworkAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return (false, Localizer.Locate("PlayerNotPlaying", textChannel));

            var artworkLink = await player.Track.FetchArtworkAsync();
            if (string.IsNullOrEmpty(artworkLink))
            {
                return (false, Localizer.Locate("AnErrorOccurred", textChannel));
            }
            else
                return (true, artworkLink);
        }

        public string Loop(uint? count, IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            if (!count.HasValue)
            {
                if (_loopDict.ContainsKey(guild.Id))
                {
                    _loopDict.TryRemove(guild.Id, out _);
                    return Localizer.Locate("LoopDisabled", textChannel);
                }
                return string.Format(Localizer.Locate("LoopNoValuePassed", textChannel), Localizer.GetPrefix(textChannel));
            }

            uint countValue = count.Value;
            if (countValue < 1)
            {
                return string.Format(Localizer.Locate("NumberOutOfIndex", textChannel), 1, Constants.MaxTrackLoops);
            }
            countValue = Math.Min(Constants.MaxTrackLoops, countValue);

            if (_loopDict.ContainsKey(guild.Id))
            {
                _loopDict[guild.Id] = countValue;
                return string.Format(Localizer.Locate("LoopUpdated", textChannel), countValue);
            }
            _loopDict.TryAdd(guild.Id, countValue);
            return string.Format(Localizer.Locate("NowLooping", textChannel), countValue);
        }

        private static async Task<string> ParseGeniusLyricsAsync(Uri uri, bool keepHeaders)
        {
            var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
            var document = await context.OpenAsync(uri.AbsoluteUri);
            var element = document?.GetElementsByClassName("lyrics")?.FirstOrDefault()
                       ?? document?.GetElementsByClassName("SongPageGrid-sc-1vi6xda-0 DGVcp Lyrics__Root-sc-1ynbvzw-0 kkHBOZ")?.FirstOrDefault();

            if (element == null)
            {
                return null;
            }

            // Remove newlines and tabs.
            string lyrics = Regex.Replace(element.InnerHtml, @"\t|\n|\r", string.Empty);

            lyrics = WebUtility.HtmlDecode(lyrics)
                .Replace("<b>", "**", StringComparison.OrdinalIgnoreCase)
                .Replace("</b>", "**", StringComparison.OrdinalIgnoreCase)
                .Replace("<i>", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("</i>", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase);

            // Remove remaining HTML tags.
            lyrics = Regex.Replace(lyrics, @"(\<.*?\>)", string.Empty);

            if (!keepHeaders)
            {
                lyrics = Regex.Replace(lyrics, @"(\[.*?\])*", string.Empty, RegexOptions.Multiline);
            }
            return Regex.Replace(lyrics, @"\n{3,}", "\n\n").Trim();
        }
    }
}