using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Fergun.Services
{
    /// <summary>
    /// Represents an optimized cache of sent, deleted and edited messages.
    /// </summary>
    public class MessageCacheService : IDisposable
    {
        // short term cache
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>> _cache
            = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>>();

        private readonly ConcurrentDictionary<ulong, ConcurrentQueue<ulong>> _orderedCache
            = new ConcurrentDictionary<ulong, ConcurrentQueue<ulong>>();

        // long term cache
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>> _editedDeletedCache
            = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>>();

        private readonly ConcurrentDictionary<ulong, DateTimeOffset> _lastCommandUsageTimes = new ConcurrentDictionary<ulong, DateTimeOffset>();
        private readonly bool _onlyCacheUserDeletedEditedMessages;
        private readonly Func<LogMessage, Task> _logger;
        private readonly DiscordSocketClient _client;
        private readonly double _maxMessageTime;
        private readonly int _messageCacheSize;
        private Timer _autoClear;
        private bool _disposed;

        private MessageCacheService()
        {
            IsDisabled = true;
            _disposed = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageCacheService"/> class.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="messageCacheSize">The message cache size. This only applies to sent messages and not edited/deleted messages.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="period">The period between cleanings. This only applies to edited/deleted messages.</param>
        /// <param name="maxMessageTime">The max. time the messages can be kept in the cache. This only applies to edited/deleted messages.</param>
        /// <param name="onlyCacheUserDeletedEditedMessages">Whether to only save messages from users in the edited/deleted messages cache.</param>
        public MessageCacheService(DiscordSocketClient client, int messageCacheSize, Func<LogMessage, Task> logger = null,
            int period = 3600000, double maxMessageTime = 6, bool onlyCacheUserDeletedEditedMessages = true)
        {
            _client = client;
            _client.MessageReceived += MessageReceived;
            _client.MessageDeleted += MessageDeleted;
            _client.MessageUpdated += MessageUpdated;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            _client.LeftGuild += LeftGuild;

            _logger = logger ?? (_ => Task.CompletedTask);
            _autoClear = new Timer(OnTimerFired, null, period, period);
            _messageCacheSize = messageCacheSize;
            _maxMessageTime = maxMessageTime;
            _onlyCacheUserDeletedEditedMessages = onlyCacheUserDeletedEditedMessages;
        }

        /// <summary>
        /// Returns a disabled instance of <see cref="MessageCacheService"/>.
        /// </summary>
        public static MessageCacheService Disabled => new MessageCacheService();

        /// <summary>
        /// Gets whether the cache is disabled.
        /// </summary>
        public bool IsDisabled { get; }

        /// <summary>
        /// Gets a cache of messages for a channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>A cache of messages.</returns>
        public IReadOnlyDictionary<ulong, ICachedMessage> GetCacheForChannel(IMessageChannel channel,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
            => GetCacheForChannel(channel.Id, sourceEvent);

        /// <summary>
        /// Gets a cache of messages for a channel.
        /// </summary>
        /// <param name="channelId">The channel id.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>A cache of messages.</returns>
        public IReadOnlyDictionary<ulong, ICachedMessage> GetCacheForChannel(ulong channelId,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
        {
            GetCacheForEvent(sourceEvent).TryGetValue(channelId, out var channel);

            return channel as IReadOnlyDictionary<ulong, ICachedMessage> ?? ImmutableDictionary<ulong, ICachedMessage>.Empty;
        }

        /// <summary>
        /// Clears all channels and messages from the cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _orderedCache.Clear();
            _editedDeletedCache.Clear();
            _lastCommandUsageTimes.Clear();
        }

        /// <summary>
        /// Attempts to clear all the messages from the cache in the specified channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <returns>Whether the channel has been removed from all caches.</returns>
        public bool TryClear(IMessageChannel channel) => TryClear(channel.Id);

        /// <summary>
        /// Attempts to clear all the messages from the cache in the specified channel.
        /// </summary>
        /// <param name="channelId">The channel id.</param>
        /// <returns>Whether the channel has been removed from all caches.</returns>
        public bool TryClear(ulong channelId)
            => _cache.TryRemove(channelId, out _)
               && _orderedCache.TryRemove(channelId, out _)
               && _editedDeletedCache.TryRemove(channelId, out _);

        /// <summary>
        /// Attempts to get a cached message associated to the provided id.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="messageId">The id of the cached message.</param>
        /// <param name="message">The cached message, or <c>null</c> if the message could not be found.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>Whether the message was found.</returns>
        public bool TryGetCachedMessage(IMessageChannel channel, ulong messageId, out ICachedMessage message,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
            => TryGetCachedMessage(channel.Id, messageId, out message, sourceEvent);

        /// <summary>
        /// Attempts to get a cached message associated to the provided id.
        /// </summary>
        /// <param name="channelId">The id of the channel.</param>
        /// <param name="messageId">The id of the cached message.</param>
        /// <param name="message">The cached message, or <c>null</c> if the message could not be found.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>Whether the message was found.</returns>
        public bool TryGetCachedMessage(ulong channelId, ulong messageId, out ICachedMessage message,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
        {
            message = null;
            var cache = GetCacheForEvent(sourceEvent);

            return cache.TryGetValue(channelId, out var cachedChannel) && cachedChannel.TryGetValue(messageId, out message);
        }

        /// <summary>
        /// Attempts to get a cached message associated to the provided id, searching in every channel cache.
        /// </summary>
        /// <param name="messageId">The id of the cached message.</param>
        /// <param name="message">The cached message, or <c>null</c> if the message could not be found.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>Whether the message was found.</returns>
        public bool TryGetCachedMessage(ulong messageId, out ICachedMessage message,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
        {
            message = null;
            var cache = GetCacheForEvent(sourceEvent);

            foreach (var cachedChannel in cache)
            {
                if (cachedChannel.Value.TryGetValue(messageId, out message))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to remove and return a cached message associated to the provided id.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="messageId">The id of the cached message.</param>
        /// <param name="message">The cached message, or <c>null</c> if the message could not be found.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>Whether the message was found.</returns>
        public bool TryRemoveCachedMessage(IMessageChannel channel, ulong messageId, out ICachedMessage message,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
            => TryRemoveCachedMessage(channel.Id, messageId, out message, sourceEvent);

        /// <summary>
        /// Attempts to remove and return a cached message associated to the provided id.
        /// </summary>
        /// <param name="channelId">The id of the channel.</param>
        /// <param name="messageId">The id of the cached message.</param>
        /// <param name="message">The cached message, or <c>null</c> if the message could not be found.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>Whether the message was found.</returns>
        public bool TryRemoveCachedMessage(ulong channelId, ulong messageId, out ICachedMessage message,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
        {
            message = null;
            var cache = GetCacheForEvent(sourceEvent);

            return cache.TryGetValue(channelId, out var cachedChannel) && cachedChannel.TryRemove(messageId, out message);
        }

        /// <summary>
        /// Updates the last time a command was executed in the specified guild to <see cref="DateTimeOffset.UtcNow"/>.
        /// </summary>
        /// <param name="guildId">The Id of the guild.</param>
        public void UpdateLastCommandUsageTime(ulong guildId)
            => UpdateLastCommandUsageTime(guildId, DateTimeOffset.UtcNow);

        /// <summary>
        /// Updates the last time a command was executed in the specified guild to <paramref name="dateTime"/>.
        /// </summary>
        /// <param name="guildId">The Id of the guild.</param>
        /// <param name="dateTime">The <see cref="DateTimeOffset"/> to set.</param>
        public void UpdateLastCommandUsageTime(ulong guildId, DateTimeOffset dateTime)
        {
            _lastCommandUsageTimes[guildId] = dateTime;
            Debug.WriteLine($"Updated last command usage time for guild {guildId} to {dateTime}");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>> GetCacheForEvent(MessageSourceEvent sourceEvent) =>
            sourceEvent == MessageSourceEvent.MessageReceived ? _cache : _editedDeletedCache;

        private void OnTimerFired(object state)
        {
            // This only applies to the long term cache.
            int removed = 0;

            foreach (var cachedChannel in _editedDeletedCache)
            {
                foreach (var cachedMessage in cachedChannel.Value)
                {
                    if ((DateTimeOffset.UtcNow - cachedMessage.Value.CreatedAt).TotalHours >= _maxMessageTime)
                    {
                        cachedChannel.Value.TryRemove(cachedMessage.Key, out _);
                        removed++;
                    }
                }
            }

            _ = _logger(new LogMessage(LogSeverity.Verbose, "MsgCache", $"Cleaned {removed} deleted / edited messages from the cache."));
        }

        private Task MessageReceived(SocketMessage message)
        {
            Debug.WriteLine($"Received message {message.Id}: {message.Content}");

            if (message.Channel is IGuildChannel guildChannel && _lastCommandUsageTimes.TryGetValue(guildChannel.GuildId, out var lastUsageTime))
            {
                var now = DateTimeOffset.Now;
                if (lastUsageTime > now.AddHours(-12) && lastUsageTime <= now)
                {
                    Debug.WriteLine($"Last command usage time of guild {guildChannel.GuildId} is inside the last 12 hours!");
                    var cachedChannel = _cache.GetOrAdd(message.Channel.Id,
                        new ConcurrentDictionary<ulong, ICachedMessage>(Environment.ProcessorCount, (int)(_messageCacheSize * 1.05)));

                    var channelQueue = _orderedCache.GetOrAdd(message.Channel.Id, new ConcurrentQueue<ulong>());

                    cachedChannel[message.Id] = new CachedMessage(message, message.CreatedAt, MessageSourceEvent.MessageReceived);
                    channelQueue.Enqueue(message.Id);
                    Debug.WriteLine($"Added message {message.Id} to the cache.");
                    Debug.WriteLine($"Messages in cached channel: {cachedChannel.Count}, queue: {channelQueue.Count}");

                    while (cachedChannel.Count > _messageCacheSize && channelQueue.TryDequeue(out ulong msgId))
                    {
                        Debug.WriteLine($"Removed {msgId} from the cache.");
                        cachedChannel.TryRemove(msgId, out _);
                    }
                }
            }

            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            HandleDeletedMessage(cache.Id, channel);
            return Task.CompletedTask;
        }

        private void HandleDeletedMessage(ulong messageId, IMessageChannel channel)
        {
            // The default message cache removes deleted message from its cache, here we just move the message to the long term cache
            if (TryRemoveCachedMessage(channel, messageId, out var message))
            {
                if (!_onlyCacheUserDeletedEditedMessages || message.Source == MessageSource.User)
                {
                    message.CachedAt = DateTimeOffset.UtcNow;
                    message.SourceEvent = MessageSourceEvent.MessageDeleted;

                    // move cached message to the long term cache
                    var cachedChannel = _editedDeletedCache.GetOrAdd(channel.Id, new ConcurrentDictionary<ulong, ICachedMessage>());
                    cachedChannel[messageId] = message;

                    Debug.WriteLine($"Moved cached message {messageId} to long term cache (MessageReceived -> MessageDeleted)");
                }
            }
            else if (TryGetCachedMessage(channel, messageId, out message, MessageSourceEvent.MessageUpdated))
            {
                message.CachedAt = DateTimeOffset.UtcNow;
                message.SourceEvent = MessageSourceEvent.MessageDeleted;
                Debug.WriteLine($"Changed cached message {messageId} source event (MessageUpdated -> MessageDeleted)");
            }
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> cachedbefore, SocketMessage updatedMessage, ISocketMessageChannel channel)
        {
            HandleUpdatedMessage(updatedMessage, channel);
            return Task.CompletedTask;
        }

        private void HandleUpdatedMessage(IMessage updatedMessage, IMessageChannel channel)
        {
            // We need to simulate the functionality of the default message cache,
            // so we update the message in the short term cache instead of removing it
            if (TryGetCachedMessage(channel, updatedMessage.Id, out var message))
            {
                _cache[channel.Id][updatedMessage.Id] = new CachedMessage(updatedMessage, DateTimeOffset.UtcNow, MessageSourceEvent.MessageUpdated);

                if (!_onlyCacheUserDeletedEditedMessages || message.Source == MessageSource.User)
                {
                    // "Copy" the cached message to the long term cache
                    var cachedChannel = _editedDeletedCache.GetOrAdd(channel.Id, new ConcurrentDictionary<ulong, ICachedMessage>());
                    cachedChannel[updatedMessage.Id] = new CachedMessage(updatedMessage, message, DateTimeOffset.UtcNow, MessageSourceEvent.MessageUpdated);

                    Debug.WriteLine($"Copied cached message {updatedMessage.Id} to long term cache (MessageReceived -> MessageUpdated)");
                }
            }
            else if (TryGetCachedMessage(channel, updatedMessage.Id, out message, MessageSourceEvent.MessageUpdated))
            {
                var cachedChannel = _editedDeletedCache.GetOrAdd(channel.Id, new ConcurrentDictionary<ulong, ICachedMessage>());

                message.OriginalMessage = null;
                // Create a new cached message containing the previous and current messages
                cachedChannel[updatedMessage.Id] = new CachedMessage(updatedMessage, message, DateTimeOffset.UtcNow, MessageSourceEvent.MessageUpdated);
                Debug.WriteLine($"Added original message to cached message {updatedMessage.Id} (MessageUpdated)");
            }
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> cachedMessages, ISocketMessageChannel channel)
        {
            foreach (var cached in cachedMessages)
            {
                HandleDeletedMessage(cached.Id, channel);
            }

            return Task.CompletedTask;
        }

        private async Task LeftGuild(SocketGuild guild)
        {
            int count = 0;
            foreach (var channel in guild.TextChannels)
            {
                if (TryClear(channel))
                    count++;
            }

            _lastCommandUsageTimes.TryRemove(guild.Id, out _);
            await _logger(new LogMessage(LogSeverity.Verbose, "MsgCache", $"Removed {count} cached channels from guild {guild.Id}"));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MessageCacheService), "Service has been disposed.");
            }

            if (!disposing) return;
            _autoClear.Dispose();
            _autoClear = null;

            Clear();

            _client.MessageReceived -= MessageReceived;
            _client.MessageDeleted -= MessageDeleted;
            _client.MessageUpdated -= MessageUpdated;
            _client.MessagesBulkDeleted -= MessagesBulkDeleted;
            _client.LeftGuild -= LeftGuild;

            _disposed = true;
        }
    }

    /// <summary>
    /// Represents an edited message.
    /// </summary>
    public interface IEditedMessage : IMessage
    {
        /// <summary>
        /// Gets ot sets the original message prior to being edited. This property is only present if the message has been edited once.
        /// </summary>
        public IMessage OriginalMessage { get; set; }
    }

    /// <summary>
    /// Represents a generic cached message.
    /// </summary>
    public interface ICachedMessage : IEditedMessage
    {
        /// <summary>
        /// Gets or sets when this message was cached.
        /// </summary>
        public DateTimeOffset CachedAt { get; set; }

        /// <summary>
        /// Gets or sets the source event of this message.
        /// </summary>
        public MessageSourceEvent SourceEvent { get; set; }
    }

    /// <summary>
    /// Represents a cached message.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class CachedMessage : ICachedMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        public CachedMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent)
        {
            _message = message;
            CachedAt = cachedAt;
            SourceEvent = sourceEvent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="originalMessage">The original message (prior to being edited).</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        public CachedMessage(IMessage message, IMessage originalMessage, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent)
        {
            _message = message;
            OriginalMessage = originalMessage;
            CachedAt = cachedAt;
            SourceEvent = sourceEvent;
        }

        private readonly IMessage _message;

        /// <inheritdoc/>
        public IMessage OriginalMessage { get; set; }

        /// <inheritdoc/>
        public DateTimeOffset CachedAt { get; set; }

        /// <inheritdoc/>
        public MessageSourceEvent SourceEvent { get; set; }

        /// <inheritdoc/>
        public ulong Id => _message.Id;

        /// <inheritdoc/>
        public DateTimeOffset CreatedAt => _message.CreatedAt;

        /// <inheritdoc/>
        public MessageType Type => _message.Type;

        /// <inheritdoc/>
        public MessageSource Source => _message.Source;

        /// <inheritdoc/>
        public bool IsTTS => _message.IsTTS;

        /// <inheritdoc/>
        public bool IsPinned => _message.IsPinned;

        /// <inheritdoc/>
        public bool IsSuppressed => _message.IsSuppressed;

        /// <inheritdoc/>
        public bool MentionedEveryone => _message.MentionedEveryone;

        /// <inheritdoc/>
        public string Content => _message.Content;

        /// <inheritdoc/>
        public DateTimeOffset Timestamp => _message.Timestamp;

        /// <inheritdoc/>
        public DateTimeOffset? EditedTimestamp => _message.EditedTimestamp;

        /// <inheritdoc/>
        public IMessageChannel Channel => _message.Channel;

        /// <inheritdoc/>
        public IUser Author => _message.Author;

        /// <inheritdoc/>
        public IReadOnlyCollection<IAttachment> Attachments => _message.Attachments;

        /// <inheritdoc/>
        public IReadOnlyCollection<IEmbed> Embeds => _message.Embeds;

        /// <inheritdoc/>
        public IReadOnlyCollection<ITag> Tags => _message.Tags;

        /// <inheritdoc/>
        public IReadOnlyCollection<ulong> MentionedChannelIds => _message.MentionedChannelIds;

        /// <inheritdoc/>
        public IReadOnlyCollection<ulong> MentionedRoleIds => _message.MentionedRoleIds;

        /// <inheritdoc/>
        public IReadOnlyCollection<ulong> MentionedUserIds => _message.MentionedUserIds;

        /// <inheritdoc/>
        public MessageActivity Activity => _message.Activity;

        /// <inheritdoc/>
        public MessageApplication Application => _message.Application;

        /// <inheritdoc/>
        public MessageReference Reference => _message.Reference;

        /// <inheritdoc/>
        public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions => _message.Reactions;

        /// <inheritdoc/>
        public MessageFlags? Flags => _message.Flags;

        /// <inheritdoc/>
        public IReadOnlyCollection<ISticker> Stickers => _message.Stickers;

        /// <inheritdoc/>
        public Task DeleteAsync(RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task AddReactionAsync(IEmote emote, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task RemoveAllReactionsAsync(RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null)
            => throw new NotSupportedException();

        private string DebuggerDisplay => $"{Author}: {Content} ({Id}{(Attachments.Count > 0 ? $", {Attachments.Count} Attachments" : "")})";
    }

    /// <summary>
    /// Represents the event where a message gets cached.
    /// </summary>
    public enum MessageSourceEvent
    {
        /// <summary>
        /// The message has been received.
        /// </summary>
        MessageReceived,

        /// <summary>
        /// The message has been deleted.
        /// </summary>
        MessageDeleted,

        /// <summary>
        /// The message has been updated (edited).
        /// </summary>
        MessageUpdated
    }

    public static class MessageCacheExtensions
    {
        /// <summary>
        /// Gets a message from this message channel using the optimized cache, and using <see cref="IMessageChannel.GetMessageAsync(ulong, CacheMode, RequestOptions)"/> as a fallback.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="cache">The snowflake identifier of this message.</param>
        /// <param name="messageId">The id of the cached message.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <param name="mode"></param>
        /// <param name="options"></param>
        /// <returns>A task that represents an asynchronous get operation for retrieving the message. The task result contains
        /// the retrieved message; <c>null</c> if no message is found with the specified identifier.</returns>
        public static Task<IMessage> GetMessageAsync(this IMessageChannel channel, MessageCacheService cache, ulong messageId,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            if (cache.IsDisabled || !cache.TryGetCachedMessage(messageId, out var message, sourceEvent))
                return channel.GetMessageAsync(messageId, mode, options);

            return Task.FromResult(message as IMessage);
        }
    }
}