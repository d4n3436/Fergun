using System;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun
{
    public static class Constants
    {
        public static string Version { get; } = "1.6";

        public static IReadOnlyList<string> PreviousVersions { get; } = new[]
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
            "1.2.9",
            "1.3",
            "1.3.3",
            "1.3.6",
            "1.4",
            "1.4.8"
        };

        public static DiscordSocketConfig ClientConfig { get; } = new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Verbose,
            ExclusiveBulkDelete = true,
            UseSystemClock = false,
            GatewayIntents =
            GatewayIntents.Guilds |

            // Moderation commands
            GatewayIntents.GuildBans |

            // General + Moderation commands
            GatewayIntents.GuildMessages |

            // Paginator commands
            GatewayIntents.GuildMessageReactions |

            // Music commands
            GatewayIntents.GuildVoiceStates |

            // DM support
            GatewayIntents.DirectMessages |
            GatewayIntents.DirectMessageReactions
        };

        public static CommandServiceConfig CommandServiceConfig { get; } = new CommandServiceConfig
        {
            LogLevel = LogSeverity.Verbose,
            CaseSensitiveCommands = false,
            IgnoreExtraArgs = true
        };

        public static TimeSpan HttpClientTimeout => TimeSpan.FromSeconds(60);

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
        public const bool CaptionbotAutoTranslateDefault = true;

        public const bool AidAutoTranslateDefault = false;

        public const bool TrackSelectionDefault = false;

        // Logos
        public const string AiDungeonLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834077905059850/aidungeon.png";

        public const string SpotifyLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838833381298143334/spotify.png";

        public const string GoogleTranslateLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838833843917029446/googletranslate.png";

        public const string BadTranslatorLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834302049452062/badtranslator.png";

        public const string WikipediaLogoUrl = "https://upload.wikimedia.org/wikipedia/commons/6/63/Wikipedia-logo.png";

        public const string WolframAlphaLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834461638131722/wolframalpha.png";
    }
}