using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Extensions;
using Fergun.Utils;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

public class OtherModule : InteractionModuleBase
{
    private readonly ILogger<OtherModule> _logger;
    private readonly IFergunLocalizer<OtherModule> _localizer;
    private readonly HttpClient _httpClient;

    public OtherModule(ILogger<OtherModule> logger, IFergunLocalizer<OtherModule> localizer, HttpClient httpClient)
    {
        _logger = logger;
        _localizer = localizer;
        _httpClient = httpClient;
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

        if (Context.Client is DiscordShardedClient shardedClient)
        {
            guilds = shardedClient.Guilds;
            shards = shardedClient.Shards.Count;
            shardId = Context.Channel.IsPrivate() ? 0 : shardedClient.GetShardIdFor(Context.Guild);
            shard = shardedClient.GetShard(shardId);
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