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

        public const string Changelog = @"**v1.8**
Additions:
- Added GScraper, a scraping library for Google Images, DuckDuckGo and Brave.
- [img2] Added new command (DuckDuckGo image search).
- [img3] Added new command (Brave image search).
- [privacy] Added new command (privacy policy and opt out options)
- [userinfo] Added user badges.
- [youtube] Added pagination support.
- [wikipedia] Added pagination support.
- [channelinfo] Added support for thread and stage channels.
- [roleinfo] Added role icon as thumbnail.
- Added icons to image search commands.
- Added Discord timestamps to multiple commands.
- Added embeds to messages with attachments (`color`, `invert`, `screenshot`).
- Added fallback pastebin to Hastebin (Hatebin).
- Added multiple configuration options.
- Added an optimized message cache.
- Added sharding support.

Changes:
- Updated the runtime to .NET 6, with lots of performance and memory improvements.
- Replaced the included translators with GTranslate, which includes new languages and a new translator (Yandex.Translate).
- Replaced System.Drawing.Commom with ImageSharp.
- Replaced the old interactive service with Fergun.Interactive.
- Replaced OCR.Space API with Bing Visual Search internal API, which is free and doesn't require an API key.
- Rewritten the AI Dungeon API wrapper, improving the response time of AI Dungeon commands.
- [img] Use images from Google Images.
- Snipe commands can now be opt out with `privacy`.
- [wikipedia] Use the localized logo.
- [stats] Display the git commit hash and sharding info.
- [help] Ignore the command group when searching for a command.
- [avatar, userinfo] Try to use the user's banner color instead of downloading the avatar and getting the average color whenever possible.
- Improved the reusage of interactive messages.
- Improved the way the bot resolves the users from the command messages.
- Improved the handling of edited command messages with attachments.
- Updated multiple comands to benefit from interactions (buttons, select menus).
- Now image search commands use the highest safe search level on non-NSFW channels.
- Now possible edited command messages won't be processed if they are 4 hours older.
- Lots of bug fixes.
- Other minor changes.

Removals:
- Removed the command `identify`, it stopped working a long time ago.
- [config] Removed CaptionBot autotranslate option.";

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

        public const int MinCommandTime = 12;

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

        public const string BingTranslatorLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/944755269034991666/BingTranslator.png";

        public const string MicrosoftAzureLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/944745954605686864/Microsoft_Azure.png";

        public const string YandexTranslateLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/857013120358416394/YandexTranslate.png";

        public const string DuckDuckGoLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890323046286651402/unknown.png";

        public const string BraveLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890323194504937522/unknown.png";

        public const string BadTranslatorLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/944755022816763914/unknown.png";

        public const string WolframAlphaLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834461638131722/wolframalpha.png";
    }
}