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

        public const string Changelog = @"**v1.9**
Additions:
- Added Yandex OCR as a fallback OCR API.

Changes:
- Fixed a bug in AI Dungeon module that caused an exception when attempting to get the error message.
- Fixed a bug that caused an exception when modifying a command message in some cases.
- Fixed a bug in play that caused an exception due to a bug in the music library.
- Fixed a bug in `translate`, `badtranslator` and `ocrtranslate` that caused the translated text to not be complete in some cases.
- Updated `badtranslator` to be more diverse and use all 4 available translation services.
- Updated `new` and `alter` to use modals (forms).
- Reduced the cooldown of `dump`.";

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