using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Fergun
{
    /// <summary>
    /// Represents an AI Dungeon adventure.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class AidAdventure : IIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AidAdventure"/> class with the provided values.
        /// </summary>
        /// <param name="id">The Id of the adventure.</param>
        /// <param name="publicId">The public Id of the adventure.</param>
        /// <param name="ownerId">The owner Id of the adventure.</param>
        /// <param name="isPublic">Whether the adventure is public.</param>
        public AidAdventure(uint id, string publicId, ulong ownerId, bool isPublic)
        {
            Id = id;
            PublicId = publicId;
            OwnerId = ownerId;
            IsPublic = isPublic;
        }

        /// <inheritdoc/>
        [BsonId]
        public ObjectId ObjectId { get; set; }

        /// <summary>
        /// Gets or sets the Id of this adventure.
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// Gets or sets the public Id of this adventure.
        /// </summary>
        public string PublicId { get; set; }

        /// <summary>
        /// Gets or sets the owner Id of this adventure.
        /// </summary>
        public ulong OwnerId { get; set; }

        /// <summary>
        /// Gets or sets whether this adventure is public.
        /// </summary>
        public bool IsPublic { get; set; }
    }
}