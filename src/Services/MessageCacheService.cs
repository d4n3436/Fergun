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
        private readonly ConcurrentDictionary<ulong, ICachedMessage> _cache = new ConcurrentDictionary<ulong, ICachedMessage>();
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
        public IReadOnlyDictionary<ulong, ICachedMessage> Cache => _cache;

        /// <summary>
        /// Gets a collection containing the keys in the cache.
        /// </summary>
        public ICollection<ulong> Keys => _cache.Keys;

        /// <summary>
        /// Gets a collection containing the values in the cache.
        /// </summary>
        public ICollection<ICachedMessage> Values => _cache.Values;

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
        public int ClearOnPredicate(Func<KeyValuePair<ulong, ICachedMessage>, bool> predicate)
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
    /// Represents a generic cached message.
    /// </summary>
    public interface ICachedMessage : IMessage
    {
        /// <summary>
        /// Gets when this message was cached.
        /// </summary>
        public DateTimeOffset CachedAt { get; }

        /// <summary>
        /// Gets the source event of this message.
        /// </summary>
        public CachedMessageSourceEvent SourceEvent { get; }
    }

    /// <summary>
    /// Represents a cached message.
    /// </summary>
    public class CachedMessage : ICachedMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedMessage"/> class.
        /// </summary>
        /// <param name="message">The original message.</param>
        /// <param name="cachedAt">When the message was cached.</param>
        /// <param name="sourceEvent">The source event of the message.</param>
        public CachedMessage(IMessage message, DateTimeOffset cachedAt, CachedMessageSourceEvent sourceEvent)
        {
            _message = message;
            CachedAt = cachedAt;
            SourceEvent = sourceEvent;
        }

        private readonly IMessage _message;

        /// <inheritdoc/>
        public DateTimeOffset CachedAt { get; }

        /// <inheritdoc/>
        public CachedMessageSourceEvent SourceEvent { get; }

        /// <inheritdoc />
        public ulong Id => _message.Id;

        /// <inheritdoc />
        public DateTimeOffset CreatedAt => _message.CreatedAt;

        /// <inheritdoc />
        public MessageType Type => _message.Type;

        /// <inheritdoc />
        public MessageSource Source => _message.Source;

        /// <inheritdoc />
        public bool IsTTS => _message.IsTTS;

        /// <inheritdoc />
        public bool IsPinned => _message.IsPinned;

        /// <inheritdoc />
        public bool IsSuppressed => _message.IsSuppressed;

        /// <inheritdoc />
        public bool MentionedEveryone => _message.MentionedEveryone;

        /// <inheritdoc />
        public string Content => _message.Content;

        /// <inheritdoc />
        public DateTimeOffset Timestamp => _message.Timestamp;

        /// <inheritdoc />
        public DateTimeOffset? EditedTimestamp => _message.EditedTimestamp;

        /// <inheritdoc />
        public IMessageChannel Channel => _message.Channel;

        /// <inheritdoc />
        public IUser Author => _message.Author;

        /// <inheritdoc />
        public IReadOnlyCollection<IAttachment> Attachments => _message.Attachments;

        /// <inheritdoc />
        public IReadOnlyCollection<IEmbed> Embeds => _message.Embeds;

        /// <inheritdoc />
        public IReadOnlyCollection<ITag> Tags => _message.Tags;

        /// <inheritdoc />
        public IReadOnlyCollection<ulong> MentionedChannelIds => _message.MentionedChannelIds;

        /// <inheritdoc />
        public IReadOnlyCollection<ulong> MentionedRoleIds => _message.MentionedRoleIds;

        /// <inheritdoc />
        public IReadOnlyCollection<ulong> MentionedUserIds => _message.MentionedUserIds;

        /// <inheritdoc />
        public MessageActivity Activity => _message.Activity;

        /// <inheritdoc />
        public MessageApplication Application => _message.Application;

        /// <inheritdoc />
        public MessageReference Reference => _message.Reference;

        /// <inheritdoc />
        public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions => _message.Reactions;

        /// <inheritdoc />
        public MessageFlags? Flags => _message.Flags;

        /// <inheritdoc />
        public IReadOnlyCollection<ISticker> Stickers => _message.Stickers;

        /// <inheritdoc />
        public Task DeleteAsync(RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task AddReactionAsync(IEmote emote, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task RemoveAllReactionsAsync(RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null) => throw new NotSupportedException();

        /// <inheritdoc />
        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Represents the event where a message gets cached.
    /// </summary>
    public enum CachedMessageSourceEvent
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
}