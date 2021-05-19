using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>> _editedCache
            = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>>();

        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>> _deletedCache
            = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>>();

        private readonly ConcurrentDictionary<ulong, DateTimeOffset> _lastCommandUsageTimes = new ConcurrentDictionary<ulong, DateTimeOffset>();
        private readonly bool _onlyCacheUserDeletedEditedMessages;
        private readonly Func<LogMessage, Task> _logger;
        private readonly DiscordSocketClient _client;
        private readonly double _maxMessageTime;
        private readonly int _messageCacheSize;
        private readonly int _minCommandTime;
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
        /// <param name="minCommandTime">The min. hours since a command has to be used in a guild for the messages to be cached there. Setting this to 0 disables this requirement.<br/>
        /// Use <see cref="UpdateLastCommandUsageTime(ulong)"/> in your command handler to update the last time a command was used.</param>
        /// <param name="onlyCacheUserDeletedEditedMessages">Whether to only save messages from users in the edited/deleted messages cache.</param>
        public MessageCacheService(DiscordSocketClient client, int messageCacheSize, Func<LogMessage, Task> logger = null,
            int period = 3600000, double maxMessageTime = 6, int minCommandTime = 12, bool onlyCacheUserDeletedEditedMessages = true)
        {
            _client = client;
            _client.MessageReceived += MessageReceived;
            _client.MessageDeleted += MessageDeleted;
            _client.MessageUpdated += MessageUpdated;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            _client.ChannelDestroyed += ChannelDestroyed;
            _client.LeftGuild += LeftGuild;

            _logger = logger ?? (_ => Task.CompletedTask);
            _autoClear = new Timer(OnTimerFired, null, period, period);
            _messageCacheSize = messageCacheSize;
            _maxMessageTime = maxMessageTime;
            _minCommandTime = minCommandTime;
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
            _editedCache.Clear();
            _deletedCache.Clear();
            _lastCommandUsageTimes.Clear();
        }

        /// <summary>
        /// Attempts to clear the specified channel and all its messages from the cache.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <returns>Whether the channel has been removed from at least one cache.</returns>
        public bool TryClear(IMessageChannel channel) => TryClear(channel.Id);

        /// <summary>
        /// Attempts to clear the specified channel and all its messages from the cache.
        /// </summary>
        /// <param name="channelId">The channel id.</param>
        /// <returns>Whether the channel has been removed from at least one cache.</returns>
        public bool TryClear(ulong channelId)
            => _cache.TryRemove(channelId, out _)
               || _orderedCache.TryRemove(channelId, out _)
               || _editedCache.TryRemove(channelId, out _)
               || _deletedCache.TryRemove(channelId, out _);

        /// <summary>
        /// Attempts to get a cached message from the provided channel and message id.
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
        /// Attempts to get a cached message from the provided channel id and message id.
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
        /// Attempts to get a cached message from the provided message id, searching in every channel cache.
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
        /// Attempts to remove and return a cached message from the provided channel and message id.
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
        /// Attempts to remove and return a cached message from the provided channel id and message id.
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
        /// <param name="guildId">The id of the guild.</param>
        public void UpdateLastCommandUsageTime(ulong guildId)
            => UpdateLastCommandUsageTime(guildId, DateTimeOffset.UtcNow);

        /// <summary>
        /// Updates the last time a command was executed in the specified guild to <paramref name="dateTime"/>.
        /// </summary>
        /// <param name="guildId">The id of the guild.</param>
        /// <param name="dateTime">The <see cref="DateTimeOffset"/> to set.</param>
        public void UpdateLastCommandUsageTime(ulong guildId, DateTimeOffset dateTime)
        {
            _lastCommandUsageTimes[guildId] = dateTime;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal IEnumerable<ulong> GetMessageQueue(ulong channelId) => _orderedCache.GetValueOrDefault(channelId) ?? Enumerable.Empty<ulong>();

        private ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ICachedMessage>> GetCacheForEvent(MessageSourceEvent sourceEvent) =>
            sourceEvent switch
            {
                MessageSourceEvent.MessageReceived => _cache,
                MessageSourceEvent.MessageUpdated => _editedCache,
                _ => _deletedCache
            };

        private bool IsCachedChannelNotPresentOrEmpty(ulong channelId, MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
            => !GetCacheForEvent(sourceEvent).TryGetValue(channelId, out var cachedChannel) || cachedChannel.IsEmpty;

        private static ICachedMessage CreateCachedMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent)
            => CreateCachedMessage(message, cachedAt, sourceEvent, null);

        private static ICachedMessage CreateCachedMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent, IMessage originalMessage)
            => CreateCachedMessage(message, cachedAt, sourceEvent, originalMessage, null);

        private static ICachedMessage CreateCachedMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent, IMessage originalMessage,
            IReadOnlyCollection<IEmbed> embeds)
            => message is IUserMessage userMessage
                ? new CachedUserMessage(userMessage, cachedAt, sourceEvent, originalMessage, embeds)
                : new CachedMessage(message, cachedAt, sourceEvent, originalMessage, embeds);

        // This only applies to the long term cache.
        private int ClearOldMessages(MessageSourceEvent sourceEvent)
        {
            int removed = 0;
            var now = DateTimeOffset.UtcNow;

            foreach (var cachedChannel in GetCacheForEvent(sourceEvent))
            {
                foreach (var cachedMessage in cachedChannel.Value)
                {
                    if ((now - SnowflakeUtils.FromSnowflake(cachedMessage.Key)).TotalHours >= _maxMessageTime)
                    {
                        cachedChannel.Value.TryRemove(cachedMessage.Key, out _);
                        removed++;
                    }
                }
            }

            return removed;
        }

        private void OnTimerFired(object state)
        {
            _ = Task.Run(() =>
            {
                int removed = ClearOldMessages(MessageSourceEvent.MessageUpdated) +
                              ClearOldMessages(MessageSourceEvent.MessageDeleted);

                _ = _logger(new LogMessage(LogSeverity.Verbose, "MsgCache", $"Cleaned {removed} deleted / edited messages from the cache."));
            });
        }

        private Task MessageReceived(SocketMessage message)
        {
            HandleReceivedMessage(message);
            return Task.CompletedTask;
        }

        private void HandleReceivedMessage(IMessage message)
        {
            if (!(message.Channel is IGuildChannel guildChannel) || !_lastCommandUsageTimes.TryGetValue(guildChannel.GuildId, out var lastUsageTime))
                return;

            var now = DateTimeOffset.Now;

            if (_minCommandTime != 0 && (lastUsageTime <= now.AddHours(-_minCommandTime) || lastUsageTime > now))
                return;

            var cachedChannel = _cache.GetOrAdd(message.Channel.Id,
                new ConcurrentDictionary<ulong, ICachedMessage>(Environment.ProcessorCount, (int)(_messageCacheSize * 1.05)));

            var channelQueue = _orderedCache.GetOrAdd(message.Channel.Id, new ConcurrentQueue<ulong>());

            cachedChannel[message.Id] = CreateCachedMessage(message, message.CreatedAt, MessageSourceEvent.MessageReceived);
            channelQueue.Enqueue(message.Id);

            while (cachedChannel.Count > _messageCacheSize && channelQueue.TryDequeue(out ulong msgId))
            {
                cachedChannel.TryRemove(msgId, out _);
            }
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            HandleDeletedMessage(cache.Id, channel);
            return Task.CompletedTask;
        }

        private void HandleDeletedMessage(ulong messageId, IMessageChannel channel)
        {
            // The default message cache removes deleted message from its cache, here we just move the message to the deleted messages
            if (TryRemoveCachedMessage(channel, messageId, out var message)) // A message gets deleted
            {
                if (_onlyCacheUserDeletedEditedMessages && message.Source != MessageSource.User) return;

                message.Update(DateTimeOffset.UtcNow, MessageSourceEvent.MessageDeleted);

                // move cached message to the deleted messages cache
                var cachedChannel = _deletedCache.GetOrAdd(channel.Id, new ConcurrentDictionary<ulong, ICachedMessage>());
                cachedChannel[messageId] = message;
            }
            else if (TryGetCachedMessage(channel, messageId, out message, MessageSourceEvent.MessageUpdated)) // An updated message gets deleted
            {
                message.Update(DateTimeOffset.UtcNow, MessageSourceEvent.MessageDeleted);

                // move cached message to the deleted messages cache
                var cachedChannel = _deletedCache.GetOrAdd(channel.Id, new ConcurrentDictionary<ulong, ICachedMessage>());
                cachedChannel[messageId] = message;
            }
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> cachedbefore, SocketMessage updatedMessage, ISocketMessageChannel channel)
        {
            HandleUpdatedMessage(updatedMessage, channel);
            return Task.CompletedTask;
        }

        private void HandleUpdatedMessage(IMessage updatedMessage, IMessageChannel channel)
        {
            // From Discord API docs:
            // "Unlike creates, message updates may contain only a subset of the full message object payload"
            // It isn't possible to update a message property since the Update() method is internal,
            // but I only want the updated embeds/link previews, so I just added a new parameter for the updated embed
            // in CachedMessage/CachedUserMessage's constructor and use that overload if the updated message's author is a SocketUnknownUser
            // and both messages contains a different number of embeds.

            if (TryGetCachedMessage(channel, updatedMessage.Id, out var message, MessageSourceEvent.MessageUpdated)) // An updated message gets updated again
            {
                var cachedChannel = _editedCache.GetOrAdd(channel.Id, new ConcurrentDictionary<ulong, ICachedMessage>());

                message.Update(null);

                bool useUpdatedEmbeds = updatedMessage.Author is SocketUnknownUser && updatedMessage.Embeds.Count != message.Embeds.Count;

                if (useUpdatedEmbeds)
                {
                    // "Add" the updated embeds to the existing message
                    cachedChannel[updatedMessage.Id] = CreateCachedMessage(message, message.CachedAt, MessageSourceEvent.MessageUpdated, null, updatedMessage.Embeds);
                }
                else if (!(updatedMessage.Author is SocketUnknownUser))
                {
                    // Create a new cached message containing the previous and current messages
                    cachedChannel[updatedMessage.Id] = CreateCachedMessage(updatedMessage, DateTimeOffset.UtcNow, MessageSourceEvent.MessageUpdated, message);
                }
            }
            else if (TryGetCachedMessage(channel, updatedMessage.Id, out message)) // A message gets updated
            {
                bool useUpdatedEmbeds = updatedMessage.Author is SocketUnknownUser && updatedMessage.Embeds.Count != message.Embeds.Count;

                // We need to simulate the functionality of the default message cache,
                // so we update the message in the short term cache instead of removing it

                if (useUpdatedEmbeds)
                {
                    // "Add" the updated embeds to the existing message
                    _cache[channel.Id][updatedMessage.Id] = CreateCachedMessage(message, DateTimeOffset.UtcNow, MessageSourceEvent.MessageUpdated, null, updatedMessage.Embeds);
                }
                else if (!(updatedMessage.Author is SocketUnknownUser))
                {
                    _cache[channel.Id][updatedMessage.Id] = CreateCachedMessage(updatedMessage, DateTimeOffset.UtcNow, MessageSourceEvent.MessageUpdated);
                }

                if (_onlyCacheUserDeletedEditedMessages && message.Source != MessageSource.User) return;

                var cachedChannel = _editedCache.GetOrAdd(channel.Id, new ConcurrentDictionary<ulong, ICachedMessage>());

                if (useUpdatedEmbeds)
                {
                    // "Add" the updated embeds to the existing message
                    cachedChannel[updatedMessage.Id] = CreateCachedMessage(message, message.CachedAt, MessageSourceEvent.MessageUpdated, null, updatedMessage.Embeds);
                }
                else if (!(updatedMessage.Author is SocketUnknownUser))
                {
                    // "Copy" the cached message to the edited messages cache
                    cachedChannel[updatedMessage.Id] = CreateCachedMessage(updatedMessage, DateTimeOffset.UtcNow, MessageSourceEvent.MessageUpdated, message);
                }
            }
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> cachedMessages, ISocketMessageChannel channel)
        {
            if (IsCachedChannelNotPresentOrEmpty(channel.Id) && IsCachedChannelNotPresentOrEmpty(channel.Id, MessageSourceEvent.MessageUpdated))
            {
                return Task.CompletedTask;
            }

            _ = Task.Run(() =>
            {
                foreach (var cached in cachedMessages)
                {
                    HandleDeletedMessage(cached.Id, channel);
                }
            });

            return Task.CompletedTask;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            _ = Task.Run(async () =>
            {
                if (channel is IMessageChannel messageChanel && TryClear(messageChanel))
                {
                    await _logger(new LogMessage(LogSeverity.Verbose, "MsgCache", $"Removed cached channel {messageChanel.Id}"));
                }
            });

            return Task.CompletedTask;
        }

        private Task LeftGuild(SocketGuild guild)
        {
            _ = Task.Run(async () =>
            {
                int count = 0;
                foreach (var channel in guild.TextChannels)
                {
                    if (TryClear(channel))
                        count++;
                }

                _lastCommandUsageTimes.TryRemove(guild.Id, out _);
                if (count > 0)
                {
                    await _logger(new LogMessage(LogSeverity.Verbose, "MsgCache", $"Removed {count} cached channels from guild {guild.Id}"));
                }
            });

            return Task.CompletedTask;
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
            _client.ChannelDestroyed -= ChannelDestroyed;
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
        /// Gets the original message prior to being edited. This property is only present if the message has been edited once.
        /// </summary>
        public IMessage OriginalMessage { get; }

        internal void Update(IMessage originalMessage);
    }

    /// <summary>
    /// Represents a generic cached message.
    /// </summary>
    public interface ICachedMessage : IEditedMessage
    {
        /// <summary>
        /// Gets when this message was cached.
        /// </summary>
        public DateTimeOffset CachedAt { get; }

        /// <summary>
        /// Gets the source event of this message.
        /// </summary>
        public MessageSourceEvent SourceEvent { get; }

        internal void Update(DateTimeOffset cachedAt, MessageSourceEvent sourceEvent);
    }

    /// <summary>
    /// Represents a cached user message.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class CachedUserMessage : CachedMessage, IUserMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedUserMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        internal CachedUserMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent)
            : base(message, cachedAt, sourceEvent)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        /// <param name="originalMessage">The original message (prior to being edited).</param>
        internal CachedUserMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent, IMessage originalMessage)
            : base(message, cachedAt, sourceEvent, originalMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        /// <param name="originalMessage">The original message (prior to being edited).</param>
        /// <param name="embeds">A collection of embeds.</param>
        internal CachedUserMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent, IMessage originalMessage,
            IReadOnlyCollection<IEmbed> embeds)
            : base(message, cachedAt, sourceEvent, originalMessage, embeds)
        {
        }

        /// <inheritdoc/>
        public Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null)
            => ((IUserMessage)_message).ModifyAsync(func, options);

        /// <inheritdoc/>
        public Task ModifySuppressionAsync(bool suppressEmbeds, RequestOptions options = null)
            => ((IUserMessage)_message).ModifySuppressionAsync(suppressEmbeds, options);

        /// <inheritdoc/>
        public Task PinAsync(RequestOptions options = null) => ((IUserMessage)_message).PinAsync(options);

        /// <inheritdoc/>
        public Task UnpinAsync(RequestOptions options = null) => ((IUserMessage)_message).UnpinAsync(options);

        /// <inheritdoc/>
        public Task CrosspostAsync(RequestOptions options = null) => ((IUserMessage)_message).CrosspostAsync(options);

        /// <inheritdoc/>
        public string Resolve(TagHandling userHandling = TagHandling.Name, TagHandling channelHandling = TagHandling.Name,
            TagHandling roleHandling = TagHandling.Name, TagHandling everyoneHandling = TagHandling.Ignore, TagHandling emojiHandling = TagHandling.Name)
            => ((IUserMessage)_message).Resolve(userHandling, channelHandling, roleHandling, everyoneHandling, emojiHandling);

        /// <inheritdoc/>
        public IUserMessage ReferencedMessage => ((IUserMessage)_message).ReferencedMessage;
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
        internal CachedMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent)
        {
            _message = message;
            CachedAt = cachedAt;
            SourceEvent = sourceEvent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        /// <param name="originalMessage">The original message (prior to being edited).</param>
        internal CachedMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent, IMessage originalMessage)
            : this(message, cachedAt, sourceEvent)
        {
            OriginalMessage = originalMessage;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        /// <param name="originalMessage">The original message (prior to being edited).</param>
        /// <param name="embeds">A collection of embeds.</param>
        internal CachedMessage(IMessage message, DateTimeOffset cachedAt, MessageSourceEvent sourceEvent, IMessage originalMessage,
            IReadOnlyCollection<IEmbed> embeds)
            : this(message, cachedAt, sourceEvent, originalMessage)
        {
            _embeds = embeds;
        }

        private protected readonly IMessage _message;
        private protected readonly IReadOnlyCollection<IEmbed> _embeds;

        /// <inheritdoc/>
        public IMessage OriginalMessage { get; private set; }

        /// <inheritdoc/>
        public DateTimeOffset CachedAt { get; private set; }

        /// <inheritdoc/>
        public MessageSourceEvent SourceEvent { get; private set; }

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
        public IReadOnlyCollection<IEmbed> Embeds => _embeds ?? _message.Embeds;

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
        public Task DeleteAsync(RequestOptions options = null) => _message.DeleteAsync(options);

        /// <inheritdoc/>
        public Task AddReactionAsync(IEmote emote, RequestOptions options = null) => _message.AddReactionAsync(emote, options);

        /// <inheritdoc/>
        public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null) => _message.RemoveReactionAsync(emote, user, options);

        /// <inheritdoc/>
        public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null) => _message.RemoveReactionAsync(emote, userId, options);

        /// <inheritdoc/>
        public Task RemoveAllReactionsAsync(RequestOptions options = null) => _message.RemoveAllReactionsAsync(options);

        /// <inheritdoc/>
        public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null) => _message.RemoveAllReactionsForEmoteAsync(emote, options);

        /// <inheritdoc/>
        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null)
            => _message.GetReactionUsersAsync(emoji, limit, options);

        void IEditedMessage.Update(IMessage originalMessage) => OriginalMessage = originalMessage;

        void ICachedMessage.Update(DateTimeOffset cachedAt, MessageSourceEvent sourceEvent)
        {
            CachedAt = cachedAt;
            SourceEvent = sourceEvent;
        }

        protected string DebuggerDisplay => $"{Author}: {Content} ({Id}{(Attachments.Count > 0 ? $", {Attachments.Count} Attachments" : "")})";
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
        /// Gets a message from this message channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="messageId">The snowflake identifier of the message.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <param name="mode"></param>
        /// <param name="options"></param>
        /// <returns>A task that represents an asynchronous get operation for retrieving the message. The task result contains
        /// the retrieved message; <c>null</c> if no message is found with the specified identifier.</returns>
        public static async Task<IMessage> GetMessageAsync(this IMessageChannel channel, MessageCacheService cache, ulong messageId,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            if (cache == null || cache.IsDisabled || !cache.TryGetCachedMessage(channel, messageId, out var message, sourceEvent))
            {
                return sourceEvent == MessageSourceEvent.MessageReceived && mode == CacheMode.AllowDownload
                    ? await channel.GetMessageAsync(messageId, mode, options)
                    : null;
            }

            return message;
        }

        /// <summary>
        /// Gets a cached message from this message channel from the optimized cache.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="messageId">The snowflake identifier of the message.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>A task that represents an asynchronous get operation for retrieving the message. The task result contains
        /// the retrieved message; <c>null</c> if no message is found with the specified identifier.</returns>
        public static ICachedMessage GetCachedMessage(this IMessageChannel channel, MessageCacheService cache, ulong messageId,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
        {
            cache.TryGetCachedMessage(channel, messageId, out var message, sourceEvent);
            return message;
        }

        /// <summary>
        /// Gets the last N messages from this message channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="limit">The numbers of message to be gotten from.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <returns>A paged collection of messages.</returns>
        public static IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(this IMessageChannel channel, MessageCacheService cache,
            int limit = DiscordConfig.MaxMessagesPerBatch, RequestOptions options = null)
            => GetMessagesInternalAsync(channel, cache, null, Direction.Before, limit, CacheMode.AllowDownload, options);

        /// <summary>
        /// Gets the last N messages from this message channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="fromMessageId">The ID of the starting message to get the messages from.</param>
        /// <param name="dir">The direction of the messages to be gotten from.</param>
        /// <param name="limit">The numbers of message to be gotten from.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <returns>A paged collection of messages.</returns>
        public static IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(this IMessageChannel channel, MessageCacheService cache,
            ulong fromMessageId, Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch, RequestOptions options = null)
            => GetMessagesInternalAsync(channel, cache, fromMessageId, dir, limit, CacheMode.AllowDownload, options);

        /// <summary>
        /// Gets the last N messages from this message channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="fromMessage">The starting message to get the messages from.</param>
        /// <param name="dir">The direction of the messages to be gotten from.</param>
        /// <param name="limit">The numbers of message to be gotten from.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <returns>A paged collection of messages.</returns>
        public static IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(this IMessageChannel channel, MessageCacheService cache,
            IMessage fromMessage, Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch, RequestOptions options = null)
            => GetMessagesInternalAsync(channel, cache, fromMessage.Id, dir, limit, CacheMode.AllowDownload, options);

        /// <summary>
        /// Gets a collection of cached messages from this message channel.
        /// </summary>
        /// <remarks>Unlike the other overloads, this method allows you to get all the cached messages from a channel, and also allows you to specify a source event.</remarks>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="sourceEvent">The source event.</param>
        /// <returns>A read-only collection of cached messages.</returns>
        public static IReadOnlyCollection<ICachedMessage> GetCachedMessages(this IMessageChannel channel, MessageCacheService cache,
            MessageSourceEvent sourceEvent = MessageSourceEvent.MessageReceived)
            => cache
                .GetCacheForChannel(channel, sourceEvent)
                .Values
                .ToArray();

        /// <summary>
        /// Gets the last N cached messages from this message channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="limit">The number of messages to get.</param>
        /// <returns>A read-only collection of cached messages.</returns>
        public static IReadOnlyCollection<ICachedMessage> GetCachedMessages(this IMessageChannel channel, MessageCacheService cache,
            int limit = DiscordConfig.MaxMessagesPerBatch)
            => GetCachedMessagesInternal(channel, cache, null, Direction.Before, limit);

        /// <summary>
        /// Gets the last N cached messages from this message channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="fromMessageId">The message ID to start the fetching from.</param>
        /// <param name="dir">The direction of which the message should be gotten from.</param>
        /// <param name="limit">The number of messages to get.</param>
        /// <returns>A read-only collection of cached messages.</returns>
        public static IReadOnlyCollection<ICachedMessage> GetCachedMessages(this IMessageChannel channel, MessageCacheService cache, ulong fromMessageId,
            Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
            => GetCachedMessagesInternal(channel, cache, fromMessageId, dir, limit);

        /// <summary>
        /// Gets the last N cached messages from this message channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="fromMessage">The message to start the fetching from.</param>
        /// <param name="dir">The direction of which the message should be gotten from.</param>
        /// <param name="limit">The number of messages to get.</param>
        /// <returns>A read-only collection of cached messages.</returns>
        public static IReadOnlyCollection<ICachedMessage> GetCachedMessages(this IMessageChannel channel, MessageCacheService cache, IMessage fromMessage,
            Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
            => GetCachedMessagesInternal(channel, cache, fromMessage.Id, dir, limit);

        private static IReadOnlyCollection<ICachedMessage> GetCachedMessagesInternal(IMessageChannel channel, MessageCacheService cache,
            ulong? fromMessageId, Direction dir, int limit)
            => cache == null || cache.IsDisabled ? Array.Empty<ICachedMessage>() : GetMany(cache, channel, fromMessageId, dir, limit);

        private static IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesInternalAsync(IMessageChannel channel, MessageCacheService cache,
            ulong? fromMessageId, Direction dir, int limit, CacheMode mode, RequestOptions options)
        {
            if (dir == Direction.After && fromMessageId == null)
                return AsyncEnumerable.Empty<IReadOnlyCollection<IMessage>>();

            var cachedMessages = GetMany(cache, channel, fromMessageId, dir, limit);
            var result = ImmutableArray.Create(cachedMessages).ToAsyncEnumerable<IReadOnlyCollection<IMessage>>();

            switch (dir)
            {
                case Direction.Before:
                {
                    limit -= cachedMessages.Count;
                    if (mode == CacheMode.CacheOnly || limit <= 0)
                        return result;

                    //Download remaining messages
                    ulong? minId = cachedMessages.Count > 0 ? cachedMessages.Min(x => x.Id) : fromMessageId;
                    var downloadedMessages = channel.GetMessagesAsync(minId ?? 0, dir, limit, CacheMode.AllowDownload, options);
                    return cachedMessages.Count != 0 ? result.Concat(downloadedMessages) : downloadedMessages;
                }
                case Direction.After:
                {
                    limit -= cachedMessages.Count;
                    if (mode == CacheMode.CacheOnly || limit <= 0)
                        return result;

                    //Download remaining messages
                    ulong maxId = cachedMessages.Count > 0 ? cachedMessages.Max(x => x.Id) : fromMessageId ?? 0;
                    var downloadedMessages = channel.GetMessagesAsync(maxId, dir, limit, CacheMode.AllowDownload, options);
                    return cachedMessages.Count != 0 ? result.Concat(downloadedMessages) : downloadedMessages;
                }
                //Direction.Around
                default:
                {
                    if (mode == CacheMode.CacheOnly || limit <= cachedMessages.Count)
                        return result;

                    //Cache isn't useful here since Discord will send them anyways
                    return channel.GetMessagesAsync(fromMessageId ?? 0, dir, limit, CacheMode.AllowDownload, options);
                }
            }
        }

        private static IReadOnlyCollection<ICachedMessage> GetMany(MessageCacheService cache, IMessageChannel channel,
            ulong? fromMessageId, Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
            => GetMany(cache, channel.Id, fromMessageId, dir, limit);

        private static IReadOnlyCollection<ICachedMessage> GetMany(MessageCacheService cache, ulong channelId, ulong? fromMessageId,
            Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
        {

            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            if (cache == null || cache.IsDisabled || limit == 0) return Array.Empty<ICachedMessage>();

            var cachedChannel = cache.GetCacheForChannel(channelId);
            if (cachedChannel.Count == 0)
                return Array.Empty<ICachedMessage>();

            var orderedChannel = cache.GetMessageQueue(channelId);

            IEnumerable<ulong> cachedMessageIds;
            if (fromMessageId == null)
                cachedMessageIds = orderedChannel;
            else switch (dir)
            {
                case Direction.Before:
                    cachedMessageIds = orderedChannel.Where(x => x < fromMessageId.Value);
                    break;
                case Direction.After:
                    cachedMessageIds = orderedChannel.Where(x => x > fromMessageId.Value);
                    break;
                //Direction.Around
                default:
                {
                    if (!cachedChannel.TryGetValue(fromMessageId.Value, out var msg))
                        return Array.Empty<ICachedMessage>();

                    int around = limit / 2;
                    var before = GetMany(cache, channelId, fromMessageId, Direction.Before, around);
                    var after = GetMany(cache, channelId, fromMessageId, Direction.After, around).Reverse();

                    return after
                        .Append(msg)
                        .Concat(before)
                        .ToArray();
                }
            }

            if (dir == Direction.Before)
                cachedMessageIds = cachedMessageIds.Reverse();
            if (dir == Direction.Around) // Only happens if fromMessageId is null, should only get "around" and itself (+1)
                limit = limit / 2 + 1;

            return cachedMessageIds
                .Select(x => cachedChannel.TryGetValue(x, out var msg) ? msg : null)
                .Where(x => x != null)
                .Take(limit)
                .ToArray();
        }
    }
}