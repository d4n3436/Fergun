using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.CommandCache;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBotsList.Api;
using DiscordBotsList.Api.Objects;
using Fergun.APIs.DiscordBots;
using Fergun.Services;
using Microsoft.Extensions.DependencyInjection;
using Victoria;

namespace Fergun
{
    public class FergunClient
    {
        public static FergunDB Database { get; private set; }
        public static List<IMessage> DeletedMessages { get; } = new List<IMessage>();
        public static List<IMessage> EditedMessages { get; } = new List<IMessage>();
        public static DateTime Uptime { get; private set; }
        public static bool IsDebugMode { get; private set; }
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static AuthDiscordBotListApi DblApi { get; private set; }
        public static IDblSelfBot DblBot { get; private set; } = null;
        public static string DblBotPage { get; private set; }
        public static string SupportServer { get; set; } = "https://discord.gg/5w5GEKE";
        public static string InviteLink { get; set; }

        public static Dictionary<string, string> Languages { get; } = new Dictionary<string, string>
        {
            { "es", "🇪🇸" },
            { "en", "🇺🇸" },
            { "ar", "🇸🇦" },
            { "ru", "🇷🇺" },
            { "tr", "🇹🇷" }
        };

        public static Dictionary<string, CultureInfo> Locales { get; private set; } = new Dictionary<string, CultureInfo>();

        public static IReadOnlyList<string> WordList => _wordlist;

        public const string Version = "1.3";

        public static IReadOnlyList<string> PreviousVersions { get; } = new List<string>()
        { 
            "0.8",
            "0.9",
            "1.0",
            "1.1",
            "1.1.5",
            "1.2",
            "1.2.3",
            "1.2.4",
            "1.2.7",
            "1.2.9"
        };

        public const double GlobalCooldown = 10.0 / 60.0; // 1/6 of a minute or 10 seconds

        public const ulong InvitePermissions =

            // General
            (ulong)(GuildPermission.KickMembers |
            GuildPermission.ManageNicknames |
            GuildPermission.BanMembers |
            GuildPermission.ChangeNickname |
            GuildPermission.ViewChannel |

            // Text
            GuildPermission.EmbedLinks |
            GuildPermission.ReadMessageHistory |
            GuildPermission.UseExternalEmojis |
            GuildPermission.SendMessages |
            GuildPermission.ManageMessages |
            GuildPermission.AttachFiles |
            GuildPermission.AddReactions |

            // Voice
            GuildPermission.Connect |
            GuildPermission.Speak);

        public const string LoadingEmote = "<a:loading:721975158826598522>";

        private readonly DiscordSocketClient _client;
        private readonly CommandService _cmdService;
        private static IServiceProvider _services;
        private readonly LogService _logService;
        private static CommandHandlingService _cmdHandlingService;

        //private static ReliabilityService _reliabilityService;
        private static string[] _wordlist = Array.Empty<string>();

        private static string[] _dbCredentials;
        private static bool _hasCredentials;
        private static readonly object _messageLock = new object();
        private static bool _firstConnect = true;
        private static DiscordBotsApi _discordBots;

        public FergunClient()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 50,
                AlwaysDownloadUsers = false,
                ConnectionTimeout = 30000, 
                LogLevel = LogSeverity.Verbose,
                ExclusiveBulkDelete = true,
                GatewayIntents = 
                GatewayIntents.Guilds |

                // Moderation commands
                GatewayIntents.GuildBans |

                // General + Moderation commands
                GatewayIntents.GuildMessages |

                // Commands that uses paginators
                GatewayIntents.GuildMessageReactions |

                // Music commands
                GatewayIntents.GuildVoiceStates |

                // DM support
                GatewayIntents.DirectMessages |
                GatewayIntents.DirectMessageReactions

                //GatewayIntents.GuildPresences
                //GatewayIntents.GuildEmojis
                //GatewayIntents.GuildMembers
                //GuildSubscriptions = true // I need it for the user status (userinfo and spotify)
            });

            _cmdService = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Verbose,
                CaseSensitiveCommands = false,
                IgnoreExtraArgs = true
            });

            _logService = new LogService(_client, _cmdService);

#if DEBUG
            IsDebugMode = true;
#else
            IsDebugMode = false;
#endif
            string wordlistFile = $"{AppContext.BaseDirectory}/Resources/wordlist.txt";
            if (File.Exists(wordlistFile))
            {
                try
                {
                    _wordlist = File.ReadAllText(wordlistFile).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                }
                catch (IOException)
                {
                }
            }
            string credentialsFile = $"{AppContext.BaseDirectory}/Resources/dbcred.txt";
            if (File.Exists(credentialsFile))
            {
                try
                {
                    _dbCredentials = File.ReadAllText(credentialsFile).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    _hasCredentials = true;
                }
                catch (IOException)
                {
                    _hasCredentials = false;
                }
            }
            else
            {
                _hasCredentials = false;
            }

            foreach (string key in Languages.Keys)
            {
                Locales[key] = new CultureInfo(key);
            }
        }

        public async Task InitializeAsync()
        {
            if (IsLinux)
            {
                if (_hasCredentials)
                {
                    Database = new FergunDB("FergunDB", _dbCredentials[0], _dbCredentials[1]);
                }
                else
                {
                    Console.Write("Enter DB User: ");
                    string user = Console.ReadLine();
                    Console.Write("Enter DB Password: ");
                    string password = ReadPassword();
                    Console.Write("Enter DB host (leave empty for local): ");
                    string host = Console.ReadLine();
                    Database = new FergunDB("FergunDB", user, password, host);
                }
            }
            else
            {
                Database = new FergunDB("FergunDB");
            }

            if (Database.IsConnected)
            {
                Console.WriteLine("Connected to database.");
            }
            else
            {
                Console.WriteLine("Could not connect to the database.");
            }
            //Guilds = DB.LoadRecords<Guild>("Guilds");
            //if (IsLinux)
            //{
            //    await UpdateLavalink();
            //}
            await StartLavalinkAsync();

            await _client.LoginAsync(TokenType.Bot, FergunConfig.Token);
            await _client.StartAsync();
            _client.Ready += ClientReady;
            _client.JoinedGuild += JoinedGuild;
            _client.LeftGuild += LeftGuild;
            _client.MessageUpdated += MessageUpdated;
            _client.MessageDeleted += MessageDeleted;
            //_client.MessagesBulkDeleted += MessagesBulkDeleted;
            _client.UserJoined += UserJoined;
            _client.UserLeft += UserLeft;
            _client.UserBanned += UserBanned;
            _client.UserUnbanned += UserUnbanned;
            //_client.ReactionAdded += HandleReactionAdded;
            //_client.ReactionRemoved += HandleReactionRemoved;

            _services = SetupServices();

            // Initialize the music service
            await _services.GetRequiredService<MusicService>().InitializeAsync();

            _cmdHandlingService = new CommandHandlingService(_client, _cmdService, _logService, _services);
            await _cmdHandlingService.InitializeAsync();

            //_reliabilityService = new ReliabilityService(_client, x => _ = _logService.LogAsync(x));
            if (!IsDebugMode)
            {
                await _client.SetGameAsync($"{FergunConfig.GlobalPrefix}help", null, ActivityType.Playing);
            }
            await _client.SetStatusAsync(UserStatus.Online);

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private static async Task StartLavalinkAsync()
        {
            Process[] processList = Process.GetProcessesByName("java");
            if (processList.Length == 0)
            {
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

        private static async Task UpdateLavalinkAsync()
        {
            string projectDir;
            if (IsLinux)
            {
                projectDir = Environment.CurrentDirectory;
            }
            else
            {
                projectDir = new DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;
            }
            string projectLavalinkDir = Path.Combine(projectDir, "Lavalink");
            string buildLavalinkDir = Path.Combine(AppContext.BaseDirectory, "Lavalink");
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
                Console.WriteLine($"An error occurred while downloading VERSION.txt: {e.Message}\nSkipping the update...");
                return;
            }
            if (File.Exists($"{buildLavalinkDir}/VERSION.txt"))
            {
                string localVersion;
                try
                {
                    localVersion = File.ReadAllText($"{buildLavalinkDir}/VERSION.txt");
                }
                catch (IOException e)
                {
                    Console.WriteLine($"An error occurred while reading VERSION.txt: {e.Message}\nSkipping the update...");
                    return;
                }
                if (localVersion != remoteVersion)
                {
                    Console.WriteLine("A new dev build of Lavalink was found.");
                    Process[] processList = Process.GetProcessesByName("java");
                    if (processList.Length != 0)
                    {
                        Console.WriteLine($"There's an instance of Lavalink (or a java app) and it's not possible to kill it since it's probably in use.\nSkipping the update...");
                        return;
                    }
                    //foreach (var process in processList)
                    //{
                    //    process.Kill();
                    //}
                    try
                    {
                        File.Delete($"{buildLavalinkDir}/Lavalink.jar");
                        File.Delete($"{projectLavalinkDir}/Lavalink.jar");
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine($"An error occurred while deleting the old builds: {e.Message}\nSkipping the update...");
                        return;
                    }
                    Console.WriteLine("Downloading the new dev build of Lavalink...");
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            await wc.DownloadFileTaskAsync("https://ci.fredboat.com/repository/download/Lavalink_Build/lastSuccessful/Lavalink.jar?guest=1", $"{buildLavalinkDir}/Lavalink.jar");
                        }
                    }
                    catch (WebException e)
                    {
                        Console.WriteLine($"An error occurred while downloading the new dev build: {e.Message}\nSkipping the update...");
                        return;
                    }
                    File.Copy($"{buildLavalinkDir}/Lavalink.jar", $"{projectLavalinkDir}/Lavalink.jar");
                    Console.WriteLine("Finished updating Lavalink.");
                }
                else
                {
                    Console.WriteLine("Lavalink is up to date.");
                }
            }
            else
            {
                Console.WriteLine("VERSION.txt not found. Not possible to compare the local build with the remote one.\nSkipping the update...");
            }
            File.WriteAllText($"{projectLavalinkDir}/VERSION.txt", remoteVersion);
            File.WriteAllText($"{buildLavalinkDir}/VERSION.txt", remoteVersion);
        }

        private IServiceProvider SetupServices()
        {
            return new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_cmdService)
                .AddSingleton(_logService)
                .AddSingleton<InteractiveService>()
                /*
                .AddSingleton<IDiscordClientWrapper, DiscordClientWrapper>()
                .AddSingleton<IAudioService, LavalinkNode>()
                .AddSingleton<LavalinkNode>()
                */
                //.AddSingleton<LavaRestClient>()
                //.AddSingleton<LavaSocketClient>()
                .AddSingleton<LavaConfig>()
                .AddSingleton<LavaNode>()
                .AddSingleton<MusicService>()
                .AddSingleton(new ReliabilityService(_client, x => _ = _logService.LogAsync(x)))
                .AddSingleton(new CommandCacheService(_client, CommandCacheService.UNLIMITED,
                message => _ = _cmdHandlingService.HandleCommandAsync(message), log => _ = _logService.LogAsync(log), 14400000, 4))
                .BuildServiceProvider();
        }

        private static string ReadPassword()
        {
            string password = string.Empty;
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    return password;
                }
                password += keyInfo.KeyChar;
            }
        }

        private async Task ClientReady()
        {
            if (_firstConnect)
            {
                if (!IsDebugMode)
                {
                    InviteLink = $"https://discord.com/oauth2/authorize?client_id={_client.CurrentUser.Id}&scope=bot&permissions={InvitePermissions}";

                    DblBotPage = $"https://top.gg/bot/{_client.CurrentUser.Id}";

                    DblApi = new AuthDiscordBotListApi(_client.CurrentUser.Id, FergunConfig.DblApiToken);
                    _discordBots = new DiscordBotsApi(FergunConfig.DiscordBotsApiToken);

                    await TryUpdateBotListStatsAsync();
                }
                Uptime = DateTime.UtcNow;
                _firstConnect = false;
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Bot", $"{_client.CurrentUser.Username} is online!"));
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> cachedbefore, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after == null || after.Author.IsBot || after.Content == null)
            {
                return;
            }

            var before = await cachedbefore.GetOrDownloadAsync();
            if (before == null || before.Content == null || before.Content == after.Content)
            {
                return;
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"Message edited in " + (channel is IGuildChannel ? $"{(channel as IGuildChannel).Guild.Name}/" : "") + $"{channel.Name}/{before.Author}: {before} -> {after}"));
            _ = MessageUpdatedHandler(before);
        }

        private async Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            var message = await cache.GetOrDownloadAsync();
            if (message == null || message.Author.IsBot || string.IsNullOrEmpty(message.Content))
            {
                return;
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"Message deleted in " + (channel is IGuildChannel ? $"{(channel as IGuildChannel).Guild.Name}/" : "") + $"{channel.Name}/{message.Author}: {message.Content}"));
            _ = MessageDeletedHandler(message);
        }

        //private async Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> msgs, ISocketMessageChannel channel)
        //{
        //    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"{msgs.Count} messages deleted in " + (channel is IGuildChannel ? $"{(channel as IGuildChannel).Guild.Name}/" : "") + $"{channel.Name}"));
        //}

        private async Task UserJoined(SocketGuildUser user)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"User \"{user}\" joined the guild \"{user.Guild.Name}\""));
        }

        private async Task UserLeft(SocketGuildUser user)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"User \"{user}\" left the guild \"{user.Guild.Name}\""));
        }

        private async Task UserBanned(SocketUser user, SocketGuild guild)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"User \"{user}\" was banned from guild \"{guild.Name}\""));
        }

        private async Task UserUnbanned(SocketUser user, SocketGuild guild)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"User \"{user}\" was unbanned from guild \"{guild.Name}\""));
        }

        private static async Task MessageUpdatedHandler(IMessage message)
        {
            lock (_messageLock)
            {
                EditedMessages.Add(message);
            }
            await Task.Delay(TimeSpan.FromMinutes(10));
            lock (_messageLock)
            {
                EditedMessages.Remove(message);
            }
        }

        private static async Task MessageDeletedHandler(IMessage message)
        {
            lock (_messageLock)
            {
                DeletedMessages.Add(message);
            }
            await Task.Delay(TimeSpan.FromMinutes(10));
            lock (_messageLock)
            {
                DeletedMessages.Remove(message);
            }
        }

        private async Task JoinedGuild(SocketGuild guild)
        {
            var blacklistedGuild = Database.Find<BlacklistEntity>("Blacklist", x => x.ID == guild.Id);
            if (blacklistedGuild != null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"Someone tried to add me to the blacklisted guild \"{guild.Name}\" ({guild.Id})"));
                await guild.LeaveAsync();
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"Bot has joined the guild \"{guild.Name}\" ({guild.Id})"));
                if (!IsDebugMode)
                {
                    await TryUpdateBotListStatsAsync();
                }
            }
        }

        private async Task LeftGuild(SocketGuild guild)
        {
            var blacklistedGuild = Database.Find<BlacklistEntity>("Blacklist", x => x.ID == guild.Id);
            if (blacklistedGuild == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"Bot has left the guild \"{guild.Name}\" ({guild.Id})"));
                if (!IsDebugMode)
                {
                    await TryUpdateBotListStatsAsync();
                }
            }
        }

        private async Task TryUpdateBotListStatsAsync()
        {
            try
            {
                DblBot ??= await DblApi.GetMeAsync();
                await DblBot.UpdateStatsAsync(_client.Guilds.Count);
                await _discordBots.UpdateStatsAsync(_client.CurrentUser.Id, _client.Guilds.Count);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Stats", "Could not update the DBL/DiscordBots bot stats.", e));
            }
        }

        //private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channel, SocketReaction reaction)
        //{
        //    var msg = await cache.GetOrDownloadAsync();
        //    if (msg == null || !reaction.User.IsSpecified || reaction.User.Value.IsBot)
        //    {
        //        return;
        //    }
        //    IUser user = reaction.User.Value;
        //    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"User \"{user.Username}#{user.Discriminator}\" added a reaction \"{reaction.Emote.Name}\"" +
        //        $" to {msg.Author}'s message."));
        //}

        //private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channel, SocketReaction reaction)
        //{
        //    var msg = await cache.GetOrDownloadAsync();
        //    if (msg != null && reaction.User.IsSpecified && !reaction.User.Value.IsBot)
        //    {
        //        await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Rest", $"User \"{reaction.User.Value.Username}#{reaction.User.Value.Discriminator}\" reaction \"{reaction.Emote.Name}\"" +
        //            $" was removed from {msg.Author}'s message."));
        //    }
        //}
    }
}