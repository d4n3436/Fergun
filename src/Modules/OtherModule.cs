using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Genius;
using Fergun.Data;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Modules.Handlers;
using Fergun.Utils;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fergun.Modules;

public class OtherModule : InteractionModuleBase
{
    private readonly ILogger<OtherModule> _logger;
    private readonly IFergunLocalizer<OtherModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly InteractiveService _interactive;
    private readonly IGeniusClient _geniusClient;
    private readonly HttpClient _httpClient;
    private readonly FergunContext _db;

    public OtherModule(ILogger<OtherModule> logger, IFergunLocalizer<OtherModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions,
        InteractiveService interactive, IGeniusClient geniusClient, HttpClient httpClient, FergunContext db)
    {
        _logger = logger;
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _geniusClient = geniusClient;
        _httpClient = httpClient;
        _interactive = interactive;
        _db = db;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("cmdstats", "Displays the command usage stats.")]
    public async Task<RuntimeResult> CommandStatsAsync()
    {
        await Context.Interaction.DeferAsync();

        var commandStats = (await _db.CommandStats
            .AsNoTracking()
            .OrderByDescending(x => x.UsageCount)
            .ToListAsync())
            .Chunk(25)
            .ToArray();

        if (commandStats.Length == 0)
        {
            return FergunResult.FromError(_localizer["No stats to display."]);
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(commandStats.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes(_fergunOptions)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, _fergunOptions.PaginatorTimeout, InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int index) => new PageBuilder()
                .WithTitle(_localizer["Command Stats"])
                .WithDescription(string.Join('\n', commandStats[index].Select((x, i) => $"{index * 25 + i + 1}. `{x.Name}`: {x.UsageCount}")))
                .WithFooter(_localizer["Page {0} of {1}", index + 1, commandStats.Length])
                .WithColor(Color.Orange);
    }

    [SlashCommand("inspirobot", "Sends an inspirational quote.")]
    public async Task<RuntimeResult> InspiroBotAsync()
    {
        await Context.Interaction.DeferAsync();

        string url = await _httpClient.GetStringAsync(new Uri("https://inspirobot.me/api?generate=true"));

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
    public async Task<RuntimeResult> LyricsAsync([Autocomplete(typeof(GeniusAutocompleteHandler))] [Summary(name: "name", description: "The name of the song.")] int id)
    {
        await Context.Interaction.DeferAsync();
        var song = await _geniusClient.GetSongAsync(id);

        if (song is null)
        {
            return FergunResult.FromError(_localizer["Unable to find a song with ID {0}. Use the autocomplete results.", id]);
        }
        
        if (song.IsInstrumental)
        {
            // This shouldn't be reachable unless someone manually passes an instrumental ID.
            return FergunResult.FromError(_localizer["\"{0}\" is instrumental.", $"{song.ArtistNames} - {song.Title}"]);
        }

        if (song.Lyrics is null)
        {
            return FergunResult.FromError(_localizer["Unable to get the lyrics of \"{0}\".", $"{song.ArtistNames} - {song.Title}"]);
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
            var chunk = chunks[index];
            
            return new PageBuilder()
                .WithTitle($"{song.ArtistNames} - {song.Title}".Truncate(EmbedBuilder.MaxTitleLength))
                .WithThumbnailUrl(song.SongArtImageUrl)
                .WithDescription(chunk.ToString())
                .AddField("Links", $"{Format.Url(_localizer["View on Genius"], song.Url)}{(song.PrimaryArtistUrl is null ? "" : $" | {Format.Url(_localizer["View Artist"], song.PrimaryArtistUrl)}")}")
                .WithFooter(_localizer["Lyrics by Genius | Page {0} of {1}", index + 1, chunks.Length])
                .WithColor((Color)(song.PrimaryArtColor ?? Color.Orange));
        }
    }

    [SlashCommand("stats", "Sends the stats of the bot.")]
    public async Task<RuntimeResult> StatsAsync()
    {
        await Context.Interaction.DeferAsync();

        long temp;
        var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
        var cpuUsage = (int)await CommandUtils.GetCpuUsageForProcessAsync();
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
                cpu = File.ReadAllLines("/proc/cpuinfo")
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
                var distroInfo = File.ReadAllLines("/etc/lsb-release");
                os = distroInfo.ElementAtOrDefault(3)?.Split('=').ElementAtOrDefault(1)?.Trim('\"');
            }

            // Total RAM & total RAM usage
            var output = CommandUtils.RunCommand("free -m")?.Split(Environment.NewLine);
            var memory = output?.ElementAtOrDefault(1)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

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
            var output = CommandUtils.RunCommand("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value")
                ?.Trim()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            if (output?.Length > 1)
            {
                long freeRam = 0;
                var split = output[0].Split('=', StringSplitOptions.RemoveEmptyEntries);
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
            .AddField(_localizer["Total Users"], $"{totalUsers?.ToString() ?? "?"} (Shard: {totalUsersInShard?.ToString() ?? totalUsers?.ToString() ?? "?"})", true)
            .AddField(_localizer["Shard ID"], shardId, true)
            .AddField("\u200b", "\u200b", true)
            .AddField("Shards", shards, true)
            .AddField(_localizer["Uptime"], elapsed.Humanize(), true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["Bot Owner"], owner, true);

        builder.WithColor(Color.Orange);

        await Context.Interaction.FollowupAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }
}