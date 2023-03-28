using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fergun.Services;

/// <summary>
/// Represents a service that updates the bot stats periodically (currently Top.gg and Discord Bots).
/// </summary>
public sealed class BotListService : BackgroundService
{
    private readonly DiscordShardedClient _discordClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BotListService> _logger;
    private readonly BotListOptions _options;
    private int _lastServerCount = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotListService"/> class.
    /// </summary>
    /// <param name="discordClient">The Discord client.</param>
    /// <param name="httpClientFactoryFactory">The factory of <see cref="HttpClient"/> instances.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The bot list options.</param>
    public BotListService(DiscordShardedClient discordClient, IHttpClientFactory httpClientFactoryFactory, ILogger<BotListService> logger, IOptions<BotListOptions> options)
    {
        _discordClient = discordClient;
        _httpClientFactory = httpClientFactoryFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Updates the bot list server count.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask UpdateStatsAsync()
    {
        int serverCount = _discordClient.Guilds.Count;
        if (_lastServerCount == -1) _lastServerCount = serverCount;
        else if (_lastServerCount == serverCount) return;

        foreach ((var botList, string token) in _options.Tokens)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                await UpdateStatsAsync(botList, serverCount, _discordClient.Shards.Count, token).ConfigureAwait(false);
            }
        }

        _lastServerCount = serverCount;
    }

    /// <summary>
    /// Updates the bot stats of a specific bot list using the specified server count and shard count.
    /// </summary>
    /// <param name="botList">The bot list.</param>
    /// <param name="serverCount">The server count.</param>
    /// <param name="shardCount">The shard count.</param>
    /// <param name="token">The API token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateStatsAsync(BotList botList, int serverCount, int shardCount, string token)
    {
        _logger.LogDebug("Updating {BotList} bot stats...", botList);

        using var request = CreateRequest(botList, serverCount, shardCount, token);
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Successfully updated {BotList} bot stats (server count: {ServerCount}, shard count: {ShardCount}).", botList, serverCount, shardCount);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to update {BotList} bot stats (server count: {ServerCount}, shard count: {ShardCount}).", botList, serverCount, shardCount);

            if (e is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound } requestException)
            {
                var statusCode = requestException.StatusCode.Value;

                if (statusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Got status code {StatusCode} ({StatusCodeName}), make sure the bot is listed on {BotList}.", statusCode.ToString("D"), statusCode, botList);
                }
                else
                {
                    _logger.LogInformation("Got status code {StatusCode} ({StatusCodeName}), make sure the token is valid.", statusCode.ToString("D"), statusCode);
                }

                _logger.LogInformation("Bot stats will not be sent to {BotList} API.", botList);
                _options.Tokens.Remove(botList);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botLists = _options.Tokens.Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToArray();
        foreach (var botList in botLists)
        {
            _options.Tokens.Remove(botList);
        }

        if (_options.Tokens.Count == 0)
        {
            _logger.LogInformation("Bot list service started. No bot stats will be updated because no tokens were provided.");
            return;
        }

        _logger.LogInformation("Bot list service started. Updating stats for {BotLists} every {UpdatePeriod}.",
            string.Join(", ", _options.Tokens.Keys), _options.UpdatePeriod.ToString("h'h 'm'm 's's'"));

        using var timer = new PeriodicTimer(_options.UpdatePeriod);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await UpdateStatsAsync().ConfigureAwait(false);
        }
    }

    private HttpRequestMessage CreateRequest(BotList botList, int serverCount, int shardCount, string token) => botList switch
    {
        BotList.TopGg => CreateTopGgRequest(serverCount, shardCount, token),
        BotList.DiscordBots => CreateDiscordBotsRequest(serverCount, shardCount, token),
        _ => throw new ArgumentException($"Unknown bot list {botList}.")
    };

    private HttpRequestMessage CreateTopGgRequest(int serverCount, int shardCount, string token) => new()
    {
        Method = HttpMethod.Post,
        RequestUri = new Uri($"https://top.gg/api/bots/{_discordClient.CurrentUser.Id}/stats"),
        Content = new StringContent($"{{\"server_count\": {serverCount},\"shard_count\":{shardCount}}}", Encoding.UTF8, "application/json"),
        Headers =
        {
            Authorization = new AuthenticationHeaderValue(token)
        }
    };

    private HttpRequestMessage CreateDiscordBotsRequest(int serverCount, int shardCount, string token) => new()
    {
        Method = HttpMethod.Post,
        RequestUri = new Uri($"https://discord.bots.gg/api/v1/bots/{_discordClient.CurrentUser.Id}/stats"),
        Content = new StringContent($"{{\"guildCount\": {serverCount},\"shardCount\":{shardCount}}}", Encoding.UTF8, "application/json"),
        Headers =
        {
            Authorization = new AuthenticationHeaderValue(token)
        }
    };
}