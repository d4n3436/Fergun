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
        public string DefaultLanguage { get; set; }
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
        public uint EmbedColor { get; set; }
        public int VideoCacheSize { get; set; }
        public bool CaptionbotAutoTranslateDefault { get; set; }
        public bool AidAutoTranslateDefault { get; set; }
        public bool TrackSelectionDefault { get; set; }
        public Dictionary<string, int> CommandStats { get; set; }
    }

    public static class FergunConfig
    {
        private static bool _tokenUsed = false;
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
                cfg.GlobalPrefix = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static string DefaultLanguage
        {
            get => GetConfig().DefaultLanguage;
            set
            {
                var cfg = GetConfig();
                cfg.DefaultLanguage = value;
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
            get => GetConfig().EmbedColor;
            set
            {
                var cfg = GetConfig();
                cfg.EmbedColor = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static int VideoCacheSize
        {
            get => GetConfig().VideoCacheSize;
            set
            {
                var cfg = GetConfig();
                cfg.VideoCacheSize = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static bool CaptionbotAutoTranslateDefault
        {
            get => GetConfig().CaptionbotAutoTranslateDefault;
            set
            {
                var cfg = GetConfig();
                cfg.CaptionbotAutoTranslateDefault = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static bool AidAutoTranslateDefault
        {
            get => GetConfig().AidAutoTranslateDefault;
            set
            {
                var cfg = GetConfig();
                cfg.AidAutoTranslateDefault = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

        public static bool TrackSelectionDefault
        {
            get => GetConfig().TrackSelectionDefault;
            set
            {
                var cfg = GetConfig();
                cfg.TrackSelectionDefault = value;
                FergunClient.Database.UpdateRecord("Config", cfg);
            }
        }

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

        private static BaseConfig GetConfig()
        {
            return FergunClient.Database.LoadRecord<BaseConfig>("Config");
        }

        /*
        public string Token { get; internal set; }
        public string DevToken { get; set; }
        public string GlobalPrefix { get; set; }
        public string DevGlobalPrefix { get; set; }
        public string DefaultLanguage { get; set; }
        public string GoogleSearchApiKey { get; set; }
        public string SearchEngineId { get; set; }
        public string YtSearchApiKey { get; set; }
        public string DeepAiApiKey { get; set; }
        public string ApiFlashAccessKey { get; set; }
        public string OCRSpaceApiKey { get; set; }
        public uint EmbedColor { get; set; }
        public int VideoCacheSize { get; set; }
        public bool CaptionbotAutoTranslateDefault { get; set; }
        public bool AidAutoTranslateDefault { get; set; }
        public bool TrackSelectionDefault { get; set; }
        */
    }

    [BsonIgnoreExtraElements]
    public class Guild : IIdentity
    {
        public Guild(ulong id)
        {
            ID = id;
        }

        public Guild(ulong id, string prefix) : this(id)
        {
            Prefix = prefix;
        }

        public Guild(ulong id, string prefix, string language) : this(id, prefix)
        {
            Language = language;
        }

        public Guild(ulong id,
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
        public string Prefix { get; set; } = null;
        public string Language { get; set; } = FergunConfig.DefaultLanguage;
        public bool CaptionbotAutoTranslate { get; set; } = FergunConfig.CaptionbotAutoTranslateDefault;
        public bool AidAutoTranslate { get; set; } = FergunConfig.AidAutoTranslateDefault;
        public bool TrackSelection { get; set; } = FergunConfig.TrackSelectionDefault;
    }

    [BsonIgnoreExtraElements]
    public class AidAdventure : IIdentity
    {
        public AidAdventure(uint id, ulong ownerid, bool isPublic)
        {
            ID = id;
            OwnerID = ownerid;
            IsPublic = isPublic;
        }

        [BsonId]
        public ObjectId ObjectId { get; set; }
        public uint ID { get; set; }
        public ulong OwnerID { get; set; }
        public bool IsPublic { get; set; }
    }

    // TODO: Merge TriviaPlayer and BlacklistEntity to User

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
        public int Points { get; set; } = 0;
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
        public string Reason { get; set; } = null;
    }
}