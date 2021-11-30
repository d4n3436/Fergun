using System;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Attributes;

namespace Fergun
{
    public static class Constants
    {
        public static string Version { get; } = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;

        public const string Changelog = @"**v1.6**
- Fergun is now open source!
- New commands: `wolframalpha`, `spotify`, `define`, `archive`, `shorten`, and `blacklistserver`.
- Added back `youtube`.
- Lots of internal changes to make the bot easy to self-host.

- Added a prefix cache system. Now the response times should be faster.
- [help] Added a command list cache.
- [stats] Improved the methods to get the bot memory usage.
- [repeat] Improved the performance, limiting the maximum possible length of text to avoid allocating memory unnecessarily.
- Improved the performance of the logging and interactive service.
- [cmdstats] Reduced the number of commands displayed per page.
- [calc] Now using a different math library.
- [urban] Now can be used in non-NSFW channels.
- [code] Rewrote to get a link pointing to the specified command method line from the repository.

- [userinfo] Fixed a bug that caused the activity field to be empty if the user had an emote in their status.
- [lyrics] Fixed a bug in the parser that caused bold text to be overlapped.
- [give] Fixed a bug that allowed to give IDs to bots.
- Fixed some bugs in the Bing Translator wrapper.

- [new] Removed the character limit in the custom mode creation.
- [alter] Removed the character limit.
- Removed the paginator/video cache.
- Removed the word list that `badtranslator` used.
- Removed `nothing` and `botcolor`.

- Now Fergun will send a warning message when a command fails due to a 5xx error.
- Now Fergun will send a warning message when a music command is used when there's no connection to a Lavalink server.
- Now when editing a command message with a paginator, the response message will be deleted if the reactions cannot be removed.
- Now when editing a command message to a command that attaches files, the ""Loading"" message will be correctly deleted.
- Lots of minor bug fixes.";

        public static string GitHash { get; } = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<GitHashInfoAttribute>()?
            .GitHash;

        public static DiscordSocketConfig ClientConfig { get; } = new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Verbose,
            UseSystemClock = false,
            GatewayIntents =
            GatewayIntents.Guilds |

            // Moderation commands
            GatewayIntents.GuildBans |

            // General + Moderation commands
            GatewayIntents.GuildMessages |

            // Music commands
            GatewayIntents.GuildVoiceStates |

            // DM support
            GatewayIntents.DirectMessages
        };

        public static CommandServiceConfig CommandServiceConfig { get; } = new CommandServiceConfig
        {
            LogLevel = LogSeverity.Verbose,
            CaseSensitiveCommands = false,
            IgnoreExtraArgs = true
        };

        public static TimeSpan HttpClientTimeout => TimeSpan.FromSeconds(60);

        public static TimeSpan PaginatorTimeout => TimeSpan.FromMinutes(10);

        public const GuildPermission InvitePermissions =
            GuildPermission.ViewChannel |
            GuildPermission.SendMessages |
            GuildPermission.EmbedLinks |
            GuildPermission.ReadMessageHistory |

            // Moderation commands
            GuildPermission.KickMembers |
            GuildPermission.BanMembers |
            GuildPermission.ChangeNickname |
            GuildPermission.ManageNicknames |

            // Paginator + Moderation commands
            GuildPermission.AddReactions |
            GuildPermission.ManageMessages |

            // Commands that uses external emojis
            GuildPermission.UseExternalEmojis |

            // Some utility commands
            GuildPermission.AttachFiles |

            // Music commands
            GuildPermission.Connect |
            GuildPermission.Speak;

        public const ChannelPermission MinimumRequiredPermissions = ChannelPermission.SendMessages | ChannelPermission.EmbedLinks;

        public const string GitHubRepository = "https://github.com/d4n3436/Fergun";

        public const string DevelopmentModuleName = "Dev";

        public const string BotConfigFile = "botconfig.json";

        public const string FergunDatabase = "FergunDB";

        public const string ConfigCollection = "Config";

        public const string GuildConfigCollection = "GuildConfig";

        public const string UserConfigCollection = "UserConfig";

        public const string AidAdventuresCollection = "AIDAdventures";

        public const int AttachmentSizeLimit = 8 * 1024 * 1024;

        public const double GlobalRatelimitPeriod = 10.0 / 60.0; // 1/6 of a minute or 10 seconds

        public const int GlobalCommandUsesPerPeriod = 3;

        public const double DefaultIgnoreTime = 0.6;

        public const double MentionIgnoreTime = 1;

        public const double CooldownIgnoreTime = 4;

        public const double BlacklistIgnoreTime = 60 * 5;

        public const uint MaxTrackLoops = 20;

        public const int CommandCacheClearInterval = 14400000;

        public const int MaxCommandCacheLongevity = 4;

        public const int MessageCacheCapacity = 200;

        public const int MessageCacheClearInterval = 3600000;

        public const int MaxMessageCacheLongevity = 6;

        public const int MaxPrefixLength = 10;

        public const string DefaultLanguage = "en";

        public const string DefaultPrefix = "f!";

        public const string DefaultDevPrefix = "f!!";

        public const uint DefaultEmbedColor = 16750877;

        public const int MaxTracksToDisplay = 10;

        // Command config defaults
        public const bool AidAutoTranslateDefault = false;

        public const bool TrackSelectionDefault = false;

        // Logos
        public const string AiDungeonLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834077905059850/aidungeon.png";

        public const string SpotifyLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838833381298143334/spotify.png";

        public const string GoogleLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890326437268168704/unknown.png";

        public const string GoogleTranslateLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838833843917029446/googletranslate.png";

        public const string BingTranslatorLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/857011754667081759/BingTranslator.png";

        public const string YandexTranslateLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/857013120358416394/YandexTranslate.png";

        public const string DuckDuckGoLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890323046286651402/unknown.png";

        public const string BraveLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890323194504937522/unknown.png";

        public const string BadTranslatorLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834302049452062/badtranslator.png";

        public const string WolframAlphaLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834461638131722/wolframalpha.png";
    }
}