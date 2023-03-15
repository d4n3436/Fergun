using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Musixmatch;
using Fergun.Data;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Modules.Handlers;
using Fergun.Utils;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.RateLimit;

namespace Fergun.Modules;

public class OtherModule : InteractionModuleBase
{
    private readonly ILogger<OtherModule> _logger;
    private readonly IFergunLocalizer<OtherModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly InteractiveService _interactive;
    private readonly IMusixmatchClient _musixmatchClient;
    private readonly HttpClient _httpClient;
    private readonly FergunContext _db;
    private static readonly Uri _inspiroBotUri = new("https://inspirobot.me/api?generate=true");

    public OtherModule(ILogger<OtherModule> logger, IFergunLocalizer<OtherModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions,
        InteractiveService interactive, IMusixmatchClient musixmatchClient, HttpClient httpClient, FergunContext db)
    {
        _logger = logger;
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _musixmatchClient = musixmatchClient;
        _httpClient = httpClient;
        _interactive = interactive;
        _db = db;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("command-stats", "Displays the command usage stats.")]
    public async Task<RuntimeResult> CommandStatsAsync()
    {
        await Context.Interaction.DeferAsync();

        var commandStats = await _db.CommandStats
            .AsNoTracking()
            .OrderByDescending(x => x.UsageCount)
            .ToListAsync();

        if (commandStats.Count == 0)
        {
            return FergunResult.FromError(_localizer["No stats to display."]);
        }

        int maxIndex = (int)Math.Ceiling((double)commandStats.Count / 25) - 1;

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
                .WithTitle(_localizer["Command Stats"])
                .WithDescription(string.Join('\n', commandStats.Take(start..(start + 25)).Select((x, i) => $"{start + i + 1}. `{x.Name}`: {x.UsageCount}")))
                .WithFooter(_localizer["Page {0} of {1}", index + 1, maxIndex + 1])
                .WithColor(Color.Orange);
        }
    }

    [SlashCommand("inspirobot", "Sends an inspirational quote.")]
    public async Task<RuntimeResult> InspiroBotAsync()
    {
        await Context.Interaction.DeferAsync();

        string url = await _httpClient.GetStringAsync(_inspiroBotUri);

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
            .WithDescription(_localizer["Click the button below to invite Fergun to your server."])
            .WithColor(Color.Orange);

        ulong applicationId = (await Context.Client.GetApplicationInfoAsync()).Id;

        var button = new ComponentBuilder()
            .WithButton(_localizer["Invite Fergun"], style: ButtonStyle.Link, url: $"https://discord.com/oauth2/authorize?client_id={applicationId}&scope=bot%20applications.commands");

        await Context.Interaction.RespondAsync(embed: builder.Build(), components: button.Build());

        return FergunResult.FromSuccess();
    }
    
    [SlashCommand("lyrics", "Gets the lyrics of a song.")]
    public async Task<RuntimeResult> LyricsAsync([Autocomplete(typeof(MusixmatchAutocompleteHandler))] [Summary(name: "name", description: "The name of the song.")] int id)
    {
        await Context.Interaction.DeferAsync();

        IMusixmatchSong? song;
        try
        {
            song = await _musixmatchClient.GetSongAsync(id);
        }
        catch (RateLimitRejectedException e)
        {
            return FergunResult.FromError(_localizer["Unable to get the requested lyrics right now. Try again in {0}.",
                e.RetryAfter.Humanize(culture: _localizer.CurrentCulture)]);
        }
        
        if (song is null)
        {
            return FergunResult.FromError(_localizer["Unable to find a song with ID {0}. Use the autocomplete results.", id]);
        }
        
        if (song.IsInstrumental || !song.HasLyrics)
        {
            // This shouldn't be reachable unless someone manually passes an instrumental ID.
            return FergunResult.FromError(_localizer["\"{0}\" is instrumental.", $"{song.ArtistName} - {song.Title}"]);
        }

        // Some (or all) songs in Musixmatch have their "restricted" field set to 0 in the track data,
        // but the real "restricted" value is in the lyrics data, this means we can't filter those restricted songs
        // in the autocomplete results
        if (song.IsRestricted)
        {
            return FergunResult.FromError(_localizer["\"{0}\" has restricted lyrics.", $"{song.ArtistName} - {song.Title}"]);
        }

        if (string.IsNullOrEmpty(song.Lyrics))
        {
            return FergunResult.FromError(_localizer["Unable to get the lyrics of \"{0}\".", $"{song.ArtistName} - {song.Title}"]);
        }

        var chunks = song.Lyrics.SplitWithoutWordBreaking(EmbedBuilder.MaxDescriptionLength).ToArray();

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
            string links = $"{Format.Url(_localizer["View on Musixmatch"], song.Url)}  | {Format.Url(_localizer["View Artist"], song.ArtistUrl!)}";

            // Discord doesn't support custom protocols in embeds (spotify://track/id)
            if (song.SpotifyTrackId is not null)
                links += $" | {Format.Url(_localizer["Open in Spotify"], $"https://open.spotify.com/track/{song.SpotifyTrackId}?go=1")}";

            return new PageBuilder()
                .WithTitle($"{song.ArtistName} - {song.Title}".Truncate(EmbedBuilder.MaxTitleLength))
                .WithThumbnailUrl(song.SongArtImageUrl)
                .WithDescription(chunks[index].ToString())
                .AddField("Links", links)
                .WithFooter(_localizer["Lyrics by Musixmatch | Page {0} of {1}", index + 1, chunks.Length])
                .WithColor(Color.Orange);
        }
    }

    [SlashCommand("stats", "Sends the stats of the bot.")]
    public async Task<RuntimeResult> StatsAsync()
    {
        await Context.Interaction.DeferAsync();

        long temp;
        var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
        int cpuUsage = (int)await CommandUtils.GetCpuUsageForProcessAsync();
        string? cpu = null;
        long? totalRamUsage = null;
        long processRamUsage = 0;
        long? totalRam = null;
        string? os = RuntimeInformation.OSDescription;

        if (OperatingSystem.IsLinux())
        {
            // CPU Name
            if (File.Exists("/proc/cpuinfo"))
            {
                cpu = (await File.ReadAllLinesAsync("/proc/cpuinfo"))
                    .FirstOrDefault(x => x.StartsWith("model name", StringComparison.OrdinalIgnoreCase))?
                    .Split(':')
                    .ElementAtOrDefault(1)?
                    .Trim();
            }

            if (string.IsNullOrWhiteSpace(cpu))
            {
                cpu = CommandUtils.RunCommand("lscpu")?
                    .Split('\n')
                    .FirstOrDefault(x => x.StartsWith("model name", StringComparison.OrdinalIgnoreCase))?
                    .Split(':')
                    .ElementAtOrDefault(1)?
                    .Trim();

                if (string.IsNullOrWhiteSpace(cpu))
                {
                    cpu = "?";
                }
            }

            // OS Name
            if (File.Exists("/etc/lsb-release"))
            {
                string[] distroInfo = await File.ReadAllLinesAsync("/etc/lsb-release");
                os = distroInfo.ElementAtOrDefault(3)?.Split('=').ElementAtOrDefault(1)?.Trim('\"');
            }

            // Total RAM & total RAM usage
            string[]? output = CommandUtils.RunCommand("free -m")?.Split(Environment.NewLine);
            string[]? memory = output?.ElementAtOrDefault(1)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (long.TryParse(memory?.ElementAtOrDefault(1), out temp)) totalRam = temp;
            if (long.TryParse(memory?.ElementAtOrDefault(2), out temp)) totalRamUsage = temp;

            // Process RAM usage
            processRamUsage = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
        }
        else if (OperatingSystem.IsWindows())
        {
            // CPU Name
            cpu = CommandUtils.RunCommand("wmic cpu get name")
                ?.Trim()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .ElementAtOrDefault(1);

            // Total RAM & total RAM usage
            string[]? output = CommandUtils.RunCommand("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value")
                ?.Trim()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            if (output?.Length > 1)
            {
                long freeRam = 0;
                string[] split = output[0].Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 1 && long.TryParse(split[1], out temp))
                {
                    freeRam = temp / 1024;
                }

                split = output[1].Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 1 && long.TryParse(split[1], out temp))
                {
                    totalRam = temp / 1024;
                }

                if (totalRam != null && freeRam != 0)
                {
                    totalRamUsage = totalRam - freeRam;
                }
            }

            // Process RAM usage
            processRamUsage = Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024;
        }

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

        string? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var elapsed = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime;

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["Fergun Stats"])
            .AddField(_localizer["Operating System"], os, true)
            .AddField("\u200b", "\u200b", true)
            .AddField("CPU", cpu, true)
            .AddField(_localizer["CPU Usage"], $"{cpuUsage}%", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["RAM Usage"],
                $"{processRamUsage}MB ({(totalRam == null ? 0 : Math.Round((double)processRamUsage / totalRam.Value * 100, 2))}%) " +
                $"/ {(totalRamUsage == null || totalRam == null ? "?MB" : $"{totalRamUsage}MB ({Math.Round((double)totalRamUsage.Value / totalRam.Value * 100, 2)}%)")} " +
                $"/ {totalRam?.ToString() ?? "?"}MB", true)
            .AddField(_localizer["Library"], $"Discord.Net v{DiscordConfig.Version}", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["Bot Version"], version is null ? "?" : $"v{version}", true)
            .AddField(_localizer["Total Servers"], $"{guilds.Count} (Shard: {shard?.Guilds?.Count ?? guilds.Count})", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["Total Users"], $"{totalUsers?.ToString() ?? "?"} (Shard: {(totalUsersInShard ?? totalUsers)?.ToString() ?? "?"})", true)
            .AddField(_localizer["Shard ID"], shardId, true)
            .AddField("\u200b", "\u200b", true)
            .AddField("Shards", shards, true)
            .AddField(_localizer["Uptime"], elapsed.Humanize(3, _localizer.CurrentCulture, TimeUnit.Day), true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["Bot Owner"], owner, true);

        builder.WithColor(Color.Orange);

        await Context.Interaction.FollowupAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }
}