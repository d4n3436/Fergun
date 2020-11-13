using Discord;

namespace Fergun
{
    /// <summary>
    /// Represents an entity that can be blacklisted.
    /// </summary>
    public interface IBlacklistEntity : IEntity<ulong>
    {
        /// <summary>
        /// Gets or sets whether this entity is blacklisted.
        /// </summary>
        public bool IsBlacklisted { get; set; }

        /// <summary>
        /// Gets or sets the reason of the blacklist.
        /// </summary>
        public string BlacklistReason { get; set; }
    }
}