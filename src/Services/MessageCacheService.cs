using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Fergun.Services
{
    /// <summary>
    /// Represents a cache of deleted and edited messages.
    /// </summary>
    public class MessageCacheService : IDisposable
    {
        private readonly ConcurrentDictionary<ulong, CachedMessage> _cache = new ConcurrentDictionary<ulong, CachedMessage>();
        private readonly IReadOnlyDictionary<ulong, UserConfig> _userConfigCache;
        private readonly Func<LogMessage, Task> _logger;
        private readonly DiscordSocketClient _client;
        private readonly double _maxMessageTime;
        private Timer _autoClear;
        private bool _disposed;

        private MessageCacheService()
        {
            IsDisabled = true;
            _disposed = true;
        }

        public MessageCacheService(DiscordSocketClient client, IReadOnlyDictionary<ulong, UserConfig> userConfigCache,
            Func<LogMessage, Task> logger = null, int period = 3600000, double maxMessageTime = 6)
        {
            _client = client;
            _client.MessageDeleted += MessageDeleted;
            _client.MessageUpdated += MessageUpdated;

            _userConfigCache = userConfigCache;
            _logger = logger ?? (_ => Task.CompletedTask);
            _autoClear = new Timer(OnTimerFired, null, period, period);
            _maxMessageTime = maxMessageTime;
        }

        /// <summary>
        /// Returns a disabled instance of <see cref="MessageCacheService"/>.
        /// </summary>
        public static MessageCacheService Disabled => new MessageCacheService();

        /// <summary>
        /// Gets the message cache.
        /// </summary>
        public IReadOnlyDictionary<ulong, CachedMessage> Cache => _cache;

        /// <summary>
        /// Gets a collection containing the keys in the cache.
        /// </summary>
        public ICollection<ulong> Keys => _cache.Keys;

        /// <summary>
        /// Gets a collection containing the values in the cache.
        /// </summary>
        public ICollection<CachedMessage> Values => _cache.Values;

        public bool IsDisabled { get; }

        /// <summary>
        /// Clears all keys and values from the cache.
        /// </summary>
        public void Clear() => _cache.Clear();

        /// <summary>
        /// Clears the cache based on a predicate.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns>The number of removed messages.</returns>
        public int ClearOnPredicate(Func<KeyValuePair<ulong, CachedMessage>, bool> predicate)
        {
            var toPurge = _cache.Where(predicate).ToList();
            return toPurge.Count(p => _cache.TryRemove(p.Key, out _));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void OnTimerFired(object state)
        {
            int removed = ClearOnPredicate(x => (DateTimeOffset.UtcNow - x.Value.CreatedAt).TotalHours >= _maxMessageTime);

            _ = _logger(new LogMessage(LogSeverity.Verbose, "MsgCache", $"Cleaned {removed} deleted / edited messages from the cache."));
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            var message = cache.Value;

            if (message?.Source != MessageSource.User)
                return Task.CompletedTask;

            if (_userConfigCache.TryGetValue(message.Author.Id, out var userConfig) && userConfig.IsOptedOutSnipe)
                return Task.CompletedTask;

            _cache[message.Id] = new CachedMessage(message, DateTimeOffset.UtcNow, CachedMessageSourceEvent.MessageDeleted);

            return Task.CompletedTask;
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> cachedbefore, SocketMessage after, ISocketMessageChannel channel)
        {
            if (string.IsNullOrEmpty(after?.Content) || after.Source != MessageSource.User)
                return Task.CompletedTask;

            var before = cachedbefore.Value;

            if (string.IsNullOrEmpty(before?.Content) || before.Content == after.Content)
                return Task.CompletedTask;

            if (_userConfigCache.TryGetValue(after.Author.Id, out var userConfig) && userConfig.IsOptedOutSnipe)
                return Task.CompletedTask;

            _cache[before.Id] = new CachedMessage(before, DateTimeOffset.UtcNow, CachedMessageSourceEvent.MessageUpdated);
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

            _client.MessageDeleted -= MessageDeleted;
            _client.MessageUpdated -= MessageUpdated;
            _disposed = true;
        }
    }

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
        public CachedMessage(IMessage message, DateTimeOffset cachedAt, CachedMessageSourceEvent sourceEvent)
        {
            Id = message.Id;
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
        public CachedMessageSourceEvent SourceEvent { get; }
    }

    /// <summary>
    /// Represents the event where a message gets cached.
    /// </summary>
    public enum CachedMessageSourceEvent
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