using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun
{
    public static class Constants
    {
        public static string Version { get; } = "1.4.8";

        public static IReadOnlyList<string> PreviousVersions { get; } = new List<string>
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
            "1.4"
        };

        public const double GlobalRatelimitPeriod = 10.0 / 60.0; // 1/6 of a minute or 10 seconds

        public static DiscordSocketConfig ClientConfig { get; } = new DiscordSocketConfig
        {
            MessageCacheSize = 100,
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
        };

        public static CommandServiceConfig CommandServiceConfig { get; } = new CommandServiceConfig
        {
            LogLevel = LogSeverity.Verbose,
            CaseSensitiveCommands = false,
            IgnoreExtraArgs = true
        };

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

            // Commands that uses the emojis below
            GuildPermission.UseExternalEmojis |

            // Some utility commands
            GuildPermission.AttachFiles |

            // Music commands
            GuildPermission.Connect |
            GuildPermission.Speak;

        public const ChannelPermission MinimunRequiredPermissions = ChannelPermission.SendMessages | ChannelPermission.EmbedLinks;

        public const string GitHubRepository = "https://github.com/d4n3436/Fergun";

        public const string DevelopmentModuleName = "Dev";

        public const string DatabaseCredentialsFile = "dbcred.json";

        public const string FergunDatabase = "FergunDB";

        /// <summary>
        /// Default attachment size limit in bytes.
        /// </summary>
        public const int AttachmentSizeLimit = 8 * 1024 * 1024;

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

        public const uint DefaultEmbedColor = 16750877;

        // Command config defaults
        public const bool CaptionbotAutoTranslateDefault = true;

        public const bool AidAutoTranslateDefault = false;

        public const bool TrackSelectionDefault = false;

        public static string LoadingEmote { get; set; } = "<a:loading:721975158826598522>";
        public static string OnlineEmote { get; set; } = "<:online:726601254016647241>";
        public static string IdleEmote { get; set; } = "<:idle:726601265563566111>";
        public static string DndEmote { get; set; } = "<:dnd:726601274434519090>";
        public static string StreamingEmote { get; set; } = "<:streaming:728358352333045832>";
        public static string OfflineEmote { get; set; } = "<:invisible:726601281455783946>";
        public static string TextEmote { get; set; } = "<:text:728358376278458368>";
        public static string VoiceEmote { get; set; } = "<:voice:728358400316145755>";
        public static string MongoDbEmote { get; set; } = "<:mongodb:728358607195996271>";
        public static string WebSocketEmote { get; set; } = "<:websocket:736733297232838736>";
    }
}