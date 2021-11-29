using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Services
{
    /// <summary>
    /// A thread-safe class used to automatically modify or delete response messages when the command message is modified or deleted.
    /// </summary>
    public class CommandCacheService : IDisposable
    {
        private readonly ConcurrentDictionary<ulong, ulong> _cache = new ConcurrentDictionary<ulong, ulong>();
        private readonly int _max;
        private Timer _autoClear;
        private readonly Func<LogMessage, Task> _logger;
        private int _count;
        private bool _disposed;
        private readonly BaseSocketClient _client;
        private readonly Func<SocketMessage, Task> _cmdHandler;
        private readonly double _maxMessageTime;
        private readonly MessageCacheService _messageCache;

        private CommandCacheService()
        {
            IsDisabled = true;
            _disposed = true;
        }

        /// <inheritdoc cref="CommandCacheService(BaseSocketClient, int, Func{SocketMessage, Task}, Func{LogMessage, Task}, int, double, MessageCacheService)"/>
        public CommandCacheService(DiscordSocketClient client, int capacity = 200, Func<SocketMessage, Task> cmdHandler = null,
            Func<LogMessage, Task> logger = null, int period = 1800000, double maxMessageTime = 2.0, MessageCacheService messageCache = null)
            : this((BaseSocketClient)client, capacity, cmdHandler, logger, period, maxMessageTime, messageCache)
        {
        }

        /// <inheritdoc cref="CommandCacheService(BaseSocketClient, int, Func{SocketMessage, Task}, Func{LogMessage, Task}, int, double, MessageCacheService)"/>
        public CommandCacheService(DiscordShardedClient client, int capacity = 200, Func<SocketMessage, Task> cmdHandler = null,
            Func<LogMessage, Task> logger = null, int period = 1800000, double maxMessageTime = 2.0, MessageCacheService messageCache = null)
            : this((BaseSocketClient)client, capacity, cmdHandler, logger, period, maxMessageTime, messageCache)
        {
        }

        /// <summary>
        /// Initializes the cache with a maximum capacity, tracking the client's message deleted event, and optionally the client's message modified event.
        /// </summary>
        /// <param name="client">The client that the MessageDeleted handler should be hooked up to.</param>
        /// <param name="capacity">The maximum capacity of the cache.</param>
        /// <param name="cmdHandler">An optional method that gets called when the modified message event is fired.</param>
        /// <param name="logger">An optional method to use for logging.</param>
        /// <param name="period">The interval between invocations of the cache clearing, in milliseconds.</param>
        /// <param name="maxMessageTime">The max. message longevity, in hours.</param>
        /// <param name="messageCache">The message cache.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if capacity is less than 1.</exception>
        public CommandCacheService(BaseSocketClient client, int capacity = 200, Func<SocketMessage, Task> cmdHandler = null,
            Func<LogMessage, Task> logger = null, int period = 1800000, double maxMessageTime = 2.0, MessageCacheService messageCache = null)
        {
            _client = client;

            _client.MessageDeleted += OnMessageDeleted;
            _client.MessageUpdated += OnMessageModified;

            // If a method is supplied, use it, otherwise use a method that does nothing.
            _cmdHandler = cmdHandler ?? (_ => Task.CompletedTask);
            _logger = logger ?? (_ => Task.CompletedTask);

            // Make sure the max capacity is within an acceptable range.
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity can not be lower than 1.");
            }

            _max = capacity;
            _maxMessageTime = maxMessageTime;
            _messageCache = messageCache;

            // Create a timer that will clear out cached messages.
            _autoClear = new Timer(OnTimerFired, null, period, period);

            _logger(new LogMessage(LogSeverity.Info, "CmdCache", "Service initialized, MessageDeleted and OnMessageModified event handlers registered."));
        }

        /// <summary>
        /// Returns a disabled instance of <see cref="CommandCacheService"/>.
        /// </summary>
        public static CommandCacheService Disabled => new CommandCacheService();

        /// <summary>
        /// Gets all the keys in the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public ICollection<ulong> Keys => _cache.Keys;

        /// <summary>
        /// Gets all the values in the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public ICollection<ulong> Values => _cache.Values;

        /// <summary>
        /// Gets the number of command/response pairs in the cache.
        /// </summary>
        public int Count => _count;

        public bool IsDisabled { get; }

        /// <summary>
        /// Adds a key and a value to the cache, or update the value if the key already exists.
        /// </summary>
        /// <param name="key">The id of the command message.</param>
        /// <param name="value">The ids of the response messages.</param>
        public void Add(ulong key, ulong value)
        {
            if (_count >= _max)
            {
                int removeCount = _count - _max + 1;
                // The left 42 bits represent the timestamp.
                var orderedKeys = _cache.Keys.OrderBy(k => k >> 22).ToList();
                // Remove items until we're under the maximum.
                int successfulRemovals = 0;
                foreach (var orderedKey in orderedKeys)
                {
                    if (successfulRemovals >= removeCount) break;

                    var success = TryRemove(orderedKey);
                    if (success) successfulRemovals++;
                }

                // Reset _count to _cache.Count.
                UpdateCount();
            }

            // TryAdd will return false if the key already exists, in which case we don't want to increment the count.
            if (!_cache.ContainsKey(value))
            {
                Interlocked.Increment(ref _count);
            }
            _cache[key] = value;
        }

        /// <summary>
        /// Adds a new command/response pair to the cache, or updates the value if the key already exists.
        /// </summary>
        /// <param name="pair">The command/response pair.</param>
        public void Add(KeyValuePair<ulong, ulong> pair) => Add(pair.Key, pair.Value);

        /// <summary>
        /// Adds a command message and response to the cache.
        /// </summary>
        /// <param name="command">The command message.</param>
        /// <param name="response">The response message.</param>
        public void Add(IUserMessage command, IUserMessage response) => Add(command.Id, response.Id);

        /// <summary>
        /// Clears all items from the cache. Will claim all locks until the operation is complete.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _count, 0);
        }

        /// <summary>
        /// Checks whether the cache contains a set with a certain key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the key was found.</returns>
        public bool ContainsKey(ulong key) => _cache.ContainsKey(key);

        /// <summary>
        /// Returns an enumerator that iterates through the cache.
        /// </summary>
        /// <returns>An enumerator for the cache.</returns>
        public IEnumerator<KeyValuePair<ulong, ulong>> GetEnumerator() => _cache.GetEnumerator();

        /// <summary>
        /// Tries to remove a value from the cache by key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>Whether or not the removal operation was successful.</returns>
        public bool TryRemove(ulong key)
        {
            var success = _cache.TryRemove(key, out _);
            if (success) Interlocked.Decrement(ref _count);
            return success;
        }

        /// <summary>
        /// Tries to get a value from the cache by key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <param name="value">The value if found.</param>
        /// <returns>Whether or not key was found in the cache.</returns>
        public bool TryGetValue(ulong key, out ulong value) => _cache.TryGetValue(key, out value);

        /// <summary>
        /// Safely disposes of the auto-clear timer
        /// and unsubscribes from the <see cref="BaseSocketClient.MessageDeleted"/> and <see cref="BaseSocketClient.MessageUpdated"/> events.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CommandCacheService), "Service has been disposed.");
            }

            if (!disposing) return;
            _autoClear.Dispose();
            _autoClear = null;

            _client.MessageDeleted -= OnMessageDeleted;
            _client.MessageUpdated -= OnMessageModified;
            _disposed = true;

            _logger(new LogMessage(LogSeverity.Info, "CmdCache", "Cache disposed successfully."));
        }

        private void UpdateCount() => Interlocked.Exchange(ref _count, _cache.Count);

        private void OnTimerFired(object state)
        {
            // Get all messages where the timestamp is older than the specified max message longevity, then convert it to a list. The result of where merely contains references to the original
            // collection, so iterating and removing will throw an exception. Converting it to a list first avoids this.
            var toPurge = _cache.Where(p =>
            {
                var difference = DateTimeOffset.UtcNow - SnowflakeUtils.FromSnowflake(p.Key);
                return difference.TotalHours >= _maxMessageTime;
            }).ToList();

            int removed = toPurge.Count(p => TryRemove(p.Key));

            UpdateCount();

            _logger(new LogMessage(LogSeverity.Verbose, "CmdCache", $"Cleaned {removed} item(s) from the cache."));
        }

        private Task OnMessageDeleted(Cacheable<IMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> cachedChannel)
        {
            _ = Task.Run(async () =>
            {
                if (TryGetValue(cacheable.Id, out ulong responseId))
                {
                    var channel = await cachedChannel.GetOrDownloadAsync();
                    var message = await channel.GetMessageAsync(_messageCache, responseId);
                    if (message != null)
                    {
                        await _logger(new LogMessage(LogSeverity.Verbose, "CmdCache", $"Command message ({cacheable.Id}) deleted. Deleting the response..."));
                        await message.DeleteAsync();
                    }
                    else
                    {
                        await _logger(new LogMessage(LogSeverity.Info, "CmdCache", $"Command message ({cacheable.Id}) deleted but the response ({responseId}) was already deleted."));
                    }
                    TryRemove(cacheable.Id);
                }
            });

            return Task.CompletedTask;
        }

        private Task OnMessageModified(Cacheable<IMessage, ulong> cacheable, SocketMessage after, ISocketMessageChannel channel)
        {
            _ = Task.Run(async () =>
            {
                // Prevent the double reply that happens when the message is "updated" with an embed or image/video preview.
                if (after.Source != MessageSource.User ||
                    after.Author is SocketUnknownUser ||
                    string.IsNullOrEmpty(after.Content) ||
                    cacheable.HasValue && cacheable.Value.Content == after.Content)
                    return;

                if (TryGetValue(cacheable.Id, out ulong responseId))
                {
                    var response = await channel.GetMessageAsync(_messageCache, responseId);
                    if (response == null)
                    {
                        await _logger(new LogMessage(LogSeverity.Info, "CmdCache", $"A command message ({cacheable.Id}) associated to a response was found but the response ({responseId}) was already deleted."));
                        TryRemove(cacheable.Id);
                    }
                    else
                    {
                        if (response.Attachments.Count > 0)
                        {
                            await _logger(new LogMessage(LogSeverity.Verbose, "CmdCache", $"Attachment found on response ({responseId}). Deleting the response..."));
                            _ = response.DeleteAsync();
                            TryRemove(cacheable.Id);
                        }
                        else
                        {
                            await _logger(new LogMessage(LogSeverity.Verbose, "CmdCache", $"Found a response associated to command message ({cacheable.Id}) in cache."));
                            if (response.Reactions.Count > 0)
                            {
                                bool manageMessages = response.Author is IGuildUser guildUser && guildUser.GetPermissions((IGuildChannel)response.Channel).ManageMessages;

                                if (manageMessages)
                                {
                                    await _logger(new LogMessage(LogSeverity.Verbose, "CmdCache", $"Removing all reactions from response ({responseId})..."));
                                    await response.RemoveAllReactionsAsync();
                                }
                            }
                        }
                    }
                }

                if ((DateTimeOffset.UtcNow - after.CreatedAt).TotalHours <= _maxMessageTime)
                {
                    _ = _cmdHandler(after);
                }
            });

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The command cache module base.
    /// </summary>
    /// <typeparam name="TCommandContext">The <see cref="ICommandContext"/> implementation.</typeparam>
    public abstract class CommandCacheModuleBase<TCommandContext> : ModuleBase<TCommandContext>
        where TCommandContext : class, ICommandContext
    {
        /// <summary>
        /// Gets or sets the command cache service.
        /// </summary>
        public CommandCacheService Cache { get; set; }

        /// <summary>
        /// Sends or edits a message to the source channel, and adds the response to the cache if the message is new.
        /// </summary>
        /// <param name="message">The message to be sent or edited.</param>
        /// <param name="isTTS">Whether the message should be read aloud by Discord or not.</param>
        /// <param name="embed">The <see cref="EmbedType.Rich"/> <see cref="Embed"/> to be sent or edited.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <param name="allowedMentions">
        /// Specifies if notifications are sent for mentioned users and roles in the message <paramref name="message"/>. If <c>null</c>, all mentioned roles and users will be notified.
        /// </param>
        /// <param name="messageReference">The message references to be included. Used to reply to specific messages.</param>
        /// <param name="component">The message components to be included with this message. Used for interactions</param>
        /// <param name="stickers">A collection of stickers to send.</param>
        /// <param name="embeds">A array of <see cref="Embed"/>s to send with this response. Max 10.</param>
        /// <returns>A task that represents an asynchronous operation for sending or editing the message. The task contains the sent or edited message.</returns>
        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null,
            RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent component = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            if (Cache.IsDisabled)
            {
                return await base.ReplyAsync(message, isTTS, embed, options, allowedMentions, messageReference, component, stickers, embeds);
            }

            IUserMessage response;
            bool found = Cache.TryGetValue(Context.Message.Id, out ulong messageId);
            if (found && (response = (IUserMessage)await Context.Channel.GetMessageAsync(messageId)) != null)
            {
                await response.ModifyAsync(x =>
                {
                    x.Content = message;
                    x.Embed = embed;
                    x.AllowedMentions = allowedMentions ?? Optional.Create<AllowedMentions>();
                    x.Components = component;
                }).ConfigureAwait(false);

                response = (IUserMessage)await Context.Channel.GetMessageAsync(messageId).ConfigureAwait(false);
            }
            else
            {
                response = await Context.Channel.SendMessageAsync(message, isTTS, embed, options, allowedMentions, messageReference, component).ConfigureAwait(false);
                Cache.Add(Context.Message, response);
            }
            return response;
        }
    }

    public static class CommandCacheExtensions
    {
        /// <summary>
        /// Sends a file to this message channel with an optional caption, then adds it to the command cache.
        /// </summary>
        /// <param name="channel">The source channel.</param>
        /// <param name="cache">The command cache that the messages should be added to.</param>
        /// <param name="commandId">The ID of the command message.</param>
        /// <param name="stream">The <see cref="Stream" /> of the file to be sent.</param>
        /// <param name="filename">The name of the attachment.</param>
        /// <param name="text">The message to be sent.</param>
        /// <param name="isTTS">Whether the message should be read aloud by Discord or not.</param>
        /// <param name="embed">The <see cref="EmbedType.Rich"/> <see cref="Embed"/> to be sent.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <param name="isSpoiler">Whether the message attachment should be hidden as a spoiler.</param>
        /// <param name="allowedMentions">
        /// Specifies if notifications are sent for mentioned users and roles in the message <paramref name="text"/>. If <c>null</c>, all mentioned roles and users will be notified.
        /// </param>
        /// <param name="messageReference">The message references to be included. Used to reply to specific messages.</param>
        /// <returns>A task that represents an asynchronous send operation for delivering the message. The task result contains the sent message.</returns>
        public static async Task<IUserMessage> SendCachedFileAsync(this IMessageChannel channel, CommandCacheService cache, ulong commandId,
            Stream stream, string filename, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false,
            AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            IUserMessage response;
            bool found = cache.TryGetValue(commandId, out ulong responseId);
            if (found && (response = (IUserMessage)await channel.GetMessageAsync(responseId)) != null)
            {
                await response.DeleteAsync();
            }

            response = await channel.SendFileAsync(stream, filename, text, isTTS, embed, options, isSpoiler, allowedMentions, messageReference);

            if (!cache.IsDisabled)
            {
                cache.Add(commandId, response.Id);
            }

            return response;
        }
    }
}