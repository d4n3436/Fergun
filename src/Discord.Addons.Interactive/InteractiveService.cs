using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Discord.Addons.Interactive
{
    /// <summary>
    /// The interactive service.
    /// </summary>
    public class InteractiveService : IDisposable
    {
        /// <summary>
        /// The callbacks.
        /// </summary>
        private readonly Dictionary<ulong, IReactionCallback> callbacks;

        /// <summary>
        /// The default timeout.
        /// </summary>
        private readonly TimeSpan defaultTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class.
        /// </summary>
        /// <param name="discord">
        /// The discord.
        /// </param>
        /// <param name="defaultTimeout">
        /// The default timeout.
        /// </param>
        public InteractiveService(DiscordSocketClient discord, TimeSpan? defaultTimeout = null)
        {
            Discord = discord;
            discord.ReactionAdded += HandleReactionAsync;

            callbacks = new Dictionary<ulong, IReactionCallback>();
            this.defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(15);
        }

        public InteractiveService(DiscordShardedClient discord, TimeSpan? defaultTimeout = null)
        {
            Discord = discord;
            discord.ReactionAdded += HandleReactionAsync;

            callbacks = new Dictionary<ulong, IReactionCallback>();
            this.defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(15);
        }

        
        /// <summary>
        /// Gets the client
        /// </summary>
        public IDiscordClient Discord { get; }

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
        /// The <see cref="Task"/>.
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
        /// waits for the next message in the channel
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
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<SocketMessage> NextMessageAsync(SocketCommandContext context, ICriterion<SocketMessage> criterion, TimeSpan? timeout = null)
        {
            timeout = timeout ?? defaultTimeout;

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
            timeout = timeout ?? defaultTimeout;
            var message = await context.Channel.SendMessageAsync(content, isTTS, embed, options).ConfigureAwait(false);
            _ = Task.Delay(timeout.Value)
                .ContinueWith(_ => message.DeleteAsync().ConfigureAwait(false))
                .ConfigureAwait(false);
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
            => callbacks[message.Id] = callback;

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
        public void RemoveReactionCallback(ulong id) => callbacks.Remove(id);

        /// <summary>
        /// Clears all reaction callbacks
        /// </summary>
        public void ClearReactionCallbacks() => callbacks.Clear();
        
        /// <summary>
        /// Unsubscribes from a reactionHandler event
        /// </summary>
        public void Dispose()
        {
            if (Discord is DiscordShardedClient shardedClient)
            {
                shardedClient.ReactionAdded -= HandleReactionAsync;
            }
            else if (Discord is DiscordSocketClient socketClient)
            {
                socketClient.ReactionAdded -= HandleReactionAsync;
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
            if (reaction.UserId == Discord.CurrentUser.Id)
            {
                return;
            }

            if (!callbacks.TryGetValue(message.Id, out var callback))
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
                    _ = Task.Run(async () => await callback.HandleCallbackAsync(reaction).ConfigureAwait(false));
                    break;
                default:
                    await callback.HandleCallbackAsync(reaction).ConfigureAwait(false);
                    break;
            }
        }
    }
}