using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBotsList.Api;
using DiscordBotsList.Api.Objects;
using Fergun.APIs.DiscordBots;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Services;
using Fergun.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Victoria;

namespace Fergun
{
#pragma warning disable CA1001
    public class FergunClient
#pragma warning restore CA1001
    {
        public static FergunDatabase Database { get; private set; }
        public static FergunConfig Config { get; private set; }
        public static DateTimeOffset Uptime { get; private set; }
        public static bool IsDebugMode { get; private set; }
        public static string DblBotPage { get; private set; }
        public static string InviteLink { get; private set; }
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static ConcurrentBag<CachedMessage> MessageCache { get; } = new ConcurrentBag<CachedMessage>();
        public static ReadOnlyDictionary<string, CultureInfo> Languages { get; private set; }

        private DiscordSocketClient _client;
        private LogService _logService;
        private readonly CommandService _cmdService;
        private static IServiceProvider _services;
        private static CommandHandlingService _cmdHandlingService;
        private static ReliabilityService _reliabilityService;
        private static CommandCacheService _commandCacheService;
        private static bool _firstConnect = true;
        private static AuthDiscordBotListApi _dblApi;
        private static IDblSelfBot _dblBot;
        private static DiscordBotsApi _discordBots;
        private static Timer _autoClear;

        public FergunClient()
        {
#if DEBUG
            IsDebugMode = true;
#else
            IsDebugMode = false;
#endif
            _cmdService = new CommandService(Constants.CommandServiceConfig);
            _logService = new LogService();
            _autoClear = new Timer(OnTimerFired, null, Constants.MessageCacheClearInterval, Constants.MessageCacheClearInterval);
        }

        ~FergunClient()
        {
            if (_autoClear != null)
            {
                _autoClear.Dispose();
            }
        }

        public async Task InitializeAsync()
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Bot", $"Fergun v{Constants.Version}"));

            Languages = new ReadOnlyDictionary<string, CultureInfo>(GetAvailableCultures().ToDictionary(x => x.TwoLetterISOLanguageName, x => x));
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"{Languages.Count} available language(s) ({string.Join(", ", Languages.Keys)})."));

            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Config", "Loading the config..."));
            Config = await LoadConfigAsync<FergunConfig>(Path.Combine(AppContext.BaseDirectory, Constants.BotConfigFile));

            if (Config == null)
            {
                Console.Write("Closing in 30 seconds... Press any key to exit now.");
                await ExitWithInputTimeoutAsync(30, 1);
            }
            try
            {
                TokenUtils.ValidateToken(TokenType.Bot, IsDebugMode ? Config.DevToken : Config.Token);
            }
            catch (ArgumentException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Config", $"Failed to validate {(IsDebugMode ? "dev " : "")}bot token", e));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Config", $"Make sure the value in key {(IsDebugMode ? "Dev" : "")}Token, in the config file ({Constants.BotConfigFile}) is valid."));

                Console.Write("Closing in 30 seconds... Press any key to exit now.");
                await ExitWithInputTimeoutAsync(30, 1);
            }

            // LogSeverity.Debug is too verbose
            if (Config.LavaConfig.LogSeverity == LogSeverity.Debug)
            {
                Config.LavaConfig.LogSeverity = LogSeverity.Verbose;
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Database", "Connecting to the database..."));
            Database = new FergunDatabase(Constants.FergunDatabase, Config.DatabaseConfig.ConnectionString);

            if (Database.IsConnected)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Database", "Connected to the database successfully."));
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Database", "Could not connect to the database."));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Database", "Ensure the MongoDB server you're trying to log in is running"));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Database", $"and make sure the server credentials in the config file ({Constants.BotConfigFile}) are correct."));

                Console.Write("Closing in 30 seconds... Press any key to exit now.");
                await ExitWithInputTimeoutAsync(30, 1);
            }

            if (string.IsNullOrEmpty(DatabaseConfig.GlobalPrefix))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Database", "The bot prefix has not been set."));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Database", $"Please set a value in the field \"{(IsDebugMode ? "Dev" : "")}GlobalPrefix\", in collection \"Config\", in the database."));

                Console.Write("Closing in 30 seconds... Press any key to exit now.");
                await ExitWithInputTimeoutAsync(30, 1);
            }

            GuildUtils.Initialize();

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Using presence intent: {Config.PresenceIntent}"));
            if (Config.PresenceIntent)
            {
                Constants.ClientConfig.GatewayIntents |= GatewayIntents.GuildPresences;
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Using server members intent: {Config.ServerMembersIntent}"));
            if (Config.ServerMembersIntent)
            {
                Constants.ClientConfig.GatewayIntents |= GatewayIntents.GuildMembers;
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Using reliability service: {Config.UseReliabilityService}"));
            if (Config.UseReliabilityService)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", "The bot will be shut down in case of deadlock. Remember to use a daemon!"));
            }
            Constants.ClientConfig.AlwaysDownloadUsers = Config.AlwaysDownloadUsers;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Always download users: {Constants.ClientConfig.AlwaysDownloadUsers}"));

            Constants.ClientConfig.MessageCacheSize = Config.MessageCacheSize;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Message cache size: {Constants.ClientConfig.MessageCacheSize}"));

            _client = new DiscordSocketClient(Constants.ClientConfig);
            _client.Ready += ClientReady;
            _client.JoinedGuild += JoinedGuild;
            _client.LeftGuild += LeftGuild;
            _client.MessageUpdated += MessageUpdated;
            _client.MessageDeleted += MessageDeleted;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            _client.UserJoined += UserJoined;
            _client.UserLeft += UserLeft;
            _client.UserBanned += UserBanned;
            _client.UserUnbanned += UserUnbanned;

            _logService.Dispose();
            _logService = new LogService(_client, _cmdService);
            if (Config.UseReliabilityService)
            {
                _reliabilityService = new ReliabilityService(_client, x => _ = _logService.LogAsync(x));
            }

            _commandCacheService = new CommandCacheService(_client, Constants.MessageCacheCapacity,
                message => _ = _cmdHandlingService.HandleCommandAsync(message),
                log => _ = _logService.LogAsync(log),
                Constants.CommandCacheClearInterval, Constants.MaxCommandCacheLongevity);

            _services = SetupServices();

            _cmdHandlingService = new CommandHandlingService(_client, _cmdService, _logService, _services);
            await _cmdHandlingService.InitializeAsync();

            if (Config.LavaConfig.Hostname == "127.0.0.1" || Config.LavaConfig.Hostname == "0.0.0.0" || Config.LavaConfig.Hostname == "localhost")
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", "Using local lavalink server. Updating and starting Lavalink..."));
                await UpdateLavalinkAsync();
                await StartLavalinkAsync();
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", "Using remote lavalink server."));
            }

            await _client.LoginAsync(TokenType.Bot, IsDebugMode ? Config.DevToken : Config.Token, false);
            await _client.StartAsync();

            if (!IsDebugMode)
            {
                await _client.SetActivityAsync(new Game($"{DatabaseConfig.GlobalPrefix}help"));
            }

            // Block this task until the program is closed.
            await Task.Delay(Timeout.Infinite);
        }

        private async Task<T> LoadConfigAsync<T>(string path) where T : class, new()
        {
            T config = null;

            if (File.Exists(path))
            {
                try
                {
                    config = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
                    if (config == null)
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Config", "Unknown error reading/deserializing the config file."));
                    }
                    else
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Config", "Loaded the config successfully."));
                    }
                }
                catch (IOException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Config", "Error reading the config file", e));
                }
                catch (JsonException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Config", "Error deserializing the config file", e));
                }
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Config", "No config file found. Creating default config file."));

                // Create a default config file.
                try
                {
                    File.WriteAllText(path, JsonConvert.SerializeObject(new T(), Formatting.Indented));
                }
                catch (IOException) { }
            }

            return config;
        }

        private static async Task ExitWithInputTimeoutAsync(int timeout, int exitCode)
        {
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(timeout)), Task.Run(() => Console.ReadKey(true)));
            Environment.Exit(exitCode);
        }

        private async Task StartLavalinkAsync()
        {
            Process[] processList = Process.GetProcessesByName("java");
            if (processList.Length == 0)
            {
                string lavalinkFile = Path.Combine(AppContext.BaseDirectory, "Lavalink", "Lavalink.jar");
                if (!File.Exists(lavalinkFile))
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Lavalink", "Lavalink.jar not found."));
                    return;
                }
                ProcessStartInfo process = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{Path.Combine(AppContext.BaseDirectory, "Lavalink")}/Lavalink.jar\"",
                    WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "Lavalink"),
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                if (!IsLinux)
                {
                    // Try to get the java exe path
                    var enviromentPath = Environment.GetEnvironmentVariable("PATH");
                    var paths = enviromentPath.Split(Path.PathSeparator);
                    var exePath = paths.FirstOrDefault(x => File.Exists(Path.Combine(x, "java.exe")));

                    if (exePath != null)
                    {
                        process.FileName = Path.Combine(exePath, "java.exe");
                    }
                }
                Process.Start(process);
                await Task.Delay(2000);
            }
        }

        private async Task UpdateLavalinkAsync()
        {
            string lavalinkDir = Path.Combine(AppContext.BaseDirectory, "Lavalink");
            string lavalinkFile = Path.Combine(lavalinkDir, "Lavalink.jar");
            string versionFile = Path.Combine(lavalinkDir, "VERSION.txt");

            if (!File.Exists(lavalinkFile)) return;
            string remoteVersion;
            try
            {
                using (WebClient wc = new WebClient())
                {
                    remoteVersion = await wc.DownloadStringTaskAsync("https://ci.fredboat.com/repository/download/Lavalink_Build/lastSuccessful/VERSION.txt?guest=1");
                }
            }
            catch (WebException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while downloading VERSION.txt", e));
                return;
            }

            string localVersion;
            if (File.Exists(versionFile))
            {
                try
                {
                    localVersion = File.ReadAllText(versionFile);
                }
                catch (IOException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while reading local VERSION.txt", e));
                    return;
                }
                if (localVersion != remoteVersion)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "A new dev build has been found."));
                }
                else
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "Lavalink is up to date."));
                    return;
                }
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "Local VERSION.txt not found or can't be read. Asuming the remote version is newer than the local..."));
            }

            Process[] processList = Process.GetProcessesByName("java");
            if (processList.Length != 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "There's a running instance of Lavalink (or a java app) and it's not possible to kill it since it's probably in use."));
                return;
            }

            try
            {
                File.Move(lavalinkFile, Path.ChangeExtension(lavalinkFile, ".jar.bak"), true);
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while renaming local Lavalink.jar", e));
                return;
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "Downloading the new dev build of Lavalink..."));
            try
            {
                using (WebClient wc = new WebClient())
                {
                    await wc.DownloadFileTaskAsync("https://ci.fredboat.com/repository/download/Lavalink_Build/lastSuccessful/Lavalink.jar?guest=1", lavalinkFile);
                }
            }
            catch (WebException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while downloading the new dev build", e));
                try
                {
                    if (File.Exists(lavalinkFile))
                    {
                        File.Delete(lavalinkFile);
                    }
                    File.Move(lavalinkFile, Path.ChangeExtension(lavalinkFile, ".jar"));
                }
                catch (IOException) { }
                return;
            }
            try
            {
                File.WriteAllText(versionFile, remoteVersion);
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while updating local VERSION.txt", e));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "Finished updating Lavalink."));
        }

        private IServiceProvider SetupServices()
        {
            var collection = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_cmdService)
                .AddSingleton(_logService)
                .AddSingleton(_commandCacheService)
                .AddSingleton(Config.LavaConfig)
                .AddSingleton<LavaNode>()
                .AddSingleton<InteractiveService>()
                .AddSingleton<MusicService>();

            if (_reliabilityService != null)
            {
                collection.AddSingleton(_reliabilityService);
            }
            return collection.BuildServiceProvider();
        }

        private static IEnumerable<CultureInfo> GetAvailableCultures()
        {
            List<CultureInfo> result = new List<CultureInfo>();

            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (CultureInfo culture in cultures)
            {
                try
                {
                    if (!culture.Equals(CultureInfo.InvariantCulture))
                    {
                        ResourceSet rs = strings.ResourceManager.GetResourceSet(culture, true, false);
                        if (rs != null)
                        {
                            result.Add(culture);
                        }
                    }
                }
                catch (CultureNotFoundException) { }
            }
            return result;
        }

        private async Task ClientReady()
        {
            if (_firstConnect)
            {
                if (!IsDebugMode)
                {
                    InviteLink = $"https://discord.com/oauth2/authorize?client_id={_client.CurrentUser.Id}&scope=bot&permissions={(ulong)Constants.InvitePermissions}";

                    if (string.IsNullOrEmpty(Config.DblApiToken))
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Stats", $"Top.gg API token is empty or has not been established. Bot server count will not be sent to the API."));
                    }
                    else
                    {
                        _dblApi = new AuthDiscordBotListApi(_client.CurrentUser.Id, Config.DblApiToken);
                        DblBotPage = $"https://top.gg/bot/{_client.CurrentUser.Id}";
                    }

                    if (string.IsNullOrEmpty(Config.DblApiToken))
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Stats", $"DiscordBots API token is empty or has not been established. Bot server count will not be sent to the API."));
                    }
                    else
                    {
                        _discordBots = new DiscordBotsApi(Config.DiscordBotsApiToken);
                    }

                    await UpdateBotListStatsAsync();
                }
                Uptime = DateTimeOffset.UtcNow;
                _firstConnect = false;
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Bot", $"{_client.CurrentUser.Username} is online!"));
        }

        private void OnTimerFired(object state)
        {
            var purge = MessageCache.Where(p =>
            {
                TimeSpan difference = DateTimeOffset.UtcNow - p.CreatedAt;
                return difference.TotalHours >= Constants.MaxMessageCacheLongevity;
            }).ToList();

            var removed = purge.Where(p => MessageCache.TryTake(out _));

            _ = _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "MsgCache", $"Cleaned {removed.Count()} deleted / edited messages from the cache."));
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> cachedbefore, SocketMessage after, ISocketMessageChannel channel)
        {
            if (string.IsNullOrEmpty(after?.Content) || after.Source != MessageSource.User) return;
            IMessage before = cachedbefore.Value;
            if (string.IsNullOrEmpty(before?.Content) || before.Content == after.Content) return;

            MessageCache.Add(new CachedMessage(before, DateTimeOffset.UtcNow, SourceEvent.MessageUpdated));
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "MsgUpdated", $"1 message edited by {after.Author} in {channel.Display()}"));
        }

        private async Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            IMessage message = cache.Value;
            if (message?.Source != MessageSource.User) return;

            MessageCache.Add(new CachedMessage(message, DateTimeOffset.UtcNow, SourceEvent.MessageDeleted));
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "MsgDeleted", $"1 message deleted by {message.Author} in {channel.Display()}"));
        }

        private async Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> msgs, ISocketMessageChannel channel)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "MsgDeleted", $"{msgs.Count} messages deleted in {channel.Display()}"));
        }

        private async Task UserJoined(SocketGuildUser user)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "UserJoined", $"User \"{user}\" joined the guild \"{user.Guild.Name}\""));
        }

        private async Task UserLeft(SocketGuildUser user)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "UserLeft", $"User \"{user}\" left the guild \"{user.Guild.Name}\""));
        }

        private async Task UserBanned(SocketUser user, SocketGuild guild)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "UserBan", $"User \"{user}\" was banned from guild \"{guild.Name}\""));
        }

        private async Task UserUnbanned(SocketUser user, SocketGuild guild)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "UserUnban", $"User \"{user}\" was unbanned from guild \"{guild.Name}\""));
        }

        private async Task JoinedGuild(SocketGuild guild)
        {
            var config = Database.FindDocument<GuildConfig>(Constants.GuildConfigCollection, x => x.Id == guild.Id);
            if (config != null && config.IsBlacklisted)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "JoinGuild", $"Someone tried to add me to the blacklisted guild \"{guild.Name}\" ({guild.Id})"));
                await guild.LeaveAsync();
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "JoinGuild", $"Bot has joined the guild \"{guild.Name}\" ({guild.Id})"));
                if (guild.PreferredLocale != null)
                {
                    string languageCode = guild.PreferredCulture.TwoLetterISOLanguageName;
                    if (Languages.ContainsKey(languageCode) && languageCode != (DatabaseConfig.Language ?? Constants.DefaultLanguage))
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "JoinGuild", $"A preferred language ({languageCode}) was found in the guild {guild.Id}. Saving the preferred language in the database..."));

                        config = new GuildConfig(guild.Id, language: languageCode);
                        Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, config);
                    }
                }
                if (!IsDebugMode)
                {
                    await UpdateBotListStatsAsync();
                }
            }
        }

        private async Task LeftGuild(SocketGuild guild)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LeftGuild", $"Bot has left the guild \"{guild.Name}\" ({guild.Id})"));
            var config = Database.FindDocument<GuildConfig>(Constants.GuildConfigCollection, x => x.Id == guild.Id);
            if (config != null && !config.IsBlacklisted)
            {
                Database.DeleteDocument(Constants.GuildConfigCollection, config);
                GuildUtils.PrefixCache.TryRemove(guild.Id, out _);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LeftGuild", $"Deleted config of guild {guild.Id}"));

                if (!IsDebugMode)
                {
                    await UpdateBotListStatsAsync();
                }
            }
        }

        private async Task UpdateBotListStatsAsync()
        {
            try
            {
                if (_dblApi != null)
                {
                    _dblBot ??= await _dblApi.GetMeAsync();
                    await _dblBot.UpdateStatsAsync(_client.Guilds.Count);
                }
                if (_discordBots != null)
                {
                    await _discordBots.UpdateStatsAsync(_client.CurrentUser.Id, _client.Guilds.Count);
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Stats", "Could not update the DBL/DiscordBots bot stats", e));
            }
        }
    }
}