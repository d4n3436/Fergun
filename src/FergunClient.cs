using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    public class FergunClient
    {
        public static FergunDatabase Database { get; private set; }
        public static FergunConfig Config { get; private set; }
        public static DateTimeOffset Uptime { get; private set; }
        public static bool IsDebugMode { get; private set; }
        public static string DblBotPage { get; private set; }
        public static string InviteLink { get; private set; }
        public static IReadOnlyDictionary<string, CultureInfo> Languages { get; private set; }

        private DiscordSocketClient _client;
        private LogService _logService;
        private MessageCacheService _messageCacheService;
        private CommandHandlingService _cmdHandlingService;
        private static AuthDiscordBotListApi _dblApi;
        private static IDblSelfBot _dblBot;
        private static DiscordBotsApi _discordBots;

        public FergunClient()
        {
#if DEBUG
            IsDebugMode = true;
#endif
            _logService = new LogService();
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
                if (Config != null)
                    TokenUtils.ValidateToken(TokenType.Bot, IsDebugMode ? Config.DevToken : Config.Token);
            }
            catch (ArgumentException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Config", $"Failed to validate {(IsDebugMode ? "dev " : "")}bot token", e));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Config", $"Make sure the value in key {(IsDebugMode ? "Dev" : "")}Token, in the config file ({Constants.BotConfigFile}) is valid."));

                Console.Write("Closing in 30 seconds... Press any key to exit now.");
                await ExitWithInputTimeoutAsync(30, 1);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Database", "Connecting to the database..."));
            bool isDbConnected = false;
            Exception dbException = null;
            try
            {
                Database = new FergunDatabase(Constants.FergunDatabase, Config!.DatabaseConfig.ConnectionString);
                isDbConnected = Database.IsConnected;
            }
            catch (TimeoutException e)
            {
                dbException = e;
            }

            if (isDbConnected)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Database", "Connected to the database successfully."));
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Critical, "Database", "Could not connect to the database.", dbException));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Database", "Ensure the MongoDB server you're trying to log in is running"));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Database", $"and make sure the server credentials in the config file ({Constants.BotConfigFile}) are correct."));

                Console.Write("Closing in 30 seconds... Press any key to exit now.");
                await ExitWithInputTimeoutAsync(30, 1);
            }

            GuildUtils.Initialize();

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Using presence intent: {Config!.PresenceIntent}"));
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
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Using command cache service: {Config.UseCommandCacheService}"));

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Using message cache service: {Config.UseMessageCacheService}"));

            Constants.ClientConfig.AlwaysDownloadUsers = Config.AlwaysDownloadUsers;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Always download users: {Constants.ClientConfig.AlwaysDownloadUsers}"));

            Constants.ClientConfig.MessageCacheSize = Config.UseMessageCacheService ? 0 : Config.MessageCacheSize;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Message cache size: {Config.MessageCacheSize}"));

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Bot", $"Messages to search limit: {Config.MessagesToSearchLimit}"));

            _client = new DiscordSocketClient(Constants.ClientConfig);
            _client.Ready += ClientReady;
            _client.JoinedGuild += JoinedGuild;
            _client.LeftGuild += LeftGuild;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            _client.UserJoined += UserJoined;
            _client.UserLeft += UserLeft;
            _client.UserBanned += UserBanned;
            _client.UserUnbanned += UserUnbanned;

            // LogSeverity.Debug is too verbose
            if (Config.LavaConfig.LogSeverity == LogSeverity.Debug)
            {
                Config.LavaConfig.LogSeverity = LogSeverity.Verbose;
            }

            if (Config.LavaConfig.Hostname == "127.0.0.1" || Config.LavaConfig.Hostname == "0.0.0.0" || Config.LavaConfig.Hostname == "localhost")
            {
                bool hasLavalink = File.Exists(Path.Combine(AppContext.BaseDirectory, "Lavalink", "Lavalink.jar"));
                if (hasLavalink)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", "Using local lavalink server. Updating and starting Lavalink..."));
                    await UpdateLavalinkAsync();
                    await StartLavalinkAsync();
                }
                else
                {
                    // Ignore all log messages from Victoria
                    Config.LavaConfig.LogSeverity = LogSeverity.Critical;
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", "Lavalink.jar not found."));
                }
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Lavalink", "Using remote lavalink server."));
            }

            _logService.Dispose();

            _messageCacheService = Config.UseMessageCacheService && Config.MessageCacheSize > 0
                ? new MessageCacheService(_client, Config.MessageCacheSize,
                    log => _ = _logService.LogAsync(log), Constants.MessageCacheClearInterval, Constants.MaxMessageCacheLongevity)
                : MessageCacheService.Disabled;

            var services = SetupServices();
            _logService = services.GetRequiredService<LogService>();

            _cmdHandlingService = new CommandHandlingService(_client, services.GetRequiredService<CommandService>(), _logService, services);
            await _cmdHandlingService.InitializeAsync();

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

        private static async Task StartLavalinkAsync()
        {
            var processList = Process.GetProcessesByName("java");
            if (processList.Length == 0)
            {
                string lavalinkFile = Path.Combine(AppContext.BaseDirectory, "Lavalink", "Lavalink.jar");
                if (!File.Exists(lavalinkFile)) return;

                var process = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{Path.Combine(AppContext.BaseDirectory, "Lavalink")}/Lavalink.jar\"",
                    WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "Lavalink"),
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Try to get the java exe path
                    var exePath = Environment.GetEnvironmentVariable("PATH")
                        ?.Split(Path.PathSeparator)
                        .FirstOrDefault(x => File.Exists(Path.Combine(x, "java.exe")));

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
            string remoteVersion;
            using var httpClient = new HttpClient();

            try
            {
                remoteVersion = await httpClient.GetStringAsync("https://ci.fredboat.com/repository/download/Lavalink_Build/lastSuccessful/VERSION.txt?guest=1");
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "An error occurred while downloading VERSION.txt", e));
                return;
            }

            if (File.Exists(versionFile))
            {
                string localVersion;
                try
                {
                    localVersion = File.ReadAllText(versionFile);
                }
                catch (IOException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "An error occurred while reading local VERSION.txt", e));
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "Local VERSION.txt not found or can't be read. Assuming the remote version is newer than the local..."));
            }

            var processList = Process.GetProcessesByName("java");
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "An error occurred while renaming local Lavalink.jar", e));
                return;
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "Downloading the new dev build of Lavalink..."));
            try
            {
                var response = await httpClient.GetAsync("https://ci.fredboat.com/repository/download/Lavalink_Build/lastSuccessful/Lavalink.jar?guest=1");
                await using var stream = await response.Content.ReadAsStreamAsync();
                var file = new FileInfo(lavalinkFile);
                await using var fileStream = file.OpenWrite();
                await stream.CopyToAsync(fileStream);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "An error occurred while downloading the new dev build", e));
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "An error occurred while updating local VERSION.txt", e));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "Finished updating Lavalink."));
        }

        private IServiceProvider SetupServices()
        {
            return new ServiceCollection()
                .AddSingleton(Constants.CommandServiceConfig)
                .AddSingleton(Config.LavaConfig)
                .AddSingleton(_client)
                .AddSingleton<CommandService>()
                .AddSingleton<LogService>()
                .AddSingleton<LavaNode>()
                .AddSingleton<InteractiveService>()
                .AddSingleton<MusicService>()
                .AddSingleton(_messageCacheService)
                .AddSingleton(Config.UseCommandCacheService
                    ? new CommandCacheService(_client, Constants.MessageCacheCapacity,
                    message => _ = _cmdHandlingService.HandleCommandAsync(message),
                    log => _ = _logService.LogAsync(log), Constants.CommandCacheClearInterval,
                    Constants.MaxCommandCacheLongevity, _messageCacheService)
                    : CommandCacheService.Disabled)
                .AddSingletonIf(Config.UseReliabilityService, new ReliabilityService(_client, message => _ = _logService.LogAsync(message)))
                .BuildServiceProvider();
        }

        private static IEnumerable<CultureInfo> GetAvailableCultures()
        {
            var result = new List<CultureInfo>();

            var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (var culture in cultures)
            {
                try
                {
                    if (culture.Equals(CultureInfo.InvariantCulture)) continue;
                    var rs = strings.ResourceManager.GetResourceSet(culture, true, false);
                    if (rs != null)
                    {
                        result.Add(culture);
                    }
                }
                catch (CultureNotFoundException) { }
            }
            return result;
        }

        private async Task ClientReady()
        {
            _client.Ready -= ClientReady;
            Uptime = DateTimeOffset.UtcNow;

            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Bot", $"{_client.CurrentUser.Username} is online!"));

            if (!IsDebugMode)
            {
                InviteLink = $"https://discord.com/oauth2/authorize?client_id={_client.CurrentUser.Id}&scope=bot&permissions={(ulong)Constants.InvitePermissions}";

                if (string.IsNullOrEmpty(Config.DblApiToken))
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Stats", "Top.gg API token is empty or not set. Bot server count will not be sent to the API."));
                }
                else
                {
                    _dblApi = new AuthDiscordBotListApi(_client.CurrentUser.Id, Config.DblApiToken);
                    DblBotPage = $"https://top.gg/bot/{_client.CurrentUser.Id}";
                }

                if (string.IsNullOrEmpty(Config.DiscordBotsApiToken))
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Stats", "DiscordBots API token is empty or not set. Bot server count will not be sent to the API."));
                }
                else
                {
                    _discordBots = new DiscordBotsApi(Config.DiscordBotsApiToken);
                }

                await UpdateBotListStatsAsync();
            }
        }

        private async Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> msgs, ISocketMessageChannel channel)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Debug, "MsgDeleted", $"{msgs.Count} messages deleted in {channel.Display()}"));
        }

        private async Task UserJoined(SocketGuildUser user)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Debug, "UserJoined", $"User \"{user}\" joined the guild \"{user.Guild.Name}\""));
        }

        private async Task UserLeft(SocketGuildUser user)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Debug, "UserLeft", $"User \"{user}\" left the guild \"{user.Guild.Name}\""));
        }

        private async Task UserBanned(SocketUser user, SocketGuild guild)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Debug, "UserBan", $"User \"{user}\" was banned from guild \"{guild.Name}\""));
        }

        private async Task UserUnbanned(SocketUser user, SocketGuild guild)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Debug, "UserUnban", $"User \"{user}\" was unbanned from guild \"{guild.Name}\""));
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "JoinGuild", $"Bot joined the guild \"{guild.Name}\" ({guild.Id})"));
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
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LeftGuild", $"Bot left the guild \"{guild.Name}\" ({guild.Id})"));
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
                if (_dblApi != null && _dblBot == null)
                {
                    try
                    {
                        _dblBot = await _dblApi.GetMeAsync();
                    }
                    catch (NullReferenceException)
                    {
                        _dblApi = null;
                        await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Stats", "Could not get the bot info from DBL API, make sure the bot is listed in DBL and the token is valid"));
                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Stats", "Bot server count will not be sent to DBL API."));
                    }
                }

                if (_dblBot != null)
                {
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