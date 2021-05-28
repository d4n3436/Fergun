using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<ulong, IReactionCallback> _callbacks = new ConcurrentDictionary<ulong, IReactionCallback>();
        private readonly ConcurrentDictionary<ulong, IInteractionCallback> _interactionCallbacks = new ConcurrentDictionary<ulong, IInteractionCallback>();
        private readonly TimeSpan? _defaultTimeout = TimeSpan.FromSeconds(15);
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class.
        /// </summary>
        /// <param name="client">
        /// The client.
        /// </param>
        /// <param name="defaultTimeout">
        /// The default timeout.
        /// </param>
        public InteractiveService(DiscordSocketClient client, TimeSpan? defaultTimeout = null)
        {
            _client = client;
            client.ReactionAdded += HandleReactionAsync;
            client.InteractionCreated += HandleInteractionAsync;
            if (defaultTimeout != null) _defaultTimeout = defaultTimeout;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class.
        /// </summary>
        /// <param name="client">
        /// The client.
        /// </param>
        /// <param name="defaultTimeout">
        /// The default timeout.
        /// </param>
        public InteractiveService(DiscordShardedClient client, TimeSpan? defaultTimeout = null)
        {
            _client = client;
            client.ReactionAdded += HandleReactionAsync;
            client.InteractionCreated += HandleInteractionAsync;
            if (defaultTimeout != null) _defaultTimeout = defaultTimeout;
        }

        /// <summary>
        /// Waits for the next message in the channel.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="fromSourceUser">
        /// Determines whether the user have to be the source user or not.
        /// </param>
        /// <param name="inSourceChannel">
        /// Determines whether the channel have to be the source channel or not.
        /// </param>
        /// <param name="timeout">
        /// The timeout.
        /// </param>
        /// <returns>
        /// A task representing the wait operation. The result contains the message, or <c>null</c> if no message was sent before the timeout.
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
        /// A task representing the wait operation. The result contains the message, or <c>null</c> if no message was sent before the timeout.
        /// </returns>
        public async Task<SocketMessage> NextMessageAsync(SocketCommandContext context, ICriterion<SocketMessage> criterion, TimeSpan? timeout = null)
        {
            timeout ??= _defaultTimeout;

            var eventTrigger = new TaskCompletionSource<SocketMessage>();

            Task Func(SocketMessage m) => HandlerAsync(m, context, eventTrigger, criterion);

            context.Client.MessageReceived += Func;

            var trigger = eventTrigger.Task;
            var delay = Task.Delay(timeout!.Value);
            var task = await Task.WhenAny(trigger, delay).ConfigureAwait(false);

            context.Client.MessageReceived -= Func;

            if (task == trigger)
            {
                return await trigger.ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>
        /// Sends a message with reaction callbacks.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="callbackData">
        /// The callback data.
        /// </param>
        /// <param name="fromSourceUser">
        /// Determines whether the user have to be the source user or not.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. The result contains the message.
        /// </returns>
        public async Task<IUserMessage> SendMessageWithReactionCallbacksAsync(SocketCommandContext context, ReactionCallbackData callbackData, bool fromSourceUser = true)
        {
            var criterion = new Criteria<SocketReaction>();
            if (fromSourceUser)
            {
                criterion.AddCriterion(new EnsureReactionFromSourceUserCriterion());
            }

            var callback = new InlineReactionCallback(this, context, callbackData, criterion);
            await callback.DisplayAsync().ConfigureAwait(false);
            return callback.Message;
        }

        /// <summary>
        /// Sends a message in the current channel and then deletes the message after the provided timeout.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="text">
        /// The message to be sent.
        /// </param>
        /// <param name="isTTS">
        /// Determines whether the message should be read aloud by Discord or not.
        /// </param>
        /// <param name="embed">
        /// The <see cref="EmbedType.Rich"/> <see cref="Embed"/> to be sent.
        /// </param>
        /// <param name="timeout">
        /// The timeout.
        /// </param>
        /// <param name="options">
        /// The options to be used when sending the request.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. The result contains the message.
        /// </returns>
        public async Task<IUserMessage> ReplyAndDeleteAsync(SocketCommandContext context, string text, bool isTTS = false, Embed embed = null, TimeSpan? timeout = null, RequestOptions options = null)
        {
            timeout ??= _defaultTimeout;
            var message = await context.Channel.SendMessageAsync(text, isTTS, embed, options).ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                await Task.Delay(timeout!.Value).ConfigureAwait(false);
                await message.DeleteAsync().ConfigureAwait(false);
            });

            return message;
        }

        /// <summary>
        /// Sends a paginated message.
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
        /// <param name="oldMessage">
        /// An old message to reuse.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. The result contains the message.
        /// </returns>
        public async Task<IUserMessage> SendPaginatedMessageAsync(SocketCommandContext context, PaginatedMessage pager, ReactionList reactions, ICriterion<SocketInteraction> criterion = null,
            IUserMessage oldMessage = null)
        {
            var callback = new PaginatedMessageCallback(this, context, pager, criterion);
            await callback.DisplayAsync(reactions, oldMessage).ConfigureAwait(false);
            return callback.Message;
        }

        /// <summary>
        /// Adds a reaction callback via message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="callback">
        /// The callback.
        /// </param>
        public void AddReactionCallback(IMessage message, IReactionCallback callback)
            => AddReactionCallback(message.Id, callback);

        /// <summary>
        /// Adds a reaction callback via message Id.
        /// </summary>
        /// <param name="id">
        /// The message Id.
        /// </param>
        /// <param name="callback">
        /// The callback.
        /// </param>
        public void AddReactionCallback(ulong id, IReactionCallback callback) => _callbacks[id] = callback;

        /// <summary>
        /// Removes a reaction callback via message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        public void RemoveReactionCallback(IMessage message) => RemoveReactionCallback(message.Id);

        /// <summary>
        /// Removes a reaction callback via message Id.
        /// </summary>
        /// <param name="id">
        /// The message Id.
        /// </param>
        public bool RemoveReactionCallback(ulong id) => _callbacks.TryRemove(id, out _);

        /// <summary>
        /// Clears all the reaction callbacks.
        /// </summary>
        public void ClearReactionCallbacks() => _callbacks.Clear();

        /// <summary>
        /// Checks whether there is a reaction callback with a certain message Id.
        /// </summary>
        /// <param name="id">The message Id to search for.</param>
        /// <returns>Whether or not the Id was found.</returns>
        public bool ContainsKey(ulong id) => _callbacks.ContainsKey(id);

        public void AddInteractionCallback(IMessage message, IInteractionCallback callback)
            => AddInteractionCallback(message.Id, callback);

        public void AddInteractionCallback(ulong id, IInteractionCallback callback) => _interactionCallbacks[id] = callback;

        public void RemoveInteractionCallback(IMessage message) => RemoveInteractionCallback(message.Id);

        public bool RemoveInteractionCallback(ulong id) => _interactionCallbacks.TryRemove(id, out _);

        public void ClearInteractionCallbacks() => _interactionCallbacks.Clear();

        public bool ContainsInteraction(ulong id) => _interactionCallbacks.ContainsKey(id);

        /// <summary>
        /// Unsubscribes from the <see cref="BaseSocketClient.InteractionCreated"/> event.
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
                throw new ObjectDisposedException(nameof(InteractiveService), "Service has been disposed.");
            }

            if (!disposing) return;
            ((BaseSocketClient)_client).ReactionAdded -= HandleReactionAsync;
            ((BaseSocketClient)_client).InteractionCreated -= HandleInteractionAsync;
            _disposed = true;
        }

        /// <summary>
        /// Handles messages for NextMessageAsync().
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
        /// Handles a message reaction.
        /// </summary>
        /// <param name="cacheable">
        /// The cached message.
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
        private Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cacheable, ISocketMessageChannel channel, SocketReaction reaction)
        {
            _ = Task.Run(async () =>
            {
                if (reaction.UserId == _client.CurrentUser.Id) return;

                if (!_callbacks.TryGetValue(cacheable.Id, out var callback)) return;

                if (!await callback.Criterion.JudgeAsync(callback.Context, reaction).ConfigureAwait(false)) return;

                _ = callback.HandleCallbackAsync(reaction).ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an interaction.
        /// </summary>
        /// <param name="interaction">
        /// The interaction.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private Task HandleInteractionAsync(SocketInteraction interaction)
        {
            _ = Task.Run(async () =>
            {
                if (interaction.Type != InteractionType.MessageComponent || !(interaction is SocketMessageComponent component))
                    return;

                if (interaction.User?.Id == _client.CurrentUser.Id) return;

                if (!TryGetInteractionCallback(component, out var callback, out string emote)) return;

                bool isCommandUser = await callback.Criterion.JudgeAsync(callback.Context, interaction).ConfigureAwait(false);

                await callback.HandleCallbackAsync(interaction, emote, isCommandUser).ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private bool TryGetInteractionCallback(SocketMessageComponent component, out IInteractionCallback callback, out string emote)
        {
            emote = null;
            callback = null;

            var span = component.Data.CustomId.AsSpan();
            int index = span.IndexOf('_');
            if (index == -1) return false;

            var emoteSpan = span.Slice(index + 1, span.Length - index - 1);
            if (emoteSpan.IsEmpty) return false;

            if (!ulong.TryParse(component.Data.CustomId.AsSpan().Slice(0, index), out ulong id)) return false;
            if (!_interactionCallbacks.TryGetValue(id, out callback)) return false;

            emote = emoteSpan.ToString();
            return true;
        }
    }
}