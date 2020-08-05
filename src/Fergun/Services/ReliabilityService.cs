using System;
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
    public class ReliabilityService
    {
        // Change log levels if desired:
        private const LogSeverity _debug = LogSeverity.Debug;
        private const LogSeverity _info = LogSeverity.Info;
        private const LogSeverity _critical = LogSeverity.Critical;

        private readonly DiscordSocketClient _client;
        private readonly Func<LogMessage, Task> _logger;
        private CancellationTokenSource _cts;
        private bool isReconnecting = false;

        // How long should we wait on the client to reconnect before resetting?
        private readonly TimeSpan _timeout;

        // Should we attempt to reset the client? Set this to false if your client is still locking up.
        private readonly bool _attemptReset = true;

        public ReliabilityService(DiscordSocketClient client, Func<LogMessage, Task> logger = null, TimeSpan? timeout = null, bool attemptReset = true)
        {
            _cts = new CancellationTokenSource();
            _client = client;
            _logger = logger ?? (_ => Task.CompletedTask);
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _attemptReset = attemptReset;

            _client.Connected += ConnectedAsync;
            _client.Disconnected += DisconnectedAsync;
        }

        public Task ConnectedAsync()
        {
            if (!isReconnecting)
            {
                // Cancel all previous state checks and reset the CancelToken - client is back online
                _ = DebugAsync("Client reconnected, resetting cancel tokens...");
                _cts.Cancel();
                _cts = new CancellationTokenSource();
                _ = DebugAsync("Client reconnected, cancel tokens reset.");
            }

            return Task.CompletedTask;
        }

        public Task DisconnectedAsync(Exception exception)
        {
            if (exception is GatewayReconnectException)
            {
                isReconnecting = true;
            }
            else
            {
                isReconnecting = false;
                // Check the state after <timeout> to see if we reconnected
                _ = InfoAsync("Client disconnected, starting timeout task...");
                _ = Task.Delay(_timeout, _cts.Token).ContinueWith(async _ =>
                {
                    await DebugAsync("Timeout expired, continuing to check client state...");
                    await CheckStateAsync();
                    await DebugAsync("State came back.");
                });
            }
            return Task.CompletedTask;
        }

        private async Task CheckStateAsync()
        {
            // Client reconnected, no need to reset
            if (_client.ConnectionState == ConnectionState.Connected) return;
            if (_attemptReset)
            {
                await InfoAsync("Attempting to reset the client...");

                var timeout = Task.Delay(_timeout);
                var connect = _client.StartAsync();
                var task = await Task.WhenAny(timeout, connect);

                if (task == timeout)
                {
                    await CriticalAsync("Client reset timed out (task deadlocked?), killing process.");
                    FailFast();
                }
                else if (connect.IsFaulted)
                {
                    await CriticalAsync("Client reset faulted, killing process.", connect.Exception);
                    FailFast();
                }
                else if (connect.IsCompletedSuccessfully)
                    await InfoAsync("Client reset succesfully!");
                return;
            }

            await CriticalAsync("Client did not reconnect in time, killing process.");
            FailFast();
        }

        private static void FailFast() => Environment.Exit(1);

        // Logging Helpers
        private const string LogSource = "Reliability";

        private Task DebugAsync(string message)
            => _logger.Invoke(new LogMessage(_debug, LogSource, message));

        private Task InfoAsync(string message)
            => _logger.Invoke(new LogMessage(_info, LogSource, message));

        private Task CriticalAsync(string message, Exception error = null)
            => _logger.Invoke(new LogMessage(_critical, LogSource, message, error));
    }
}