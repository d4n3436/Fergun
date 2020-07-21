using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Fergun.APIs.Genius;
using Fergun.Extensions;
using HtmlAgilityPack;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Fergun.Services
{
    public class MusicService
    {
        private readonly DiscordSocketClient _client;
        private readonly LogService _logService;
        private readonly LavaConfig _lavaConfig;
        private readonly LavaNode _lavaNode;
        private const uint _maxLoops = 20;
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
            _lavaNode = new LavaNode(_client, _lavaConfig);
            _geniusApi = new GeniusApi(FergunConfig.GeniusApiToken);
        }

        public async Task InitializeAsync()
        {
            _client.Ready += ClientReadyAsync;
            //_client.Connected += ClientConnectedAsync;
            //_client.Disconnected += ClientDisconnectedAsync;
            _lavaNode.OnLog += LogAsync;
            _lavaNode.OnTrackEnded += OnTrackEndedAsync;
            _lavaNode.OnTrackStuck += OnTrackStuckAsync;
            _lavaNode.OnTrackException += OnTrackExceptionAsync;
            _lavaNode.OnWebSocketClosed += OnWebSocketClosed;
            await Task.CompletedTask;
        }

        private async Task ClientReadyAsync()
        {
            if (!_lavaNode.IsConnected)
            {
                await _lavaNode.ConnectAsync();
            }
            //foreach (var player in _lavaNode.Players)
            //{
            //    //if (!_lavaNode.HasPlayer(player.VoiceChannel.Guild))
            //    var user = await player.VoiceChannel.Guild.GetUserAsync(_client.CurrentUser.Id);
            //    if (user != null && user.VoiceChannel != null)
            //    {
            //        //await player.VoiceChannel.ConnectAsync(true);
            //        var vc = player.VoiceChannel;
            //        await _lavaNode.LeaveAsync(player.VoiceChannel);
            //        await _lavaNode.JoinAsync(vc, player.TextChannel);
            //    }
            //    else
            //    {
            //        await player.StopAsync();
            //        await _lavaNode.LeaveAsync(player.VoiceChannel);
            //    }
            //}
        }

        private async Task OnWebSocketClosed(WebSocketClosedEventArgs args)
        {
            if (_loopDict.ContainsKey(args.GuildId))
            {
                _loopDict.TryRemove(args.GuildId, out _);
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Websocket closed the connection on guild ID {args.GuildId} with code {args.Code} and reason: {args.Reason}"));
            //if (_lavaNode.HasPlayer(_client.GetGuild(args.GuildId)))
            //{
            //    var player = _lavaNode.GetPlayer(_client.GetGuild(args.GuildId));
            //    if (player != null)
            //    {
            //        await player.StopAsync();
            //        await _lavaNode.LeaveAsync(player.VoiceChannel);
            //    }
            //}
        }

        //private async Task ClientConnectedAsync()
        //{
        //    _lavaNode = new LavaNode(_client, _lavaConfig);
        //    await _lavaNode.ConnectAsync();
        //}

        //private async Task ClientDisconnectedAsync(Exception e)
        //{
        //    await Task.CompletedTask;
        //    //await _lavaNode.DisconnectAsync();
        //    //await _lavaNode.DisposeAsync();
        //}

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
                        .WithDescription(string.Format(Localizer.Locate("LoopEnded", args.Player.TextChannel), $"[{args.Track.Title}]({args.Track.Url})"))
                        .WithColor(FergunConfig.EmbedColor);
                    await args.Player.TextChannel.SendMessageAsync(null, false, builder2.Build());
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", $"Loop for track {args.Track.Title} ({args.Track.Url}) ended in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", $"Queue now empty in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
                return;
            }

            await args.Player.PlayAsync(nextTrack);
            builder.WithTitle(Localizer.Locate("NowPlaying", args.Player.TextChannel))
                .WithDescription($"[{nextTrack.Title}]({nextTrack.Url}) ({nextTrack.Duration.ToShortForm()})")
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", $"Now playing: {nextTrack.Title} ({nextTrack.Url}) in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
        }

        private async Task OnTrackExceptionAsync(TrackExceptionEventArgs args)
        {
            var builder = new EmbedBuilder()
                .WithDescription($"\u26A0 {Localizer.Locate("PlayerError", args.Player.TextChannel)}:```{args.ErrorMessage}```")
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            // The current track is auto-skipped
        }

        private async Task OnTrackStuckAsync(TrackStuckEventArgs args)
        {
            var builder = new EmbedBuilder()
                .WithDescription($"\u26A0 {string.Format(Localizer.Locate("PlayerStuck", args.Player.TextChannel), args.Player.Track.Title, args.Threshold.TotalSeconds)}")
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            // The current track is auto-skipped
        }

        public async Task<string> JoinAsync(IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            if (_lavaNode.HasPlayer(guild))
                return Localizer.Locate("AlreadyConnected", textChannel);
            await _lavaNode.JoinAsync(voiceChannel, textChannel);
            return string.Format(Localizer.Locate("NowConnected", textChannel), Format.Bold(voiceChannel.Name));
        }

        public async Task<bool> LeaveAsync(IGuild guild, SocketVoiceChannel voiceChannel)
        {
            bool hasPlayer = _lavaNode.HasPlayer(guild);
            if (hasPlayer)
            {
                if (_loopDict.ContainsKey(guild.Id))
                {
                    _loopDict.TryRemove(guild.Id, out _);
                }
                await _lavaNode.LeaveAsync(voiceChannel);
            }
            return hasPlayer;
        }

        public async Task<string> MoveAsync(IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            var oldChannel = player.VoiceChannel;
            if (voiceChannel.Id == oldChannel.Id)
                return Localizer.Locate("MoveSameChannel", textChannel);
            await _lavaNode.MoveChannelAsync(voiceChannel);
            return string.Format(Localizer.Locate("PlayerMoved", textChannel), oldChannel, voiceChannel);
        }

        public async Task<(string, IReadOnlyList<LavaTrack>)> PlayAsync(string query, IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            var search = await _lavaNode.SearchYouTubeAsync(query);

            if (search.LoadStatus == LoadStatus.NoMatches || search.LoadStatus == LoadStatus.LoadFailed)
            {
                search = await _lavaNode.SearchAsync(query);
                if (search.LoadStatus == LoadStatus.NoMatches || search.LoadStatus == LoadStatus.LoadFailed)
                {
                    return (Localizer.Locate("PlayerNoMatches", textChannel), null);
                }
            }

            LavaPlayer player;

            if (search.Playlist.Name != null)
            {
                if (!_lavaNode.TryGetPlayer(guild, out player))
                {
                    await _lavaNode.JoinAsync(voiceChannel, textChannel);
                    player = _lavaNode.GetPlayer(guild);
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
                    return (string.Format(Localizer.Locate("PlayerEmptyPlaylistAdded", textChannel), trackCount, time.ToShortForm(), $"[{search.Tracks[0].Title}]({search.Tracks[0].Url})", search.Tracks[0].Duration.ToShortForm()), null);
                }
            }
            else
            {
                LavaTrack track;
                if (search.Tracks.Count == 1 || Uri.IsWellFormedUriString(query, UriKind.Absolute))
                {
                    track = search.Tracks[0];
                }
                else
                {
                    return (null, search.Tracks);
                }

                if (!_lavaNode.TryGetPlayer(guild, out player))
                {
                    await _lavaNode.JoinAsync(voiceChannel, textChannel);
                    player = _lavaNode.GetPlayer(guild);
                }
                if (player.PlayerState == PlayerState.Playing)
                {
                    player.Queue.Enqueue(track);
                    return (string.Format(Localizer.Locate("PlayerTrackAdded", textChannel), $"[{track.Title}]({track.Url})", track.Duration.ToShortForm()), null);
                }
                else
                {
                    await player.PlayAsync(track);
                    return (string.Format(Localizer.Locate("PlayerNowPlaying", textChannel), $"[{track.Title}]({track.Url})", track.Duration.ToShortForm()), null);
                }
            }
        }

        public async Task<string> PlayTrack(IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel, LavaTrack track)
        {
            if (!_lavaNode.TryGetPlayer(guild, out var player))
            {
                await _lavaNode.JoinAsync(voiceChannel, textChannel);
                player = _lavaNode.GetPlayer(guild);
            }
            if (track == null)
                return Localizer.Locate("InvalidTrack", textChannel);
            if (player.PlayerState == PlayerState.Playing)
            {
                player.Queue.Enqueue(track);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", $"Added {track.Title} ({track.Url}) to the queue in {textChannel.Guild.Name}/{textChannel.Name}"));
                return string.Format(Localizer.Locate("PlayerTrackAdded", textChannel), $"[{track.Title}]({track.Url})", track.Duration.ToShortForm());
            }
            else
            {
                await player.PlayAsync(track);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", $"Now playing: {track.Title} ({track.Url}) in {textChannel.Guild.Name}/{textChannel.Name}"));
                return string.Format(Localizer.Locate("PlayerNowPlaying", textChannel), $"[{track.Title}]({track.Url})", track.Duration.ToShortForm());
            }
        }

        public async Task<string> ReplayAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
            if (player == null)
                return Localizer.Locate("EmptyQueue", textChannel);
            else if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            await player.SeekAsync(TimeSpan.Zero);
            return $"{Localizer.Locate("Replaying", textChannel)} {player.Track.ToTrackLink()}";
        }

        public async Task<string> SeekAsync(IGuild guild, ITextChannel textChannel, string time)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return Localizer.Locate("PlayerNotPlaying", textChannel);
            if (player == null)
                return Localizer.Locate("PlayerError", textChannel);
            if (player.Queue.Count == 0)
                return Localizer.Locate("EmptyQueue", textChannel);

            var oldTrack = player.Track;
            await player.SkipAsync();
            return string.Format(Localizer.Locate("PlayerTrackSkipped", textChannel), $"[{oldTrack.Title}]({oldTrack.Url})", $"[{player.Track.Title}]({player.Track.Url})", player.Track.Duration.ToShortForm());
        }

        public async Task<string> SetVolumeAsync(int vol, IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            if (vol > 150 || vol <= 2)
            {
                return Localizer.Locate("VolumeOutOfIndex", textChannel);
            }

            await player.UpdateVolumeAsync((ushort)vol);
            return $"{Localizer.Locate("VolumeSetted", textChannel)} {vol}";
        }

        public async Task<string> PauseOrResumeAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            return string.Format(Localizer.Locate("CurrentlyPlaying", textChannel), $"[{player.Track.Title}]({player.Track.Url})", player.Track.Position.ToShortForm(), player.Track.Duration.ToShortForm());
        }

        public string GetQueue(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return Localizer.Locate("PlayerNotPlaying", textChannel);

            string queue = string.Format(Localizer.Locate("CurrentlyPlaying", textChannel), $"[{player.Track.Title}]({player.Track.Url})", player.Track.Position.ToShortForm(), player.Track.Duration.ToShortForm()) + "\n\n";
            if (player.Queue.Count == 0)
            {
                return $"{queue}{Localizer.Locate("EmptyQueue", textChannel)}";
            }

            queue += $"{Localizer.Locate("MusicInQueue", textChannel)}\n";
            //return "Music in queue:\n" + string.Join("\n", player.Queue.Select(x => (x as LavaTrack).Title));
            int tracksToShow = Math.Min(10, player.Queue.Count);
            int excess = player.Queue.Count - 10;

            for (int i = 0; i < tracksToShow; i++)
            {
                var current = player.Queue.ElementAt(i) as LavaTrack;
                queue += $"{i + 1}. {Format.Url(current.Title, current.Url)} ({current.Duration.ToShortForm()})\n";
            }
            if (excess > 0)
            {
                queue += "\n" + string.Format(Localizer.Locate("QueueExcess", textChannel), excess);
            }
            return queue;
        }

        public string Shuffle(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
            //await Task.CompletedTask;
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
            var titleToRemove = (player.Queue.ElementAt(index - 1) as LavaTrack).Title;

            player.Queue.RemoveAt(index - 1);
            return string.Format(Localizer.Locate("TrackRemoved", textChannel), titleToRemove, index);
        }

        public async Task<(string, IEnumerable<string>)> GetLyricsAsync(string query, bool keepHeaders, IGuild guild, ITextChannel textChannel, SpotifyGame spotify)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
            if (query == null)
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

            string lyrics = ParseGeniusLyrics(uri, keepHeaders);
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                return (string.Format(Localizer.Locate("ErrorParsingLyrics", textChannel), Format.Code(query.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase))), null);
            }
            var split = lyrics.SplitToLines(EmbedFieldBuilder.MaxFieldValueLength);

            string links = Format.Url("Genius", uri.AbsoluteUri);
            links += $" - {Format.Url(Localizer.Locate("ArtistPage", textChannel), genius.Response.Hits[0].Result.PrimaryArtist.Url.AbsoluteUri)}";
            split = split.Prepend(links);
            split = split.Prepend(title);
            return (null, split);
        }

        public async Task<(bool, string)> GetArtworkAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
            bool hasPlayer = _lavaNode.TryGetPlayer(guild, out var player);
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
                return string.Format(Localizer.Locate("NumberOutOfIndex", textChannel), 1, _maxLoops);
            }
            countValue = Math.Min(_maxLoops, countValue);

            if (_loopDict.ContainsKey(guild.Id))
            {
                _loopDict[guild.Id] = countValue;
                return string.Format(Localizer.Locate("LoopUpdated", textChannel), countValue);
            }
            _loopDict.TryAdd(guild.Id, countValue);
            return string.Format(Localizer.Locate("NowLooping", textChannel), countValue);
        }

        private static string ParseGeniusLyrics(Uri uri, bool keepHeaders)
        {
            HtmlWeb web = new HtmlWeb();
            var html = web.Load(uri);
            //var oldDiv = html.DocumentNode.Descendants(0).Where(node => node.HasClass("lyrics"));
            //var newDiv = html.DocumentNode.Descendants(0).Where(node => node.HasClass("SongPageGrid-sc-1vi6xda-0 DGVcp Lyrics__Root-sc-1ynbvzw-0 jvlKWy"));
            bool newDiv = true;
            var doc = html.DocumentNode.SelectNodes("//div[@class='SongPageGrid-sc-1vi6xda-0 DGVcp Lyrics__Root-sc-1ynbvzw-0 jvlKWy']//text()");
            if (doc == null)
            {
                newDiv = false;
                doc = html.DocumentNode.SelectNodes("//div[@class='lyrics']//text()");
                if (doc == null)
                {
                    return null;
                }
            }
            if (doc.Count > 600)
            {
                return null;
            }
            //Console.WriteLine($"Parse Genius Lyrics:\nUrl: {url}\nIs new div: {newDiv}\nKeep headers: {keepHeaders}");
            string lyrics = "";
            foreach (var part in doc)
            {
                string text = part.InnerText;
                if (part.ParentNode?.OuterHtml == $"<i>{text}</i>")
                {
                    text = Format.Italics(text.Replace("*", string.Empty, StringComparison.OrdinalIgnoreCase));
                }
                text = WebUtility.HtmlDecode(text);
                //Console.WriteLine($"item: \"{text}\"");

                //if (newDiv)
                //    lyrics += "\n";
                lyrics += text;
                if (newDiv)
                {
                    lyrics += " ";
                    if (part.NextSibling?.Name == "br")
                    {
                        lyrics += "\n";
                    }
                }
                //new: convert br to newline
                //old: already has newlines
                //if (!newDiv && string.IsNullOrWhiteSpace(part.InnerText))
                //{
                //    continue;
                //}
                //lyrics += (newDiv ? "\n" : "") + HttpUtility.HtmlDecode(text) + " ";
                //if (newDiv && part.NextSibling != null && part.NextSibling.Name == "br")
                //{
                //    lyrics += "\n";
                //}
                //if (part.NextSibling != null && part.NextSibling.Name == "br")
                //{
                //    text += "\n";
                //}
                //else
                //{

                //}
                //if (part.InnerText.StartsWith('['))
                //{
                //    text += "\n";
                //}
            }
            //text = doc.Select(x => x.InnerText);

            //if (oldDiv.Any())
            //{
            //    lyrics = oldDiv.FirstOrDefault().GetDirectInnerText();
            //}
            //if (newDiv.Any())
            //{
            //    lyrics = newDiv.FirstOrDefault().GetDirectInnerText();
            //    lyrics = lyrics.Replace("<br/>", "\n");
            //    lyrics = Regex.Replace(lyrics, @"(\<.*?\>)", string.Empty);
            //}
            //else
            //{
            //    return null;
            //}
            //lyrics = string.Join("\n", lyrics);
            //lyrics = lyrics.Replace("<br/>", "\n");
            lyrics = Regex.Replace(lyrics, @"(\<.*?\>)", string.Empty);
            if (!keepHeaders)
            {
                lyrics = Regex.Replace(lyrics, @"(\[.*?\])*", string.Empty, RegexOptions.Multiline);
                //lyrics = Regex.Replace(lyrics, @"\n{2}", "\n");
            }
            else
            {
                lyrics = lyrics.Replace(" [", "\n[", StringComparison.OrdinalIgnoreCase).Replace("[", "\n[", StringComparison.OrdinalIgnoreCase);
            }
            lyrics = Regex.Replace(lyrics, @"\n{3,}", "\n\n");

            //Console.WriteLine($"Final lyrics:\n{lyrics.Trim()}");
            return lyrics.Trim().Replace(" **", "* *", StringComparison.OrdinalIgnoreCase);
        }
    }
}