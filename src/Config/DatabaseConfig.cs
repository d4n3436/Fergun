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
        public static IDictionary<string, int> CommandStats => GetConfig().CommandStats ?? new Dictionary<string, int>();

        /// <summary>
        /// Gets a dictionary containing the commands that have been disabled globally.
        /// </summary>
        public static IDictionary<string, string> GloballyDisabledCommands => GetConfig().GloballyDisabledCommands ?? new Dictionary<string, string>();

        /// <summary>
        /// Modifies the database config with the specified properties.
        /// </summary>
        /// <param name="action">A delegate containing the properties to modify the config with.</param>
        public static void Update(Action<BaseDatabaseConfig> action)
        {
            var cfg = GetConfig();
            action(cfg);
            FergunClient.Database.InsertOrUpdateDocument(Constants.ConfigCollection, cfg);
        }

        private static BaseDatabaseConfig GetConfig()
        {
            return FergunClient.Database.GetFirstDocument<BaseDatabaseConfig>(Constants.ConfigCollection) ?? new BaseDatabaseConfig();
        }
    }

    [BsonIgnoreExtraElements]
    public class BaseDatabaseConfig : IIdentity
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }
        public string GlobalPrefix { get; set; } = Constants.DefaultPrefix;
        public string DevGlobalPrefix { get; set; } = Constants.DefaultDevPrefix;
        public string Language { get; set; }
        public IDictionary<string, int> CommandStats { get; set; }
        public IDictionary<string, string> GloballyDisabledCommands { get; set; }
    }
}