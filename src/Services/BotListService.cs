using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Fergun.Services
{
    /// <summary>
    /// Represents a service that updates the bot server count periodically (currently Top.gg and DiscordBots)
    /// </summary>
    public class BotListService : IDisposable
    {
        private readonly BaseSocketClient _client;
        private readonly Timer _updateTimer;
        private readonly Func<LogMessage, Task> _logger;
        private readonly HttpClient _topGgClient;
        private readonly HttpClient _discordBotsClient;
        private bool _topGgClientDisposed;
        private bool _discordBotsClientDisposed;
        private bool _disposed;
        private int _lastServerCount;

        public BotListService(BaseSocketClient client, string topGgToken = null, string discordBotsToken = null,
            TimeSpan? updatePeriod = null, Func<LogMessage, Task> logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? Task.FromResult;

            if (string.IsNullOrEmpty(topGgToken))
            {
                _topGgClientDisposed = true;
                _ = _logger(new LogMessage(LogSeverity.Info, "BotList", "Top.gg API token is empty or not set. Bot server count will not be sent to the API."));
            }
            else
            {
                _topGgClient = new HttpClient
                {
                    BaseAddress = new Uri("https://top.gg/api/")
                };
                _topGgClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(topGgToken);
            }

            if (string.IsNullOrEmpty(discordBotsToken))
            {
                _discordBotsClientDisposed = true;
                _ = _logger(new LogMessage(LogSeverity.Info, "BotList", "DiscordBots API token is empty or not set. Bot server count will not be sent to the API."));
            }
            else
            {
                _discordBotsClient = new HttpClient
                {
                    BaseAddress = new Uri("https://discord.bots.gg/api/v1/")
                };
                _discordBotsClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(discordBotsToken);
            }
            if (_topGgClientDisposed && _discordBotsClientDisposed)
            {
                return;
            }

            updatePeriod ??= TimeSpan.FromMinutes(30);
            _updateTimer = new Timer(OnTimerFired, null, updatePeriod.Value, updatePeriod.Value);
            _lastServerCount = _client.Guilds.Count;
        }

        private void OnTimerFired(object state)
        {
            _ = UpdateStatsAsync();
        }

        /// <summary>
        /// Manually updates the bot list server count using the client's guild count.
        /// </summary>

        public async Task UpdateStatsAsync() => await UpdateStatsAsync(_client.Guilds.Count);

        /// <summary>
        /// Manually updates the bot list server count using the specified server count.
        /// </summary>
        /// <param name="serverCount">The server count.</param>
        public async Task UpdateStatsAsync(int serverCount)
        {
            if (_lastServerCount == serverCount) return;

            await UpdateStatsAsync(serverCount, BotList.TopGg);
            await UpdateStatsAsync(serverCount, BotList.DiscordBots);
            _lastServerCount = serverCount;

            if (_topGgClientDisposed && _discordBotsClientDisposed)
            {
                Dispose();
            }
        }

        /// <summary>
        /// Manually updates a specific bot list server count using the specified server count.
        /// </summary>
        /// <param name="serverCount">The server count.</param>
        /// <param name="botList">The bot list.</param>
        public async Task UpdateStatsAsync(int serverCount, BotList botList)
        {
            switch (botList)
            {
                case BotList.TopGg when _topGgClientDisposed:
                case BotList.DiscordBots when _discordBotsClientDisposed:
                    return;
            }

            var httpClient = botList == BotList.TopGg ? _topGgClient : _discordBotsClient;
            string botListString = botList == BotList.TopGg ? "Top.gg" : "DiscordBots";
            await _logger(new LogMessage(LogSeverity.Info, "BotList", $"Updating {botListString} bot server count..."));
            HttpResponseMessage response = null;
            try
            {
                string serverCountString = botList == BotList.TopGg ? "server_count" : "guildCount";
                var content = new StringContent($"{{\"{serverCountString}\": {serverCount}}}", Encoding.UTF8, "application/json");

                response = await httpClient.PostAsync(new Uri($"bots/{_client.CurrentUser.Id}/stats", UriKind.Relative), content);
                response.EnsureSuccessStatusCode();
                await _logger(new LogMessage(LogSeverity.Warning, "BotList", $"Successfully updated {botListString} bot server count to {serverCount}."));
            }
            catch (Exception e)
            {
                await _logger(new LogMessage(LogSeverity.Warning, "BotList", $"Failed to update {botListString} bot server count to {serverCount}.", e));
                if (response != null)
                {
                    int code = (int)response.StatusCode;
                    if (code == 401 || code == 403 || code == 404)
                    {
                        string message = code == 404 ? $"Got error {code}, make sure the bot is listed on {botListString}." : $"Got error {code}, make sure the token is valid.";
                        await _logger(new LogMessage(LogSeverity.Info, "BotList", message));
                        await _logger(new LogMessage(LogSeverity.Info, "BotList", $"Bot server count will not be sent to {botListString} API."));
                        httpClient.Dispose();
                        if (botList == BotList.TopGg)
                        {
                            _topGgClientDisposed = true;
                        }
                        else
                        {
                            _discordBotsClientDisposed = true;
                        }
                    }
                }
            }
        }

        public enum BotList
        {
            TopGg,
            DiscordBots
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _updateTimer?.Dispose();
                _topGgClient?.Dispose();
                _discordBotsClient?.Dispose();
                _topGgClientDisposed = true;
                _discordBotsClientDisposed = true;
            }

            _disposed = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
    }
}