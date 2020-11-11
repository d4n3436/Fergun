using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Fergun.Extensions;
using Fergun.Utils;
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

            _client.Ready += ClientReadyAsync;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdatedAsync;
            LavaNode.OnLog += LogAsync;
            LavaNode.OnTrackEnded += OnTrackEndedAsync;
            LavaNode.OnTrackStuck += OnTrackStuckAsync;
            LavaNode.OnTrackException += OnTrackExceptionAsync;
            LavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
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
                            .WithDescription("\u26A0 " + GuildUtils.Locate("LeftVCInactivity", player.TextChannel))
                            .WithColor(FergunConfig.EmbedColor);

                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Left the voice channel \"{player.VoiceChannel.Name}\" in guild \"{player.VoiceChannel.Guild.Name}\" because there are no users."));
                        await player.TextChannel.SendMessageAsync(embed: builder.Build());

                        await LavaNode.LeaveAsync(player.VoiceChannel);
                    }
                }
            }
        }

        private async Task OnWebSocketClosedAsync(WebSocketClosedEventArgs args)
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
                        .WithDescription(string.Format(GuildUtils.Locate("LoopEnded", args.Player.TextChannel), args.Track.ToTrackLink(false)))
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
                builder.WithDescription(GuildUtils.Locate("NoTracks", args.Player.TextChannel))
                    .WithColor(FergunConfig.EmbedColor);

                await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Queue now empty in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
                return;
            }

            await args.Player.PlayAsync(nextTrack);
            builder.WithTitle(GuildUtils.Locate("NowPlaying", args.Player.TextChannel))
                .WithDescription(nextTrack.ToTrackLink())
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Now playing: {nextTrack.Title} ({nextTrack.Url}) in {args.Player.TextChannel.Guild.Name}/{args.Player.TextChannel.Name}"));
        }

        private async Task OnTrackExceptionAsync(TrackExceptionEventArgs args)
        {
            var builder = new EmbedBuilder()
                .WithDescription($"\u26a0 {GuildUtils.Locate("PlayerError", args.Player.TextChannel)}:```{args.ErrorMessage}```")
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            // The current track is auto-skipped
        }

        private async Task OnTrackStuckAsync(TrackStuckEventArgs args)
        {
            var builder = new EmbedBuilder()
                .WithDescription($"\u26a0 {string.Format(GuildUtils.Locate("PlayerStuck", args.Player.TextChannel), args.Player.Track.Title, args.Threshold.TotalSeconds)}")
                .WithColor(FergunConfig.EmbedColor);
            await args.Player.TextChannel.SendMessageAsync(embed: builder.Build());
            // The current track is auto-skipped
        }

        public async Task<string> JoinAsync(IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            if (LavaNode.HasPlayer(guild))
                return GuildUtils.Locate("AlreadyConnected", textChannel);
            await LavaNode.JoinAsync(voiceChannel, textChannel);
            return string.Format(GuildUtils.Locate("NowConnected", textChannel), Format.Bold(voiceChannel.Name));
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
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);
            var oldChannel = player.VoiceChannel;
            if (voiceChannel.Id == oldChannel.Id)
                return GuildUtils.Locate("MoveSameChannel", textChannel);
            await LavaNode.MoveChannelAsync(voiceChannel);
            return string.Format(GuildUtils.Locate("PlayerMoved", textChannel), oldChannel, voiceChannel);
        }

        public async Task<(string, IReadOnlyList<LavaTrack>)> PlayAsync(string query, IGuild guild, SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            var search = await LavaNode.SearchYouTubeAsync(query);

            if (search.LoadStatus == LoadStatus.NoMatches || search.LoadStatus == LoadStatus.LoadFailed)
            {
                search = await LavaNode.SearchAsync(query);
                if (search.LoadStatus == LoadStatus.NoMatches || search.LoadStatus == LoadStatus.LoadFailed)
                {
                    return (GuildUtils.Locate("PlayerNoMatches", textChannel), null);
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
                    return (string.Format(GuildUtils.Locate("PlayerPlaylistAdded", textChannel), search.Playlist.Name, trackCount, time.ToShortForm()), null);
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
                    return (string.Format(GuildUtils.Locate("PlayerEmptyPlaylistAdded", textChannel), trackCount, time.ToShortForm(), search.Tracks[0].ToTrackLink()), null);
                }
            }
            else
            {
                LavaTrack track;
                if (search.Tracks.Count == 0)
                {
                    return (GuildUtils.Locate("PlayerNoMatches", textChannel), null);
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
                    return (string.Format(GuildUtils.Locate("PlayerTrackAdded", textChannel), track.ToTrackLink()), null);
                }
                else
                {
                    await player.PlayAsync(track);
                    return (string.Format(GuildUtils.Locate("PlayerNowPlaying", textChannel), track.ToTrackLink()), null);
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
                return GuildUtils.Locate("InvalidTrack", textChannel);
            if (player.PlayerState == PlayerState.Playing)
            {
                player.Queue.Enqueue(track);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Added {track.Title} ({track.Url}) to the queue in {textChannel.Guild.Name}/{textChannel.Name}"));
                return string.Format(GuildUtils.Locate("PlayerTrackAdded", textChannel), track.ToTrackLink());
            }
            else
            {
                await player.PlayAsync(track);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Victoria", $"Now playing: {track.Title} ({track.Url}) in {textChannel.Guild.Name}/{textChannel.Name}"));
                return string.Format(GuildUtils.Locate("PlayerNowPlaying", textChannel), track.ToTrackLink());
            }
        }

        public async Task<string> ReplayAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (player == null)
                return GuildUtils.Locate("EmptyQueue", textChannel);
            else if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);
            await player.SeekAsync(TimeSpan.Zero);
            return string.Format(GuildUtils.Locate("Replaying", textChannel), player.Track.ToTrackLink());
        }

        public async Task<string> SeekAsync(IGuild guild, ITextChannel textChannel, string time)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);
            if (player?.Track?.Duration == null || !player.Track.CanSeek)
                return GuildUtils.Locate("CannotSeek", textChannel);

            if (uint.TryParse(time, out uint second))
            {
                if (second >= player.Track.Duration.TotalSeconds)
                {
                    return string.Format(GuildUtils.Locate("SeekHigherOrEqual", textChannel), second, player.Track.Duration.TotalSeconds);
                }
                await player.SeekAsync(TimeSpan.FromSeconds(second));

                return string.Format(GuildUtils.Locate("SeekComplete", textChannel), second, TimeSpan.FromSeconds(second).ToShortForm(), player.Track.Duration.ToShortForm());
            }
            // Assume its a string with the formats below
            string[] timeformats = { @"m\:ss", @"mm\:ss", @"h\:mm\:ss", @"hh\:mm\:ss" };
            if (!TimeSpan.TryParseExact(time, timeformats, CultureInfo.InvariantCulture, out TimeSpan span))
            {
                return GuildUtils.Locate("SeekInvalidFormat", textChannel);
            }
            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero;
            }
            if (span >= player.Track.Duration)
            {
                return string.Format(GuildUtils.Locate("SeekTimeHigherOrEqual", textChannel), span.ToShortForm(), player.Track.Duration.ToShortForm());
            }
            await player.SeekAsync(span);

            return string.Format(GuildUtils.Locate("SeekTimeComplete", textChannel), span.ToShortForm(), player.Track.Duration.ToShortForm());
        }

        public async Task<string> StopAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);
            if (player == null)
                return GuildUtils.Locate("PlayerError", textChannel);
            await player.StopAsync();
            if (_loopDict.ContainsKey(guild.Id))
            {
                _loopDict.TryRemove(guild.Id, out _);
            }
            return GuildUtils.Locate("PlayerStopped", textChannel);
        }

        public async Task<string> SkipAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);
            if (player == null)
                return GuildUtils.Locate("PlayerError", textChannel);
            if (player.Queue.Count == 0)
                return GuildUtils.Locate("EmptyQueue", textChannel);

            var oldTrack = player.Track;
            await player.SkipAsync();
            return string.Format(GuildUtils.Locate("PlayerTrackSkipped", textChannel), oldTrack.ToTrackLink(false), player.Track.ToTrackLink());
        }

        public async Task<string> SetVolumeAsync(int volume, IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);

            volume = Math.Min(volume, 150);
            if (volume <= 2)
            {
                return GuildUtils.Locate("VolumeOutOfIndex", textChannel);
            }

            await player.UpdateVolumeAsync((ushort)volume);
            return string.Format(GuildUtils.Locate("VolumeSet", textChannel), volume);
        }

        public async Task<string> PauseOrResumeAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);

            if (player.PlayerState == PlayerState.Playing)
            {
                await player.PauseAsync();
                return GuildUtils.Locate("PlayerPaused", textChannel);
            }
            else
            {
                await player.ResumeAsync();
                return GuildUtils.Locate("PlaybackResumed", textChannel);
            }
        }

        public async Task<string> ResumeAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);

            if (player.PlayerState == PlayerState.Paused)
            {
                await player.ResumeAsync();
                return GuildUtils.Locate("PlaybackResumed", textChannel);
            }

            return GuildUtils.Locate("PlayerNotPaused", textChannel);
        }

        public string GetCurrentTrack(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);

            return string.Format(GuildUtils.Locate("CurrentlyPlaying", textChannel), player.Track.ToTrackLink(false), player.Track.Position.ToShortForm(), player.Track.Duration.ToShortForm());
        }

        public string GetQueue(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);

            string queue = string.Format(GuildUtils.Locate("CurrentlyPlaying", textChannel), player.Track.ToTrackLink(false), player.Track.Position.ToShortForm(), player.Track.Duration.ToShortForm()) + "\n\n";
            if (player.Queue.Count == 0)
            {
                return queue + GuildUtils.Locate("EmptyQueue", textChannel);
            }

            queue += $"{GuildUtils.Locate("MusicInQueue", textChannel)}\n";
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
                queue += "\n" + string.Format(GuildUtils.Locate("QueueExcess", textChannel), excess);
            }
            return queue;
        }

        public string Shuffle(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);
            if (player.Queue.Count == 0)
            {
                return GuildUtils.Locate("EmptyQueue", textChannel);
            }
            else if (player.Queue.Count == 1)
            {
                return GuildUtils.Locate("Queue1Item", textChannel);
            }

            player.Queue.Shuffle();

            return GuildUtils.Locate("QueueShuffled", textChannel);
        }

        public string RemoveAt(IGuild guild, ITextChannel textChannel, int index)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);
            if (player.Queue.Count == 0)
            {
                return GuildUtils.Locate("EmptyQueue", textChannel);
            }
            if (index < 1 || index > player.Queue.Count)
            {
                return GuildUtils.Locate("IndexOutOfRange", textChannel);
            }
            var track = player.Queue.ElementAt(index - 1);

            player.Queue.RemoveAt(index - 1);
            return string.Format(GuildUtils.Locate("TrackRemoved", textChannel), track.ToTrackLink(false), index);
        }

        public async Task<(bool, string)> GetArtworkAsync(IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return (false, GuildUtils.Locate("PlayerNotPlaying", textChannel));

            var artworkLink = await player.Track.FetchArtworkAsync();
            if (string.IsNullOrEmpty(artworkLink))
            {
                return (false, GuildUtils.Locate("AnErrorOccurred", textChannel));
            }
            else
                return (true, artworkLink);
        }

        public string Loop(uint? count, IGuild guild, ITextChannel textChannel)
        {
            bool hasPlayer = LavaNode.TryGetPlayer(guild, out var player);
            if (!hasPlayer || player.PlayerState != PlayerState.Playing)
                return GuildUtils.Locate("PlayerNotPlaying", textChannel);

            if (!count.HasValue)
            {
                if (_loopDict.ContainsKey(guild.Id))
                {
                    _loopDict.TryRemove(guild.Id, out _);
                    return GuildUtils.Locate("LoopDisabled", textChannel);
                }
                return string.Format(GuildUtils.Locate("LoopNoValuePassed", textChannel), GuildUtils.GetPrefix(textChannel));
            }

            uint countValue = count.Value;
            if (countValue < 1)
            {
                return string.Format(GuildUtils.Locate("NumberOutOfIndex", textChannel), 1, Constants.MaxTrackLoops);
            }
            countValue = Math.Min(Constants.MaxTrackLoops, countValue);

            if (_loopDict.ContainsKey(guild.Id))
            {
                _loopDict[guild.Id] = countValue;
                return string.Format(GuildUtils.Locate("LoopUpdated", textChannel), countValue);
            }
            _loopDict.TryAdd(guild.Id, countValue);
            return string.Format(GuildUtils.Locate("NowLooping", textChannel), countValue);
        }

        public async Task ShutdownAllPlayersAsync()
        {
            var players = LavaNode.Players.Where(x => x != null);

            if (players.Any())
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Logout", $"Shutting down {players.Count()} music players..."));

                foreach (var player in players)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle($"\u26a0 {GuildUtils.Locate("Warning", player.TextChannel)} \u26a0")
                        .WithDescription(GuildUtils.Locate("MusicPlayerShutdownWarning", player.TextChannel))
                        .WithColor(FergunConfig.EmbedColor);

                    await player.TextChannel.SendMessageAsync(embed: embed.Build());
                }

                await Task.Delay(5000);

                foreach (var player in players)
                {
                    try
                    {
                        await LavaNode.LeaveAsync(player.VoiceChannel);
                    }
                    catch (NullReferenceException) { };
                }
            }
        }
    }
}