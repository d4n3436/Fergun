using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Fergun
{
    /// <summary>
    /// Represents the bot configuration in the database.
    /// </summary>
    public static class DatabaseConfig
    {
        /// <summary>
        /// Gets the bot global prefix.
        /// </summary>
        public static string GlobalPrefix => FergunClient.IsDebugMode ? GetConfig().DevGlobalPrefix : GetConfig().GlobalPrefix;

        /// <summary>
        /// Gets the bot language.
        /// </summary>
        public static string Language => GetConfig().Language;

        /// <summary>
        /// Gets a dictionary containing the command stats.
        /// </summary>
        public static IDictionary<string, int> CommandStats => GetConfig().CommandStats;

        /// <summary>
        /// Gets a dictionary containing the commands that have been disabled globally.
        /// </summary>
        public static IDictionary<string, string> GloballyDisabledCommands => GetConfig().GloballyDisabledCommands;

        /// <summary>
        /// Modifies the database config with the specified properties.
        /// </summary>
        /// <param name="action">A delegate containing the properties to modify the confg with.</param>
        public static void Update(Action<BaseDatabaseConfig> action)
        {
            var cfg = GetConfig();
            action(cfg);
            FergunClient.Database.InsertOrUpdateDocument(Constants.ConfigCollection, cfg);
        }

        private static BaseDatabaseConfig GetConfig()
        {
            return FergunClient.Database.GetSingleDocument<BaseDatabaseConfig>(Constants.ConfigCollection);
        }
    }

    [BsonIgnoreExtraElements]
    public class BaseDatabaseConfig : IIdentity
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }
        public string GlobalPrefix { get; set; }
        public string DevGlobalPrefix { get; set; }
        public string Language { get; set; }
        public IDictionary<string, int> CommandStats { get; set; } = new Dictionary<string, int>();
        public IDictionary<string, string> GloballyDisabledCommands { get; set; } = new Dictionary<string, string>();
    }
}