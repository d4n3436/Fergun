using MongoDB.Bson;

namespace Fergun
{
    /// <summary>
    /// Represents an object that can be identified with an ObjectId.
    /// </summary>
    public interface IIdentity
    {
        /// <summary>
        /// Gets or sets the ObjectId.
        /// </summary>
        ObjectId ObjectId { get; set; }
    }
}