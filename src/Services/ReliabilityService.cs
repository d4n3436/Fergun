using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Fergun.Services
{
    // This service requires that your bot is being run by a daemon that handles
    // Exit Code 1 (or any exit code) as a restart.
    //
    // If you do not have your bot setup to run in a daemon, this service will just
    // terminate the process and the bot will not restart.
    //
    // Links to daemons:
    // [Powershell (Windows+Unix)] https://gitlab.com/snippets/21444
    // [Bash (Unix)] https://stackoverflow.com/a/697064
    public class ReliabilityService : IDisposable
    {
        private readonly IDiscordClient _client;
        private readonly Func<LogMessage, Task> _logger;
        private CancellationTokenSource _cts;
        private bool _isReconnecting;
        private bool _disposed;

        // How long should we wait on the client to reconnect before resetting?
        private readonly TimeSpan _timeout;

        // Should we attempt to reset the client? Set this to false if your client is still locking up.
        private readonly bool _attemptReset;
        private const string _logSource = "Reliability";

        public ReliabilityService(DiscordSocketClient client, Func<LogMessage, Task> logger = null, TimeSpan? timeout = null, bool attemptReset = true)
        {
            _cts = new CancellationTokenSource();
            _client = client;
            _logger = logger ?? (_ => Task.CompletedTask);
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _attemptReset = attemptReset;

            client.Connected += ConnectedAsync;
            client.Disconnected += DisconnectedAsync;
        }

        public ReliabilityService(DiscordShardedClient client, Func<LogMessage, Task> logger = null, TimeSpan? timeout = null, bool attemptReset = true)
        {
            _cts = new CancellationTokenSource();
            _client = client;
            _logger = logger ?? (_ => Task.CompletedTask);
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _attemptReset = attemptReset;

            client.ShardConnected += ShardConnectedAsync;
            client.ShardDisconnected += ShardDisconnectedAsync;
        }

        /// <summary>
        /// Disposes the cancellation token
        /// and unsubscribes from the <see cref="DiscordSocketClient.Connected"/> and <see cref="DiscordSocketClient.Disconnected"/> events.
        /// </summary>
        /// <remarks>
        /// If a sharded client is used, unsubscribes from the <see cref="DiscordShardedClient.ShardConnected"/> and <see cref="DiscordShardedClient.ShardDisconnected"/> events.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ReliabilityService), "Service has been disposed.");
            }

            if (!disposing) return;
            _cts.Dispose();
            _cts = null;

            switch (_client)
            {
                case DiscordSocketClient socketClient:
                    socketClient.Connected -= ConnectedAsync;
                    socketClient.Disconnected -= DisconnectedAsync;
                    break;

                case DiscordShardedClient shardedClient:
                    shardedClient.ShardConnected -= ShardConnectedAsync;
                    shardedClient.ShardDisconnected -= ShardDisconnectedAsync;
                    break;
            }

            _disposed = true;
        }

        private Task ConnectedAsync()
        {
            if (_isReconnecting) return Task.CompletedTask;
            // Cancel all previous state checks and reset the CancelToken - client is back online
            _ = _logger(new LogMessage(LogSeverity.Debug, _logSource, "Client reconnected, resetting cancel tokens..."));
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _ = _logger(new LogMessage(LogSeverity.Debug, _logSource, "Client reconnected, cancel tokens reset."));

            return Task.CompletedTask;
        }

        private Task ShardConnectedAsync(DiscordSocketClient client) => ConnectedAsync();

        public Task DisconnectedAsync(Exception exception)
        {
            if (exception is GatewayReconnectException)
            {
                _isReconnecting = true;
            }
            else
            {
                _isReconnecting = false;
                // Check the state after <timeout> to see if we reconnected
                _ = Task.Run(async () =>
                {
                    await _logger(new LogMessage(LogSeverity.Info, _logSource, "Client disconnected, starting timeout task..."));
                    await Task.Delay(_timeout, _cts.Token);
                    await _logger(new LogMessage(LogSeverity.Debug, _logSource, "Timeout expired, continuing to check client state..."));
                    await CheckStateAsync();
                    await _logger(new LogMessage(LogSeverity.Debug, _logSource, "State came back."));
                });
            }
            return Task.CompletedTask;
        }

        private Task ShardDisconnectedAsync(Exception exception, DiscordSocketClient client)
        {
            return ((DiscordShardedClient)_client).Shards.All(x => x.ConnectionState == ConnectionState.Disconnected)
                ? DisconnectedAsync(exception)
                : Task.CompletedTask;
        }

        private async Task CheckStateAsync()
        {
            // Client reconnected, no need to reset
            if (_client.ConnectionState == ConnectionState.Connected) return;
            if (_attemptReset)
            {
                await _logger(new LogMessage(LogSeverity.Info, _logSource, "Attempting to reset the client..."));

                var timeout = Task.Delay(_timeout);
                var connect = _client.StartAsync();
                var task = await Task.WhenAny(timeout, connect);

                if (task == timeout)
                {
                    await _logger(new LogMessage(LogSeverity.Critical, _logSource, "Client reset timed out (task deadlocked?), killing process."));
                    FailFast();
                }
                else if (connect.IsFaulted)
                {
                    await _logger(new LogMessage(LogSeverity.Critical, _logSource, "Client reset faulted, killing process.", connect.Exception));
                    FailFast();
                }
                else if (connect.IsCompletedSuccessfully)
                    await _logger(new LogMessage(LogSeverity.Info, _logSource, "Client reset successfully!"));
                return;
            }

            await _logger(new LogMessage(LogSeverity.Critical, _logSource, "Client did not reconnect in time, killing process."));
            FailFast();
        }

        private static void FailFast() => Environment.Exit(1);
    }
}