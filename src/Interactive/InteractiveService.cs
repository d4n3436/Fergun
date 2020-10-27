using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// The interactive service.
    /// </summary>
    public class InteractiveService : IDisposable
    {
        private readonly IDiscordClient _client;
    private readonly Dictionary<ulong, IReactionCallback> _callbacks;
        private readonly TimeSpan _defaultTimeout;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class.
        /// </summary>
        /// <param name="client">
        /// The discord.
        /// </param>
        /// <param name="defaultTimeout">
        /// The default timeout.
        /// </param>
        public InteractiveService(DiscordSocketClient client, TimeSpan? defaultTimeout = null)
        {
            _client = client;
            client.ReactionAdded += HandleReactionAsync;

            _callbacks = new Dictionary<ulong, IReactionCallback>();
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(15);
        }

        public InteractiveService(DiscordShardedClient discord, TimeSpan? defaultTimeout = null)
        {
            _client = discord;
            discord.ReactionAdded += HandleReactionAsync;

            _callbacks = new Dictionary<ulong, IReactionCallback>();
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(15);
        }

        /// <summary>
        /// waits for the next message in the channel
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="fromSourceUser">
        /// The from source user.
        /// </param>
        /// <param name="inSourceChannel">
        /// The in source channel.
        /// </param>
        /// <param name="timeout">
        /// The timeout.
        /// </param>
        /// <returns>
        /// A task representing the wait operation. The result contains the message, or null if no message was sent before the timeout.
        /// </returns>
        public Task<SocketMessage> NextMessageAsync(SocketCommandContext context, bool fromSourceUser = true, bool inSourceChannel = true, TimeSpan? timeout = null)
        {
            var criterion = new Criteria<SocketMessage>();
            if (fromSourceUser)
            {
                criterion.AddCriterion(new EnsureSourceUserCriterion());
            }

            if (inSourceChannel)
            {
                criterion.AddCriterion(new EnsureSourceChannelCriterion());
            }

            return NextMessageAsync(context, criterion, timeout);
        }
        
        /// <summary>
        /// Waits for the next message in the channel.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        /// <param name="timeout">
        /// The timeout.
        /// </param>
        /// <returns>
        /// A task representing the wait operation. The result contains the message, or null if no message was sent before the timeout.
        /// </returns>
        public async Task<SocketMessage> NextMessageAsync(SocketCommandContext context, ICriterion<SocketMessage> criterion, TimeSpan? timeout = null)
        {
            timeout ??= _defaultTimeout;

            var eventTrigger = new TaskCompletionSource<SocketMessage>();

            Task Func(SocketMessage m) => HandlerAsync(m, context, eventTrigger, criterion);

            context.Client.MessageReceived += Func;
            
            var trigger = eventTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, delay).ConfigureAwait(false);

            context.Client.MessageReceived -= Func;

            if (task == trigger)
            {
                return await trigger.ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>
        /// Sends a message with reaction callbacks
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="reactionCallbackData">
        /// The callbacks.
        /// </param>
        /// <param name="fromSourceUser">
        /// The from source user.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<IUserMessage> SendMessageWithReactionCallbacksAsync(SocketCommandContext context, ReactionCallbackData reactionCallbackData, bool fromSourceUser = true)
        {
            var criterion = new Criteria<SocketReaction>();
            if (fromSourceUser)
            {
                criterion.AddCriterion(new EnsureReactionFromSourceUserCriterion());
            }

            var callback = new InlineReactionCallback(this, context, reactionCallbackData, criterion);
            await callback.DisplayAsync().ConfigureAwait(false);
            return callback.Message;
        }

        /// <summary>
        /// Replies and then deletes the message after the provided time-span
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="content">
        /// The content.
        /// </param>
        /// <param name="isTTS">
        /// The is tts.
        /// </param>
        /// <param name="embed">
        /// The embed.
        /// </param>
        /// <param name="timeout">
        /// The timeout.
        /// </param>
        /// <param name="options">
        /// The options.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<IUserMessage> ReplyAndDeleteAsync(SocketCommandContext context, string content, bool isTTS = false, Embed embed = null, TimeSpan? timeout = null, RequestOptions options = null)
        {
            timeout ??= _defaultTimeout;
            var message = await context.Channel.SendMessageAsync(content, isTTS, embed, options).ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                await Task.Delay(timeout.Value).ConfigureAwait(false);
                await message.DeleteAsync().ConfigureAwait(false);
            }); 

            return message;
        }

        /// <summary>
        /// Sends a paginated message in the current channel
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="pager">
        /// The pager.
        /// </param>
        /// <param name="reactions">
        /// The reactions.
        /// </param>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<IUserMessage> SendPaginatedMessageAsync(SocketCommandContext context, PaginatedMessage pager, ReactionList reactions, ICriterion<SocketReaction> criterion = null,
            IUserMessage oldMessage = null)
        {
            var callback = new PaginatedMessageCallback(this, context, pager, criterion);
            await callback.DisplayAsync(reactions, oldMessage).ConfigureAwait(false);
            return callback.Message;
        }

        /// <summary>
        /// The add reaction callback.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="callback">
        /// The callback.
        /// </param>
        public void AddReactionCallback(IMessage message, IReactionCallback callback)
            => _callbacks[message.Id] = callback;

        /// <summary>
        /// Removes a reaction callback via message
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        public void RemoveReactionCallback(IMessage message) => RemoveReactionCallback(message.Id);

        /// <summary>
        /// Removes a reaction callback via message Id
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        public void RemoveReactionCallback(ulong id) => _callbacks.Remove(id);

        /// <summary>
        /// Clears all reaction callbacks
        /// </summary>
        public void ClearReactionCallbacks() => _callbacks.Clear();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InteractiveService), "Service has been disposed.");
            }
            else if (disposing)
            {
                if (_client is DiscordShardedClient shardedClient)
                {
                    shardedClient.ReactionAdded -= HandleReactionAsync;
                }
                else if (_client is DiscordSocketClient socketClient)
                {
                    socketClient.ReactionAdded -= HandleReactionAsync;
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Handles messages for NextMessageAsync
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="eventTrigger">
        /// The event trigger.
        /// </param>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private static async Task HandlerAsync(SocketMessage message, SocketCommandContext context, TaskCompletionSource<SocketMessage> eventTrigger, ICriterion<SocketMessage> criterion)
        {
            var result = await criterion.JudgeAsync(context, message).ConfigureAwait(false);
            if (result)
            {
                eventTrigger.SetResult(message);
            }
        }

        /// <summary>
        /// Handles a message reaction
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="channel">
        /// The channel.
        /// </param>
        /// <param name="reaction">
        /// The reaction.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (reaction.UserId == _client.CurrentUser.Id)
            {
                return;
            }

            if (!_callbacks.TryGetValue(message.Id, out var callback))
            {
                return;
            }

            if (!await callback.Criterion.JudgeAsync(callback.Context, reaction).ConfigureAwait(false))
            {
                return;
            }

            switch (callback.RunMode)
            {
                case RunMode.Async:
                    _ = callback.HandleCallbackAsync(reaction).ConfigureAwait(false);
                    break;
                default:
                    await callback.HandleCallbackAsync(reaction).ConfigureAwait(false);
                    break;
            }
        }
    }
}