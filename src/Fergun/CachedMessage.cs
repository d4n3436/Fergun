using System;
using System.Collections.Generic;
using Discord;

namespace Fergun
{
    public class CachedMessage
    {
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

        public IUser Author { get; }
        public IReadOnlyCollection<IAttachment> Attachments { get; }
        public IChannel Channel { get; }
        public string Content { get; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset CachedAt { get; }
        public SourceEvent SourceEvent { get; }
    }

    public enum SourceEvent
    {
        MessageDeleted,
        MessageUpdated
    }
}