using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Fergun
{
    public interface IIdentity
    {
        ObjectId ObjectId { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class BaseConfig : IIdentity
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }
        public string Token { get; set; }
        public string DevToken { get; set; }
        public string GlobalPrefix { get; set; }
        public string DevGlobalPrefix { get; set; }
        public string Language { get; set; }
        public string DblApiToken { get; set; }
        public string DiscordBotsApiToken { get; set; }
        public string GeniusApiToken { get; set; }
        public string AiDungeonToken { get; set; }
        public string GoogleSearchApiKey { get; set; }
        public string SearchEngineId { get; set; }
        public string YtSearchApiKey { get; set; }
        public string DeepAiApiKey { get; set; }
        public string ApiFlashAccessKey { get; set; }
        public string OCRSpaceApiKey { get; set; }
        public uint? EmbedColor { get; set; }
        public int? VideoCacheSize { get; set; }
        public Dictionary<string, int> CommandStats { get; set; }
        public Dictionary<string, string> GloballyDisabledCommands { get; set; }
        public string SupportServer { get; set; }
        public string LogChannel { get; set; }
        public bool? PresenceIntent { get; set; }
        public bool? ServerMembersIntent { get; set; }
        public int? MessageCacheSize { get; set; }
        public bool? AlwaysDownloadUsers { get; set; }
    }

    public static class FergunConfig
    {
        private static bool _tokenUsed;
        public static string Token
        {
            get
            {
                if (!_tokenUsed)
                {
                    _tokenUsed = true;
                    return FergunClient.IsDebugMode ? GetConfig().DevToken : GetConfig().Token;
                }
                return "No";
            }
        }

        public static string GlobalPrefix
        {
            get => FergunClient.IsDebugMode ? GetConfig().DevGlobalPrefix : GetConfig().GlobalPrefix;
            set
            {
                var cfg = GetConfig();
                if (FergunClient.IsDebugMode)
                {
                    cfg.DevGlobalPrefix = value;
                }
                else
                {
                    cfg.GlobalPrefix = value;
                }
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static string Language
        {
            get => GetConfig().Language;
            set
            {
                var cfg = GetConfig();
                cfg.Language = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static string DblApiToken => GetConfig().DblApiToken;

        public static string DiscordBotsApiToken => GetConfig().DiscordBotsApiToken;

        public static string GeniusApiToken => GetConfig().GeniusApiToken;

        public static string AiDungeonToken => GetConfig().AiDungeonToken;

        public static string GoogleSearchApiKey => GetConfig().GoogleSearchApiKey;

        public static string SearchEngineId => GetConfig().SearchEngineId;

        public static string YtSearchApiKey => GetConfig().YtSearchApiKey;

        public static string DeepAiApiKey => GetConfig().DeepAiApiKey;

        public static string ApiFlashAccessKey => GetConfig().ApiFlashAccessKey;

        public static string OCRSpaceApiKey => GetConfig().OCRSpaceApiKey;

        public static uint EmbedColor
        {
            get => GetConfig().EmbedColor ?? Constants.DefaultEmbedColor;
            set
            {
                var cfg = GetConfig();
                cfg.EmbedColor = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static int? VideoCacheSize => GetConfig().VideoCacheSize;

        public static Dictionary<string, int> CommandStats
        {
            get => GetConfig().CommandStats;
            set
            {
                var cfg = GetConfig();
                cfg.CommandStats = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static Dictionary<string, string> GloballyDisabledCommands
        {
            get => GetConfig().GloballyDisabledCommands ?? new Dictionary<string, string>();
            set
            {
                var cfg = GetConfig();
                cfg.GloballyDisabledCommands = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static string SupportServer => GetConfig().SupportServer;

        public static string LogChannel => GetConfig().LogChannel;

        public static bool? PresenceIntent => GetConfig().PresenceIntent;

        public static bool? ServerMembersIntent => GetConfig().ServerMembersIntent;

        public static int? MessageCacheSize => GetConfig().MessageCacheSize;

        public static bool? AlwaysDownloadUsers => GetConfig().AlwaysDownloadUsers;

        private static BaseConfig GetConfig()
        {
            return FergunClient.Database.LoadRecord<BaseConfig>("Config");
        }
    }

    [BsonIgnoreExtraElements]
    public class GuildConfig : IIdentity
    {
        public GuildConfig(ulong id)
        {
            ID = id;
        }

        public GuildConfig(ulong id, string prefix) : this(id)
        {
            Prefix = prefix;
        }

        public GuildConfig(ulong id, string prefix, string language) : this(id, prefix)
        {
            Language = language;
        }

        public GuildConfig(ulong id,
                     string prefix,
                     string language,
                     bool captionbotAutoTranslate,
                     bool aidAutoTranslate,
                     bool trackSelection) : this(id, prefix, language)
        {
            CaptionbotAutoTranslate = captionbotAutoTranslate;
            AidAutoTranslate = aidAutoTranslate;
            TrackSelection = trackSelection;
        }

        [BsonId]
        public ObjectId ObjectId { get; set; }
        public ulong ID { get; set; }
        public string Prefix { get; set; }
        public string Language { get; set; } = FergunConfig.Language;
        public List<string> DisabledCommands { get; set; } = new List<string>();
        public bool CaptionbotAutoTranslate { get; set; } = Constants.CaptionbotAutoTranslateDefault;
        public bool AidAutoTranslate { get; set; } = Constants.AidAutoTranslateDefault;
        public bool TrackSelection { get; set; } = Constants.TrackSelectionDefault;
    }

    [BsonIgnoreExtraElements]
    public class AidAdventure : IIdentity
    {
        public AidAdventure(uint id, string publicId, ulong ownerId, bool isPublic)
        {
            ID = id;
            PublicId = publicId;
            OwnerID = ownerId;
            IsPublic = isPublic;
        }

        [BsonId]
        public ObjectId ObjectId { get; set; }
        public uint ID { get; set; }
        public string PublicId { get; set; }
        public ulong OwnerID { get; set; }
        public bool IsPublic { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class TriviaPlayer : IIdentity
    {
        public TriviaPlayer(ulong id)
        {
            ID = id;
        }

        public TriviaPlayer(ulong id, int points) : this(id)
        {
            Points = points;
        }

        [BsonId]
        public ObjectId ObjectId { get; set; }
        public ulong ID { get; set; }
        public int Points { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class BlacklistEntity : IIdentity
    {
        public BlacklistEntity(ulong id)
        {
            ID = id;
        }

        public BlacklistEntity(ulong id, string reason) : this(id)
        {
            Reason = reason;
        }

        [BsonId]
        public ObjectId ObjectId { get; set; }
        public ulong ID { get; set; }
        public string Reason { get; set; }
    }
}