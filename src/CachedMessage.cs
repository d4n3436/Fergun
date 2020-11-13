using System;
using System.Collections.Generic;
using Discord;

namespace Fergun
{
    /// <summary>
    /// Represents a cached message.
    /// </summary>
    public class CachedMessage : ISnowflakeEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The base message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        public CachedMessage(IMessage message, DateTimeOffset cachedAt, SourceEvent sourceEvent)
        {
            Author = message.Author;
            Attachments = message.Attachments;
            Channel = message.Channel;
            Content = message.Content;
            CreatedAt = message.CreatedAt;
            CachedAt = cachedAt;
            SourceEvent = sourceEvent;
        }

        /// <inheritdoc/>
        public ulong Id { get; }

        /// <summary>
        /// Gets the author of this message.
        /// </summary>
        public IUser Author { get; }

        /// <summary>
        /// Gets all attachments included in this message.
        /// </summary>
        public IReadOnlyCollection<IAttachment> Attachments { get; }

        /// <summary>
        /// Gets the source channel of this message.
        /// </summary>
        public IChannel Channel { get; }

        /// <summary>
        /// Gets the content of this message.
        /// </summary>
        public string Content { get; }

        /// <inheritdoc/>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets the time this message was cached.
        /// </summary>
        public DateTimeOffset CachedAt { get; }

        /// <summary>
        /// Gets the source event of this message.
        /// </summary>
        public SourceEvent SourceEvent { get; }
    }

    /// <summary>
    /// Represents the event where a message gets cached.
    /// </summary>
    public enum SourceEvent
    {
        /// <summary>
        /// The message has been deleted.
        /// </summary>
        MessageDeleted,

        /// <summary>
        /// The message has been updated (edited).
        /// </summary>
        MessageUpdated
    }
}