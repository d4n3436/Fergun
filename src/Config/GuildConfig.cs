using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Fergun
{
    /// <summary>
    /// Represents a Discord server configuration in the database.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class GuildConfig : IBlacklistEntity, IIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GuildConfig"/> class with the provided Id.
        /// </summary>
        /// <param name="id">The server Id.</param>
        public GuildConfig(ulong id)
        {
            Id = id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuildConfig"/> class with the provided values.
        /// </summary>
        /// <param name="id">The server Id.</param>
        /// <param name="isBlacklisted">Whether the server is blacklisted.</param>
        /// <param name="blacklistReason">The reason of the blacklist.</param>
        /// <param name="prefix">The prefix of the server.</param>
        /// <param name="language">The language of the server.</param>
        /// <param name="disabledCommands">A list of disabled commands for the server.</param>
        /// <param name="captionbotAutoTranslate">Whether the CaptionBot result should be translated to the server's language.</param>
        /// <param name="aidAutoTranslate">Whether the response of the AI Dungeon API should be translated to the server's language.</param>
        /// <param name="trackSelection">Whether the track selection message should be sent instead of playing the first result automatically in the server.</param>
        public GuildConfig(ulong id, bool isBlacklisted = false, string blacklistReason = null,
            string prefix = null, string language = null, IList<string> disabledCommands = null,
            bool captionbotAutoTranslate = false, bool aidAutoTranslate = false, bool trackSelection = false)
            : this(id)
        {
            IsBlacklisted = isBlacklisted;
            BlacklistReason = blacklistReason;
            Prefix = prefix;
            Language = language;
            DisabledCommands = disabledCommands;
            CaptionbotAutoTranslate = captionbotAutoTranslate;
            AidAutoTranslate = aidAutoTranslate;
            TrackSelection = trackSelection;
        }

        /// <inheritdoc/>
        [BsonId]
        public ObjectId ObjectId { get; set; }

        /// <inheritdoc/>
        public ulong Id { get; set; }

        /// <inheritdoc/>
        public bool IsBlacklisted { get; set; }

        /// <inheritdoc/>
        public string BlacklistReason { get; set; }

        /// <summary>
        /// Gets or sets the prefix of this server.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// Gets or sets the language of this server.
        /// </summary>
        public string Language { get; set; } = DatabaseConfig.Language ?? Constants.DefaultLanguage;

        /// <summary>
        /// Gets or sets a collection of disabled commands for this server.
        /// </summary>
        public ICollection<string> DisabledCommands { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether the CaptionBot result should be translated to this server's language.
        /// </summary>
        public bool CaptionbotAutoTranslate { get; set; } = Constants.CaptionbotAutoTranslateDefault;

        /// <summary>
        /// Gets or sets whether the response of the AI Dungeon API should be translated to this server's language.
        /// </summary>
        public bool AidAutoTranslate { get; set; } = Constants.AidAutoTranslateDefault;

        /// <summary>
        /// Gets or sets whether the track selection message should be sent instead of playing the first result automatically in this server.
        /// </summary>
        public bool TrackSelection { get; set; } = Constants.TrackSelectionDefault;
    }
}