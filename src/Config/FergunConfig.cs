using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Discord;
using Newtonsoft.Json;
using Victoria;

namespace Fergun
{
    /// <summary>
    /// Represents the bot configuration.
    /// </summary>
    public class FergunConfig
    {
        /// <summary>
        /// Gets the bot token.
        /// </summary>
        [JsonProperty]
        public string Token { get; private set; }

        /// <summary>
        /// Gets the development bot token.
        /// </summary>
        /// <remarks>
        /// This token will be automatically used in debug builds.
        /// </remarks>
        [JsonProperty]
        public string DevToken { get; private set; }

        /// <summary>
        /// Gets the Discord Bot List API token.
        /// </summary>
        [JsonProperty]
        public string DblApiToken { get; private set; }

        /// <summary>
        /// Gets the Discord Bots API token.
        /// </summary>
        [JsonProperty]
        public string DiscordBotsApiToken { get; private set; }

        /// <summary>
        /// Gets the Genius API token.
        /// </summary>
        [JsonProperty]
        public string GeniusApiToken { get; private set; }

        /// <summary>
        /// Gets the AI Dungeon user token.
        /// </summary>
        [JsonProperty]
        public string AiDungeonToken { get; private set; }

        /// <summary>
        /// Gets the DeepAI API key.
        /// </summary>
        [JsonProperty]
        public string DeepAiApiKey { get; private set; }

        /// <summary>
        /// Gets the ApiFlash access key.
        /// </summary>
        [JsonProperty]
        public string ApiFlashAccessKey { get; private set; }

        /// <summary>
        /// Gets the OCR.Space API key.
        /// </summary>
        [JsonProperty]
        public string OCRSpaceApiKey { get; private set; }

        /// <summary>
        /// Gets the WolframAlpha App ID.
        /// </summary>
        [JsonProperty]
        public string WolframAlphaAppId { get; private set; }

        /// <summary>
        /// Gets the raw value of the color the bot will use in its embeds.
        /// </summary>
        [JsonProperty]
        public uint EmbedColor { get; private set; } = Constants.DefaultEmbedColor;

        /// <summary>
        /// Gets the support server invite.
        /// </summary>
        [JsonProperty]
        public string SupportServer { get; private set; }

        /// <summary>
        /// Gets the ID of the log channel.
        /// </summary>
        [JsonProperty]
        public ulong LogChannel { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="GatewayIntents.GuildPresences"/> intent should be used.
        /// </summary>
        [JsonProperty]
        public bool PresenceIntent { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="GatewayIntents.GuildMembers"/> intent should be used.
        /// </summary>
        [JsonProperty]
        public bool ServerMembersIntent { get; private set; }

        /// <summary>
        /// Gets the message cache size.
        /// </summary>
        [JsonProperty]
        public int MessageCacheSize { get; private set; } = 100;

        /// <summary>
        /// Gets the number of messages to search in a channel.
        /// </summary>
        /// <remarks>
        /// This property is used in commands that searches for a Url in the messages of a channel.
        /// </remarks>
        [JsonProperty]
        public int MessagesToSearchLimit { get; private set; } = 100;

        /// <summary>
        /// Gets whether all users should be downloaded to the cache.
        /// </summary>
        [JsonProperty]
        public bool AlwaysDownloadUsers { get; private set; }

        /// <summary>
        /// Gets whether the reliability service should be used.
        /// </summary>
        [JsonProperty]
        public bool UseReliabilityService { get; private set; }

        /// <summary>
        /// Gets whether the command cache service should be used.
        /// </summary>
        [JsonProperty]
        public bool UseCommandCacheService { get; private set; } = true;

        /// <summary>
        /// Gets the MongoDB server authentication info.
        /// </summary>
        [JsonProperty]
        public MongoConfig DatabaseConfig { get; private set; } = MongoConfig.Default;

        /// <summary>
        /// Gets the Lavalink server configuration.
        /// </summary>
        [JsonProperty]
        public LavaConfig LavaConfig { get; private set; } = new LavaConfig();

        /// <summary>
        /// Gets the loading emote.
        /// </summary>
        [JsonProperty]
        public string LoadingEmote { get; private set; }

        /// <summary>
        /// Gets the online emote.
        /// </summary>
        [JsonProperty]
        public string OnlineEmote { get; private set; }

        /// <summary>
        /// Gets  the idle emote.
        /// </summary>
        [JsonProperty]
        public string IdleEmote { get; private set; }

        /// <summary>
        /// Gets the do not disturb emote.
        /// </summary>
        [JsonProperty]
        public string DndEmote { get; private set; }

        /// <summary>
        /// Gets the streaming emote.
        /// </summary>
        [JsonProperty]
        public string StreamingEmote { get; private set; }

        /// <summary>
        /// Gets the offline emote.
        /// </summary>
        [JsonProperty]
        public string OfflineEmote { get; private set; }

        /// <summary>
        /// Gets the text channel emote.
        /// </summary>
        [JsonProperty]
        public string TextEmote { get; private set; }

        /// <summary>
        /// Gets the voice channel emote.
        /// </summary>
        [JsonProperty]
        public string VoiceEmote { get; private set; }

        /// <summary>
        /// Gets the MongoDB emote.
        /// </summary>
        [JsonProperty]
        public string MongoDbEmote { get; private set; }

        /// <summary>
        /// Gets the websocket emote.
        /// </summary>
        [JsonProperty]
        public string WebSocketEmote { get; private set; }

        /// <summary>
        /// Gets the Nitro booster emote.
        /// </summary>
        [JsonProperty]
        public string BoosterEmote { get; internal set; }

        /// <summary>
        /// Gets user flags emotes.
        /// </summary>
        [JsonProperty]
        public IReadOnlyDictionary<string, string> UserFlagsEmotes { get; private set; }
            = new ReadOnlyDictionary<string, string>(Enum.GetNames(typeof(UserProperties)).ToDictionary(x => x, x => (string)null));
    }
}