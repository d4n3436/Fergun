using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
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
        public static FergunDB Database { get; private set; }
        public static DateTime Uptime { get; private set; }
        public static bool IsDebugMode { get; private set; }
        public static string DblBotPage { get; private set; }
        public static string InviteLink { get; set; }
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static ConcurrentBag<CachedMessage> MessageCache { get; } = new ConcurrentBag<CachedMessage>();
        public static Dictionary<string, CultureInfo> Locales { get; private set; } = new Dictionary<string, CultureInfo>();

        private DiscordSocketClient _client;
        private LogService _logService;
        private readonly CommandService _cmdService;
        private static IServiceProvider _services;
        private static CommandHandlingService _cmdHandlingService;
        private static ReliabilityService _reliabilityService;
        private static CommandCacheService _commandCacheService;
        private static bool _firstConnect = true;
        private static AuthDiscordBotListApi DblApi;
        private static IDblSelfBot DblBot;
        private static DiscordBotsApi _discordBots;
        private static Timer _autoClear;

        public FergunClient()
        {
            _cmdService = new CommandService(Constants.CommandServiceConfig);

            _autoClear = new Timer(OnTimerFired, null, Constants.MessageCacheClearInterval, Constants.MessageCacheClearInterval);

            foreach (string key in Constants.Languages.Keys)
            {
                Locales[key] = new CultureInfo(key);
            }

#if DEBUG
            IsDebugMode = true;
#else
            IsDebugMode = false;
#endif
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
            Console.WriteLine($"Fergun v{Constants.Version}");
            await Task.Delay(3000);

            var dbAuth = LoadDatabaseCredentials();
            Console.WriteLine("Connecting to the database...");
            Database = new FergunDB(Constants.FergunDatabase, dbAuth.ConnectionString);
            dbAuth = null;

            if (Database.IsConnected)
            {
                Console.WriteLine("Connected to the database successfully.");
            }
            else
            {
                Console.WriteLine("Could not connect to the database.");
                Console.WriteLine($"Please check the content of the database login credentials file ({Constants.DatabaseCredentialsFile}) and try again.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey(true);
                Environment.Exit(1);
            }

            if (string.IsNullOrEmpty(FergunConfig.GlobalPrefix))
            {
                Console.WriteLine("The bot prefix has not been set.");
                Console.WriteLine($"Please set the value of the field \"{(IsDebugMode ? "Dev" : "")}GlobalPrefix\", in collection \"Config\", in the database.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey(true);
                Environment.Exit(1);
            }

            GuildUtils.Initialize();

            if (FergunConfig.PresenceIntent ?? false)
            {
                Constants.ClientConfig.GatewayIntents |= GatewayIntents.GuildPresences;
            }
            if (FergunConfig.ServerMembersIntent ?? false)
            {
                Constants.ClientConfig.GatewayIntents |= GatewayIntents.GuildMembers;
            }

            // Only override the default value if the corresponding value in the database has been set.
            if (FergunConfig.AlwaysDownloadUsers != null)
            {
                Constants.ClientConfig.AlwaysDownloadUsers = FergunConfig.AlwaysDownloadUsers.Value;
            }
            if (FergunConfig.MessageCacheSize != null)
            {
                Constants.ClientConfig.MessageCacheSize = FergunConfig.MessageCacheSize.Value;
            }

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

            _logService = new LogService(_client, _cmdService);

            await UpdateLavalinkAsync();
            await StartLavalinkAsync();

            await _client.LoginAsync(TokenType.Bot, FergunConfig.Token);
            await _client.StartAsync();

            _reliabilityService = new ReliabilityService(_client, x => _ = _logService.LogAsync(x));
            _commandCacheService = new CommandCacheService(_client, Constants.MessageCacheCapacity,
                message => _ = _cmdHandlingService.HandleCommandAsync(message),
                log => _ = _logService.LogAsync(log),
                Constants.CommandCacheClearInterval, Constants.MaxCommandCacheLongevity);

            _services = SetupServices();

            _cmdHandlingService = new CommandHandlingService(_client, _cmdService, _logService, _services);
            await _cmdHandlingService.InitializeAsync();

            if (!IsDebugMode)
            {
                await _client.SetActivityAsync(new Game($"{FergunConfig.GlobalPrefix}help"));
            }

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private static MongoAuth LoadDatabaseCredentials()
        {
            string credentialsFile = Path.Combine(AppContext.BaseDirectory, Constants.DatabaseCredentialsFile);
            MongoAuth dbCredentials = null;
            bool hasCredentials = false;
            if (File.Exists(credentialsFile))
            {
                try
                {
                    dbCredentials = JsonConvert.DeserializeObject<MongoAuth>(File.ReadAllText(credentialsFile));
                    if (dbCredentials == null)
                    {
                        Console.WriteLine("Unknown error reading/deserializing the database login credentials file.");
                    }
                    else
                    {
                        Console.WriteLine("Loaded the database login credentials successfully.");
                        hasCredentials = true;
                    }
                }
                catch (IOException e)
                {
                    Console.WriteLine($"Error reading the database login credentials file. Using default credentials.\n{e}");
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"Error deserializing the database login credentials file. Using default credentials.\n{e}");
                }
            }
            else
            {
                Console.WriteLine("No database login credentials file found. Using default credentials.");
            }

            if (!hasCredentials)
            {
                dbCredentials = MongoAuth.Default;
                // Create the database login credentials file.
                try
                {
                    File.WriteAllText(credentialsFile, JsonConvert.SerializeObject(dbCredentials, Formatting.Indented));
                }
                catch (IOException) { }
            }

            return dbCredentials;
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while downloading VERSION.txt: {e.Message}\nSkipping the update..."));
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
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while reading local VERSION.txt: {e.Message}\nSkipping the update..."));
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", "There's a running instance of Lavalink (or a java app) and it's not possible to kill it since it's probably in use.\r\nSkipping the update..."));
                return;
            }

            try
            {
                File.Move(lavalinkFile, Path.ChangeExtension(lavalinkFile, ".jar.bak"), true);
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while renaming local Lavalink.jar: {e.Message}\nSkipping the update..."));
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while downloading the new dev build: {e.Message}"));
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
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "LLUpdater", $"An error occurred while updating local VERSION.txt: {e.Message}"));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LLUpdater", "Finished updating Lavalink."));
        }

        private IServiceProvider SetupServices() => new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_cmdService)
                .AddSingleton(_logService)
                .AddSingleton<InteractiveService>()
                .AddSingleton<LavaConfig>()
                .AddSingleton<LavaNode>()
                .AddSingleton<MusicService>()
                .AddSingleton(_reliabilityService)
                .AddSingleton(_commandCacheService)
                .BuildServiceProvider();

        private async Task ClientReady()
        {
            if (_firstConnect)
            {
                if (!IsDebugMode)
                {
                    InviteLink = $"https://discord.com/oauth2/authorize?client_id={_client.CurrentUser.Id}&scope=bot&permissions={(ulong)Constants.InvitePermissions}";

                    if (string.IsNullOrEmpty(FergunConfig.DblApiToken))
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Stats", $"Top.gg API token is empty or has not been established. Bot server count will not be sent to the API."));
                    }
                    else
                    {
                        DblApi = new AuthDiscordBotListApi(_client.CurrentUser.Id, FergunConfig.DblApiToken);
                        DblBotPage = $"https://top.gg/bot/{_client.CurrentUser.Id}";
                    }

                    if (string.IsNullOrEmpty(FergunConfig.DblApiToken))
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Stats", $"DiscordBots API token is empty or has not been established. Bot server count will not be sent to the API."));
                    }
                    else
                    {
                        _discordBots = new DiscordBotsApi(FergunConfig.DiscordBotsApiToken);
                    }

                    await UpdateBotListStatsAsync();
                }
                Uptime = DateTime.UtcNow;
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

        private Task MessageUpdated(Cacheable<IMessage, ulong> cachedbefore, SocketMessage after, ISocketMessageChannel channel)
        {
            _ = Task.Run(async () =>
            {
                if (after == null || after.Source != MessageSource.User || string.IsNullOrEmpty(after.Content))
                {
                    return;
                }
                IMessage before = null;
                try
                {
                    before = await cachedbefore.GetOrDownloadAsync();
                }
                catch (HttpException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "MsgUpdated", $"Could not get original message content ({cachedbefore.Id}) in channel \"{channel.Name}\" ({channel.Id}), reason: {e.Message}"));
                }
                if (before == null || string.IsNullOrEmpty(before.Content) || before.Content == after.Content)
                {
                    return;
                }

                MessageCache.Add(new CachedMessage(before, DateTimeOffset.UtcNow, SourceEvent.MessageUpdated));
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "MsgUpdated", $"Message edited in {before.Display()}: {before} -> {after}"));
            });

            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            _ = Task.Run(async () =>
            {
                IMessage message = null;
                try
                {
                    message = await cache.GetOrDownloadAsync();
                }
                catch (HttpException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "MsgDeleted", $"Could not get deleted message ({cache.Id}) in channel \"{channel.Name}\" ({channel.Id}), reason: {e.Message}"));
                }
                if (message == null || message.Source != MessageSource.User)
                {
                    return;
                }

                MessageCache.Add(new CachedMessage(message, DateTimeOffset.UtcNow, SourceEvent.MessageDeleted));
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "MsgDeleted", $"Message deleted in {message.Display()}: {(string.IsNullOrEmpty(message.Content) ? message.Attachments.FirstOrDefault()?.Url : message.Content)}"));
            });

            return Task.CompletedTask;
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
            var blacklistedGuild = Database.Find<BlacklistEntity>("Blacklist", x => x.ID == guild.Id);
            if (blacklistedGuild != null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "JoinGuild", $"Someone tried to add me to the blacklisted guild \"{guild.Name}\" ({guild.Id})"));
                await guild.LeaveAsync();
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "JoinGuild", $"Bot has joined the guild \"{guild.Name}\" ({guild.Id})"));
                if (guild.PreferredLocale != null)
                {
                    string preferredLanguage = Locales.FirstOrDefault(x => x.Value.TwoLetterISOLanguageName == guild.PreferredCulture.TwoLetterISOLanguageName).Key;
                    if (preferredLanguage != null)
                    {
                        if (preferredLanguage != FergunConfig.Language)
                        {
                            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "JoinGuild", $"A preferred language ({preferredLanguage}) was found in the guild {guild.Id}. Saving the preferred language in the database..."));

                            var config = new GuildConfig(guild.Id)
                            {
                                Language = preferredLanguage
                            };
                            Database.UpdateRecord("Guilds", config);
                        }
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
            var blacklistedGuild = Database.Find<BlacklistEntity>("Blacklist", x => x.ID == guild.Id);
            if (blacklistedGuild == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LeftGuild", $"Bot has left the guild \"{guild.Name}\" ({guild.Id})"));
                var config = Database.Find<GuildConfig>("Guilds", x => x.ID == guild.Id);
                if (config != null)
                {
                    Database.DeleteRecord("Guilds", config);
                    GuildUtils.PrefixCache.TryRemove(guild.Id, out _);
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "LeftGuild", $"Deleted config of guild {guild.Id}"));
                }
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
                if (DblApi != null)
                {
                    DblBot ??= await DblApi.GetMeAsync();
                    await DblBot.UpdateStatsAsync(_client.Guilds.Count);
                }
                if (_discordBots != null)
                {
                    await _discordBots.UpdateStatsAsync(_client.CurrentUser.Id, _client.Guilds.Count);
                }
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Stats", "Could not update the DBL/DiscordBots bot stats", e));
            }
        }
    }
}