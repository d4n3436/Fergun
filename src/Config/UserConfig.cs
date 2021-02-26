using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Fergun
{
    /// <summary>
    /// Represents a user configuration in the database.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class UserConfig : IBlacklistEntity, IIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserConfig"/> class with the provided user Id.
        /// </summary>
        /// <param name="id">The user Id.</param>
        public UserConfig(ulong id)
        {
            Id = id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserConfig"/> class with the provided values.
        /// </summary>
        /// <param name="id">The user Id.</param>
        /// <param name="isBlacklisted">Whether the user is blacklisted.</param>
        /// <param name="blacklistReason">The reason of the blacklist.</param>
        /// <param name="triviaPoints">The Trivia points of the user.</param>
        public UserConfig(ulong id, bool isBlacklisted = false, string blacklistReason = null, int triviaPoints = 0) : this(id)
        {
            IsBlacklisted = isBlacklisted;
            BlacklistReason = blacklistReason;
            TriviaPoints = triviaPoints;
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
        /// Gets or sets the Trivia points of this user.
        /// </summary>
        public int TriviaPoints { get; set; }

        /// <summary>
        /// Gets or sets whether the user has opted out the temporary collection of deleted/edited messages in the "snipe" commands.
        /// </summary>
        public bool IsOptedOutSnipe { get; set; }
    }
}