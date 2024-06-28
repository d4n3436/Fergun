using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Genius;
using Fergun.Configuration;
using Fergun.Data;
using Fergun.Extensions;
using Fergun.Hardware;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Modules.Handlers;
using Fergun.Preconditions;
using Fergun.Services;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fergun.Modules;

[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod)]
public class OtherModule : InteractionModuleBase
{
    private static readonly Uri _inspiroBotUri = new("https://inspirobot.me/api?generate=true");
    private readonly ILogger<OtherModule> _logger;
    private readonly IFergunLocalizer<OtherModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly InteractiveService _interactive;
    private readonly IGeniusClient _geniusClient;
    private readonly HttpClient _httpClient;
    private readonly FergunContext _db;
    private readonly ApplicationCommandCache _commandCache;

    public OtherModule(ILogger<OtherModule> logger, IFergunLocalizer<OtherModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions,
        InteractiveService interactive, IGeniusClient geniusClient, HttpClient httpClient, FergunContext db, ApplicationCommandCache commandCache)
    {
        _logger = logger;
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _geniusClient = geniusClient;
        _httpClient = httpClient;
        _interactive = interactive;
        _db = db;
        _commandCache = commandCache;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("command-stats", "Displays the command usage stats.")]
    public async Task<RuntimeResult> CommandStatsAsync()
    {
        await Context.Interaction.DeferAsync();

        _logger.LogInformation("Requesting command stats from database");

        var commandStats = await _db.CommandStats
            .AsNoTracking()
            .OrderByDescending(x => x.UsageCount)
            .ToListAsync();

        _logger.LogDebug("Command stats count: {Count}", commandStats.Count);

        if (commandStats.Count == 0)
        {
            return FergunResult.FromError(_localizer["NoStats"]);
        }

        int maxIndex = (int)Math.Ceiling((double)commandStats.Count / 25) - 1;
        _logger.LogDebug("Sending command stats paginator with {Count} pages", maxIndex + 1);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(maxIndex)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes(_fergunOptions)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, _fergunOptions.PaginatorTimeout, InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int index)
        {
            int start = index * 25;

            return new PageBuilder()
                .WithTitle(_localizer["CommandStats"])
                .WithDescription(string.Join('\n', commandStats.Take(start..(start + 25)).Select((x, i) => $"{start + i + 1}. `{x.Name}`: {x.UsageCount}")))
                .WithFooter(_localizer["PaginatorFooter", index + 1, maxIndex + 1])
                .WithColor(Color.Orange);
        }
    }

    [SlashCommand("inspirobot", "Sends an inspirational quote.")]
    public async Task<RuntimeResult> InspiroBotAsync()
    {
        await Context.Interaction.DeferAsync();

        _logger.LogDebug("Requesting URL from InspiroBot API");

        string url = await _httpClient.GetStringAsync(_inspiroBotUri);

        _logger.LogInformation("Obtained url from InspiroBot API: {Url}", url);

        var builder = new EmbedBuilder()
            .WithTitle("InspiroBot")
            .WithImageUrl(url)
            .WithColor(Color.Orange);

        await Context.Interaction.FollowupAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }

    [SlashCommand("invite", "Invite Fergun to your server.")]
    public async Task<RuntimeResult> InviteAsync()
    {
        var builder = new EmbedBuilder()
            .WithDescription(_localizer["InviteDescription"])
            .WithColor(Color.Orange);

        ulong applicationId = (await Context.Client.GetApplicationInfoAsync()).Id;

        var button = new ComponentBuilder()
            .WithButton(_localizer["InviteFergun"], style: ButtonStyle.Link, url: $"https://discord.com/oauth2/authorize?client_id={applicationId}&scope=bot%20applications.commands");

        await Context.Interaction.RespondAsync(embed: builder.Build(), components: button.Build());

        return FergunResult.FromSuccess();
    }

    [Ratelimit(1, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("lyrics", "Gets the lyrics of a song.")]
    public async Task<RuntimeResult> LyricsAsync([MaxValue(int.MaxValue)] [Autocomplete(typeof(GeniusAutocompleteHandler))] [Summary(name: "name", description: "The name of the song.")] int id)
    {
        await Context.Interaction.DeferAsync();

        return await LyricsInternalAsync(id);
    }

    [Ratelimit(1, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("lyrics-spotify", "Gets the lyrics of the song you're listening to on Spotify.")]
    public async Task<RuntimeResult> LyricsSpotifyAsync()
    {
        var spotifyActivity = Context.User.Activities.OfType<SpotifyGame>().FirstOrDefault();
        if (spotifyActivity is null)
        {
            return FergunResult.FromError(_localizer["NoSpotifyActivity"], true);
        }

        await Context.Interaction.DeferAsync();

        string artist = spotifyActivity.Artists.First();
        string title = RemoveTitleExtraInfo(spotifyActivity.TrackTitle);
        string query = $"{artist} {title}";

        _logger.LogInformation("Detected Spotify activity on user {User} ({UserId})", Context.User, Context.User.Id);
        _logger.LogInformation("Searching for songs matching Spotify song \"{Song}\"", $"{artist} - {title}");

        var results = await _geniusClient.SearchSongsAsync(query);

        var match = results.FirstOrDefault(x =>
            x.PrimaryArtistNames.Equals(artist, StringComparison.InvariantCultureIgnoreCase) &&
            x.Title.Equals(title, StringComparison.InvariantCultureIgnoreCase));

        if (match is not null)
        {
            _logger.LogInformation("Found exact match for Spotify song \"{Song}\"", match);
            if (match.IsInstrumental)
            {
                return FergunResult.FromError(_localizer["LyricsInstrumental", match]);
            }

            if (match.LyricsState == "unreleased")
            {
                return FergunResult.FromError(_localizer["LyricsUnreleased", match]);
            }
        }
        else
        {
            match = results.FirstOrDefault(x => !x.IsInstrumental && x.LyricsState != "unreleased" &&
                (x.PrimaryArtistNames.Equals(artist, StringComparison.InvariantCultureIgnoreCase) ||
                x.Title.Equals(title, StringComparison.InvariantCultureIgnoreCase)));
        }

        if (match is null)
        {
            return FergunResult.FromError(_localizer["NoSongMatchFound", $"{artist} - {title}"]);
        }

        return await LyricsInternalAsync(match.Id, false);
    }

    private async Task<RuntimeResult> LyricsInternalAsync(int id, bool checkSpotifyStatus = true)
    {
        _logger.LogInformation("Requesting song from Genius with ID {Id}", id);
        var song = await _geniusClient.GetSongAsync(id);

        if (song is null)
        {
            _logger.LogDebug("Song with ID {Id} was not found", id);
            return FergunResult.FromError(_localizer["LyricsNotFound", id]);
        }

        _logger.LogInformation("Retrieved song (title: \"{Title}\", is instrumental: {IsInstrumental}, lyrics state: {LyricsState})", song, song.IsInstrumental, song.LyricsState);

        if (song.IsInstrumental)
        {
            // This shouldn't be reachable unless someone manually passes an instrumental ID.
            return FergunResult.FromError(_localizer["LyricsInstrumental", $"{song.ArtistNames} - {song.Title}"]);
        }

        if (song.LyricsState == "unreleased")
        {
            // This shouldn't be reachable unless someone manually passes an unreleased ID.
            return FergunResult.FromError(_localizer["LyricsUnreleased", $"{song.ArtistNames} - {song.Title}"]);
        }

        if (string.IsNullOrEmpty(song.Lyrics))
        {
            return FergunResult.FromError(_localizer["LyricsEmpty", $"{song.ArtistNames} - {song.Title}"]);
        }

        var spotifyLyricsCommand = _commandCache.CachedCommands.FirstOrDefault(x => x.Name == "lyrics-spotify");
        Debug.Assert(spotifyLyricsCommand != null, "Expected /lyrics-spotify to be present");

        var chunks = song.Lyrics.SplitForPagination(EmbedBuilder.MaxDescriptionLength).ToArray();
        _logger.LogDebug("Split lyrics into {Chunks}", "chunk".ToQuantity(chunks.Length));

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(chunks.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes(_fergunOptions)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(20), InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int index)
        {
            string links = $"{Format.Url(_localizer["ViewOnGenius"], song.Url)} | {Format.Url(_localizer["ViewArtist"], song.PrimaryArtistUrl)}";

            // Discord doesn't support custom protocols in embeds (spotify://track/id)
            if (song.SpotifyTrackId is not null)
                links += $" | {Format.Url(_localizer["OpenInSpotify"], $"https://open.spotify.com/track/{song.SpotifyTrackId}?go=1")}";

            var builder = new PageBuilder()
                .WithTitle($"{song.ArtistNames} - {song.Title}".Truncate(EmbedBuilder.MaxTitleLength))
                .WithThumbnailUrl(song.SongArtImageUrl)
                .WithDescription(chunks[index].ToString())
                .WithFooter(_localizer["GeniusPaginatorFooter", index + 1, chunks.Length], Constants.GeniusLogoUrl)
                .WithColor(Color.Orange);

            if (checkSpotifyStatus && IsSameSong())
            {
                var mention = $"</{spotifyLyricsCommand.Name}:{spotifyLyricsCommand.Id}>";
                builder.AddField(_localizer["Note"], _localizer["UseSpotifyLyricsCommand", mention]);
            }

            builder.AddField(_localizer["Links"], links);

            return builder;
        }

        bool IsSameSong()
        {
            var spotifyActivity = Context.User.Activities.OfType<SpotifyGame>().FirstOrDefault();
            if (spotifyActivity is null)
                return false;

            string artist = spotifyActivity.Artists.First();
            string title = RemoveTitleExtraInfo(spotifyActivity.TrackTitle);

            return (song.SpotifyTrackId is not null && song.SpotifyTrackId == spotifyActivity.TrackId) ||
                (song.PrimaryArtistNames.Equals(artist, StringComparison.InvariantCultureIgnoreCase) &&
                song.Title.Equals(title, StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [SlashCommand("stats", "Sends the stats of the bot.")]
    public async Task<RuntimeResult> StatsAsync()
    {
        await Context.Interaction.DeferAsync();

        var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;

        _logger.LogInformation("Requesting hardware info ({Instance})", HardwareInfo.Instance);
        double cpuUsage = await HardwareInfo.GetCpuUsageAsync();
        string cpu = HardwareInfo.GetCpuName() ?? "?";
        string os = HardwareInfo.GetOperatingSystemName();

        _logger.LogDebug("CPU: {Cpu}", cpu);
        _logger.LogDebug("CPU Usage: {Usage}", cpuUsage.ToString("P0"));
        _logger.LogDebug("Operating System: {OS}", os);

        var memoryStatus = HardwareInfo.GetMemoryStatus();
        long totalRamUsage = memoryStatus.UsedPhysicalMemory;
        long processRamUsage = memoryStatus.ProcessUsedMemory;
        long totalRam = memoryStatus.TotalPhysicalMemory;

        _logger.LogDebug("Total RAM Usage: {TotalRamUsage}", memoryStatus.UsedPhysicalMemory.Bytes());
        _logger.LogDebug("Process RAM Usage: {ProcessRamUsage}", memoryStatus.ProcessUsedMemory.Bytes());
        _logger.LogDebug("Total RAM: {TotalRam}", memoryStatus.TotalPhysicalMemory.Bytes());

        IReadOnlyCollection<IGuild> guilds;
        int shards = 1;
        int shardId = 0;
        int? totalUsersInShard = null;
        DiscordSocketClient? shard = null;

        if (Context is ShardedInteractionContext shardedContext)
        {
            guilds = shardedContext.Client.Guilds;
            shards = shardedContext.Client.Shards.Count;
            shardId = Context.Channel.IsPrivate() ? 0 : shardedContext.Client.GetShardIdFor(Context.Guild);
            shard = shardedContext.Client.GetShard(shardId);
            totalUsersInShard = shard.Guilds.Sum(x => x.MemberCount);
        }
        else
        {
            // Context.Client returns the current socket client instead of the shared client
            guilds = await Context.Client.GetGuildsAsync(CacheMode.CacheOnly);
        }

        int? totalUsers = guilds.Sum(x => x.ApproximateMemberCount ?? (x as SocketGuild)?.MemberCount);
        _logger.LogDebug("Total users: {TotalUsers}", totalUsers?.ToString() ?? "?");

        string? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        _logger.LogDebug("Version: {Version}", version ?? "?");

        string[]? split = version?.Split('+');
        string versionAndCommitLink = split?.Length switch
        {
            1 => split[0],
            2 => $"{split[0]}+{Format.Url(split[1][..7], $"{Constants.GitHubRepositoryUrl}/commit/{split[1]}")}",
            _ => "?"
        };

        var elapsed = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime;
        string ramUsage = processRamUsage.Bytes().ToString(_localizer.CurrentCulture);
        if (totalRam > 0)
        {
            if (totalRamUsage > 0)
            {
                string usagePercentage = ((double)processRamUsage / totalRam).ToString("P2", _localizer.CurrentCulture);
                string totalUsagePercentage = ((double)totalRamUsage / totalRam).ToString("P2", _localizer.CurrentCulture);
                ramUsage += $" ({usagePercentage}) / {totalRamUsage.Bytes().ToString(_localizer.CurrentCulture)} ({totalUsagePercentage})";
            }

            ramUsage += $" / {totalRam.Bytes()}";
        }

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["FergunStats"])
            .AddField(_localizer["OperatingSystem"], os, true)
            .AddField("\u200b", "\u200b", true)
            .AddField("CPU", cpu, true)
            .AddField(_localizer["CPUUsage"], cpuUsage.ToString("P0", _localizer.CurrentCulture), true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["RAMUsage"], ramUsage, true)
            .AddField(_localizer["Library"], $"Discord.Net v{DiscordConfig.Version}", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["BotVersion"], versionAndCommitLink, true)
            .AddField(_localizer["TotalServers"], $"{guilds.Count} (Shard: {shard?.Guilds?.Count ?? guilds.Count})", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["TotalUsers"], $"{totalUsers?.ToString() ?? "?"} (Shard: {(totalUsersInShard ?? totalUsers)?.ToString() ?? "?"})", true)
            .AddField(_localizer["ShardID"], shardId, true)
            .AddField("\u200b", "\u200b", true)
            .AddField("Shards", shards, true)
            .AddField(_localizer["Uptime"], elapsed.Humanize(3, _localizer.CurrentCulture, TimeUnit.Day, TimeUnit.Second), true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["BotOwner"], owner, true);

        builder.WithColor(Color.Orange);

        await Context.Interaction.FollowupAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }

    [return: NotNullIfNotNull(nameof(input))]
    private static string? RemoveTitleExtraInfo(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        int index = input.IndexOf(" - ", StringComparison.Ordinal);
        return index != -1 ? input[..index] : input;
    }
}