using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;

namespace Fergun.Interactive
{
    // Based on Discord.InteractivityAddon
    // https://github.com/Playwo/Discord.InteractivityAddon

    /// <summary>
    /// Represents a service containing methods for interactivity purposes.
    /// </summary>
    public class InteractiveService
    {
        private readonly BaseSocketClient _client;

        /// <summary>
        /// Gets the default timeout for interactive actions provided by this service.
        /// </summary>
        public TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class using the default timeout.
        /// </summary>
        /// <param name="client">An instance of <see cref="BaseSocketClient"/>.</param>
        public InteractiveService(BaseSocketClient client)
        {
            InteractiveGuards.NotNull(client, nameof(client));
            _client = client;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class using a specified default timeout.
        /// </summary>
        /// <param name="client">An instance of <see cref="BaseSocketClient"/>.</param>
        /// <param name="defaultTimeout">The default timeout for the interactive actions.</param>
        public InteractiveService(BaseSocketClient client, TimeSpan defaultTimeout)
            : this(client)
        {
            if (defaultTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultTimeout), "Timespan cannot be negative or zero.");
            }

            DefaultTimeout = defaultTimeout;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class using the default timeout.
        /// </summary>
        /// <param name="client">An instance of <see cref="DiscordSocketClient"/>.</param>
        public InteractiveService(DiscordSocketClient client)
            : this((BaseSocketClient)client)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class using a specified default timeout.
        /// </summary>
        /// <param name="client">An instance of <see cref="DiscordSocketClient"/>.</param>
        /// <param name="defaultTimeout">The default timeout for the interactive actions.</param>
        public InteractiveService(DiscordSocketClient client, TimeSpan defaultTimeout)
            : this((BaseSocketClient)client, defaultTimeout)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class using the default timeout.
        /// </summary>
        /// <param name="client">An instance of <see cref="DiscordShardedClient"/>.</param>
        public InteractiveService(DiscordShardedClient client)
            : this((BaseSocketClient)client)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractiveService"/> class using a specified default timeout.
        /// </summary>
        /// <param name="client">An instance of <see cref="DiscordShardedClient"/>.</param>
        /// <param name="defaultTimeout">The default timeout for the interactive actions.</param>
        public InteractiveService(DiscordShardedClient client, TimeSpan defaultTimeout)
            : this((BaseSocketClient)client, defaultTimeout)
        {
        }

        /// <summary>
        /// Sends a message to a channel (after an optional delay) and deletes it after another delay.
        /// </summary>
        /// <remarks>Discard the returning task if you don't want to wait it for completion.</remarks>
        /// <param name="channel">The target message channel.</param>
        /// <param name="sendDelay">The time to wait before sending the message.</param>
        /// <param name="deleteDelay">The time to wait between sending and deleting the message.</param>
        /// <param name="message">An existing message to modify.</param>
        /// <param name="text">The message to be sent.</param>
        /// <param name="isTTS">Determines whether the message should be read aloud by Discord or not.</param>
        /// <param name="embed">The <see cref="EmbedType.Rich"/> <see cref="Embed"/> to be sent.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <param name="allowedMentions">
        ///     Specifies if notifications are sent for mentioned users and roles in the message <paramref name="text"/>.
        ///     If <c>null</c>, all mentioned roles and users will be notified.
        /// </param>
        /// <param name="messageReference">The message references to be included. Used to reply to specific messages.</param>
        /// <returns>A task that represents the asynchronous delay, send message operation, delay and delete message operation.</returns>
        public async Task DelayedSendMessageAndDeleteAsync(IMessageChannel channel, TimeSpan? sendDelay = null, TimeSpan? deleteDelay = null,
            IUserMessage message = null, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null,
            AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            InteractiveGuards.NotNull(channel, nameof(channel));
            InteractiveGuards.MessageFromCurrentUser(_client, message);

            await Task.Delay(sendDelay ?? TimeSpan.Zero).ConfigureAwait(false);

            if (message == null)
            {
                message = await channel.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference).ConfigureAwait(false);
            }
            else
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = text;
                    x.Embed = embed;
                    x.AllowedMentions = allowedMentions;
                }).ConfigureAwait(false);
            }

            await DelayedDeleteMessageAsync(message, deleteDelay).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a file to a channel delayed and deletes it after another delay.
        /// </summary>
        /// <remarks>Discard the returning task if you don't want to wait it for completion.</remarks>
        /// <param name="channel">The target message channel.</param>
        /// <param name="sendDelay">The time to wait before sending the message.</param>
        /// <param name="deleteDelay">The time to wait between sending and deleting the message.</param>
        /// <param name="filePath">The file path of the file.</param>
        /// <param name="text">The message to be sent.</param>
        /// <param name="isTTS">Whether the message should be read aloud by Discord or not.</param>
        /// <param name="embed">The <see cref="EmbedType.Rich"/> <see cref="Embed"/> to be sent.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <param name="isSpoiler">Whether the message attachment should be hidden as a spoiler.</param>
        /// <param name="allowedMentions">
        ///     Specifies if notifications are sent for mentioned users and roles in the message <paramref name="text"/>.
        ///     If <c>null</c>, all mentioned roles and users will be notified.
        /// </param>
        /// <param name="messageReference">The message references to be included. Used to reply to specific messages.</param>
        /// <returns>A task that represents the asynchronous delay, send message operation, delay and delete message operation.</returns>
        public async Task DelayedSendFileAndDeleteAsync(IMessageChannel channel, TimeSpan? sendDelay = null, TimeSpan? deleteDelay = null,
            string filePath = null, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null,
            bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            InteractiveGuards.NotNull(channel, nameof(channel));

            await Task.Delay(sendDelay ?? TimeSpan.Zero).ConfigureAwait(false);
            var msg = await channel.SendFileAsync(filePath, text, isTTS, embed, options, isSpoiler, allowedMentions, messageReference)
                .ConfigureAwait(false);
            await DelayedDeleteMessageAsync(msg, deleteDelay).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a file to a channel delayed and deletes it after another delay.
        /// </summary>
        /// <remarks>Discard the returning task if you don't want to wait it for completion.</remarks>
        /// <param name="channel">The target message channel.</param>
        /// <param name="sendDelay">The time to wait before sending the message.</param>
        /// <param name="deleteDelay">The time to wait between sending and deleting the message.</param>
        /// <param name="stream">The <see cref="Stream"/> of the file to be sent.</param>
        /// <param name="filename">The name of the attachment.</param>
        /// <param name="text">The message to be sent.</param>
        /// <param name="isTTS">Whether the message should be read aloud by Discord or not.</param>
        /// <param name="embed">The <see cref="EmbedType.Rich"/> <see cref="Embed"/> to be sent.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <param name="isSpoiler">Whether the message attachment should be hidden as a spoiler.</param>
        /// <param name="allowedMentions">
        ///     Specifies if notifications are sent for mentioned users and roles in the message <paramref name="text"/>.
        ///     If <c>null</c>, all mentioned roles and users will be notified.
        /// </param>
        /// <param name="messageReference">The message references to be included. Used to reply to specific messages.</param>
        /// <returns>A task that represents the asynchronous delay, send message operation, delay and delete message operation.</returns>
        public async Task DelayedSendFileAndDeleteAsync(IMessageChannel channel, TimeSpan? sendDelay = null, TimeSpan? deleteDelay = null,
            Stream stream = null, string filename = null, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null,
            bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            InteractiveGuards.NotNull(channel, nameof(channel));

            await Task.Delay(sendDelay ?? TimeSpan.Zero).ConfigureAwait(false);
            var msg = await channel.SendFileAsync(stream, filename, text, isTTS, embed, options, isSpoiler, allowedMentions, messageReference)
                .ConfigureAwait(false);
            await DelayedDeleteMessageAsync(msg, deleteDelay).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a message after a delay.
        /// </summary>
        /// <remarks>Discard the returning task if you don't want to wait it for completion.</remarks>
        /// <param name="message">The message to delete</param>
        /// <param name="deleteDelay">The time to wait before deleting the message</param>
        /// <returns>A task that represents the asynchronous delay and delete message operation.</returns>
        public async Task DelayedDeleteMessageAsync(IMessage message, TimeSpan? deleteDelay = null)
        {
            InteractiveGuards.NotNull(message, nameof(message));

            await Task.Delay(deleteDelay ?? DefaultTimeout).ConfigureAwait(false);

            try
            {
                await message.DeleteAsync().ConfigureAwait(false);
            }
            catch (HttpException e) when (e.HttpCode == HttpStatusCode.NotFound)
            {
                // We want to delete the message so we don't care if the message has been already deleted.
            }
        }

        /// <summary>
        /// Gets the next incoming message that passes the <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter">A filter which the message has to pass.</param>
        /// <param name="action">
        /// An action which gets executed to incoming message,
        /// where <see cref="SocketMessage"/> is the incoming message and <see cref="bool"/>
        /// is whether the message passed the <paramref name="filter"/>.
        /// </param>
        /// <param name="timeout">The time to wait before the methods returns a timeout result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the request.</param>
        /// <returns>
        /// A task that represents the asynchronous wait operation for the next message.
        /// The task result contains an <see cref="InteractiveResult{T}"/> with the
        /// message (if successful), the elapsed time and the status.
        /// </returns>
        public async Task<InteractiveResult<SocketMessage>> NextMessageAsync(Func<SocketMessage, bool> filter = null,
            Func<SocketMessage, bool, Task> action = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;

            action ??= (message, filterPassed) => Task.CompletedTask;
            filter ??= message => true;

            var messageSource = new TaskCompletionSource<InteractiveResult<SocketMessage>>();
            var cancelSource = new TaskCompletionSource<InteractiveResult<SocketMessage>>();

            var cancellationRegistration = cancellationToken.Register(()
                => cancelSource.SetResult(new InteractiveResult<SocketMessage>(null, DateTimeOffset.UtcNow - startTime, InteractiveStatus.Canceled)));

            var messageTask = messageSource.Task;
            var cancelTask = cancelSource.Task;
            var timeoutTask = Task.Delay(timeout ?? DefaultTimeout, CancellationToken.None);

            Task HandleMessageAsync(SocketMessage message)
            {
                _ = Task.Run(async () =>
                {
                    if (message.Author.Id == _client.CurrentUser.Id)
                    {
                        return;
                    }

                    bool passFilter = filter(message);
                    await action(message, passFilter).ConfigureAwait(false);
                    if (!passFilter)
                    {
                        return;
                    }

                    messageSource.SetResult(new InteractiveResult<SocketMessage>(message, message.Timestamp - startTime));
                }, CancellationToken.None);

                return Task.CompletedTask;
            }

            try
            {
                _client.MessageReceived += HandleMessageAsync;

                var result = await Task.WhenAny(messageTask, timeoutTask, cancelTask).ConfigureAwait(false);

                if (result == messageTask)
                {
                    return await messageTask.ConfigureAwait(false);
                }

                if (result == cancelTask)
                {
                    return await cancelTask.ConfigureAwait(false);
                }

                return new InteractiveResult<SocketMessage>(null, timeout ?? DefaultTimeout, InteractiveStatus.TimedOut);
            }
            finally
            {
                _client.MessageReceived -= HandleMessageAsync;
                cancellationRegistration.Dispose();
            }
        }

        /// <summary>
        /// Gets the next incoming reaction that passes the <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter">A filter which the reaction has to pass.</param>
        /// <param name="action">
        /// An action which gets executed to incoming reactions, where <see cref="SocketReaction"/>
        /// is the incoming reaction and <see cref="bool"/> is whether the interaction passed the <paramref name="filter"/>.
        /// </param>
        /// <param name="timeout">The time to wait before the methods returns a timeout result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the request.</param>
        /// <returns>
        /// A task that represents the asynchronous wait operation for the next reaction.
        /// The task result contains an <see cref="InteractiveResult{T}"/> with the
        /// reaction (if successful), the elapsed time and the status.
        /// </returns>
        public async Task<InteractiveResult<SocketReaction>> NextReactionAsync(Func<SocketReaction, bool> filter = null,
            Func<SocketReaction, bool, Task> action = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;

            filter ??= reaction => true;
            action ??= (reaction, filterPassed) => Task.CompletedTask;

            var reactionSource = new TaskCompletionSource<InteractiveResult<SocketReaction>>();
            var cancelSource = new TaskCompletionSource<InteractiveResult<SocketReaction>>();

            var cancellationRegistration = cancellationToken.Register(()
                => cancelSource.SetResult(new InteractiveResult<SocketReaction>(null, DateTimeOffset.UtcNow - startTime, InteractiveStatus.Canceled)));

            var reactionTask = reactionSource.Task;
            var cancelTask = cancelSource.Task;
            var timeoutTask = Task.Delay(timeout ?? DefaultTimeout, CancellationToken.None);

            Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
            {
                _ = Task.Run(async () =>
                {
                    if (reaction.UserId == _client.CurrentUser.Id)
                    {
                        return;
                    }

                    bool passFilter = filter(reaction);
                    await action(reaction, passFilter).ConfigureAwait(false);
                    if (!passFilter)
                    {
                        return;
                    }

                    reactionSource.SetResult(new InteractiveResult<SocketReaction>(reaction, DateTimeOffset.UtcNow - startTime));
                }, CancellationToken.None);

                return Task.CompletedTask;
            }

            try
            {
                _client.ReactionAdded += HandleReactionAsync;

                var result = await Task.WhenAny(reactionTask, cancelTask, timeoutTask).ConfigureAwait(false);

                if (result == reactionTask)
                {
                    return await reactionTask.ConfigureAwait(false);
                }

                if (result == cancelTask)
                {
                    return await cancelTask.ConfigureAwait(false);
                }

                return new InteractiveResult<SocketReaction>(null, timeout ?? DefaultTimeout, InteractiveStatus.TimedOut);
            }
            finally
            {
                _client.ReactionAdded -= HandleReactionAsync;
                cancellationRegistration.Dispose();
            }
        }

#if DNETLABS
        /// <summary>
        /// Gets the next interaction that passes the <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter">A filter which the interaction has to pass.</param>
        /// <param name="action">
        /// An action which gets executed to incoming interactions,
        /// where <see cref="SocketInteraction"/> is the incoming interaction and <see cref="bool"/>
        /// is whether the interaction passed the <paramref name="filter"/>.
        /// </param>
        /// <param name="timeout">The time to wait before the methods returns a timeout result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the request.</param>
        /// <returns>
        /// A task that represents the asynchronous wait operation for the next interaction.
        /// The task result contains an <see cref="InteractiveResult{T}"/> with the
        /// interaction (if successful), the elapsed time and the status.
        /// </returns>
        public async Task<InteractiveResult<SocketInteraction>> NextInteractionAsync(Func<SocketInteraction, bool> filter = null,
            Func<SocketInteraction, bool, Task> action = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;

            action ??= (interaction, filterPassed) => Task.CompletedTask;
            filter ??= interaction => true;

            var interactionSource = new TaskCompletionSource<InteractiveResult<SocketInteraction>>();
            var cancelSource = new TaskCompletionSource<InteractiveResult<SocketInteraction>>();

            var cancellationRegistration = cancellationToken.Register(()
                => cancelSource.SetResult(new InteractiveResult<SocketInteraction>(null, DateTimeOffset.UtcNow - startTime, InteractiveStatus.Canceled)));

            var interactionTask = interactionSource.Task;
            var cancelTask = cancelSource.Task;
            var timeoutTask = Task.Delay(timeout ?? DefaultTimeout, CancellationToken.None);

            Task HandleInteractionAsync(SocketInteraction interaction)
            {
                _ = Task.Run(async () =>
                {
                    if (interaction.User?.Id == _client.CurrentUser.Id)
                    {
                        return;
                    }

                    bool passFilter = filter(interaction);
                    await action(interaction, passFilter).ConfigureAwait(false);
                    if (!passFilter)
                    {
                        return;
                    }

                    interactionSource.SetResult(new InteractiveResult<SocketInteraction>(interaction, interaction.CreatedAt - startTime));
                }, CancellationToken.None);

                return Task.CompletedTask;
            }

            try
            {
                _client.InteractionCreated += HandleInteractionAsync;

                var result = await Task.WhenAny(interactionTask, timeoutTask, cancelTask).ConfigureAwait(false);

                if (result == interactionTask)
                {
                    return await interactionTask.ConfigureAwait(false);
                }

                if (result == cancelTask)
                {
                    return await cancelTask.ConfigureAwait(false);
                }

                return new InteractiveResult<SocketInteraction>(null, timeout ?? DefaultTimeout, InteractiveStatus.TimedOut);
            }
            finally
            {
                _client.InteractionCreated -= HandleInteractionAsync;
                cancellationRegistration.Dispose();
            }
        }
#endif

        /// <summary>
        /// Sends a paginator with pages which the user can change through via reactions or buttons.
        /// </summary>
        /// <param name="paginator">The paginator to send.</param>
        /// <param name="channel">The channel to send the <see cref="Paginator"/> to.</param>
        /// <param name="timeout">The time until the <see cref="Paginator"/> times out.</param>
        /// <param name="message">An existing message to modify to display the <see cref="Paginator"/>.</param>
        /// <param name="doNotWait">
        /// Whether to not wait for a timeout or a cancellation and instead return when the message has been sent.
        /// The paginator will still receive inputs.
        /// </param>
        /// <param name="resetTimeoutOnInput">Whether to reset the internal timeout timer when a valid input is received.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the paginator.</param>
        /// <returns>
        /// A task that represents the asynchronous operation for sending the paginator and waiting for a timeout or cancellation.<br/>
        /// The task result contains an <see cref="InteractiveMessageResult{T}"/> with the message used for pagination
        /// (which may not be valid if the message has been deleted), the elapsed time and the status.<br/>
        /// If <paramref name="doNotWait"/> is <see langword="true"/> or the paginator only contains one page,
        /// the task will return when the message has been sent and the result will contain the sent message and a <see cref="InteractiveStatus.Success"/> status.
        /// </returns>
        public async Task<InteractiveMessageResult> SendPaginatorAsync(Paginator paginator, IMessageChannel channel, TimeSpan? timeout = null,
            IUserMessage message = null, bool doNotWait = false, bool resetTimeoutOnInput = false, CancellationToken cancellationToken = default)
        {
            InteractiveGuards.NotNull(paginator, nameof(paginator));
            InteractiveGuards.NotNull(channel, nameof(channel));
            InteractiveGuards.MessageFromCurrentUser(_client, message);
            InteractiveGuards.DeleteAndDisableInputNotSet(paginator.ActionOnTimeout, nameof(paginator.ActionOnTimeout));
            InteractiveGuards.DeleteAndDisableInputNotSet(paginator.ActionOnCancellation, nameof(paginator.ActionOnCancellation));
#if !DNETLABS
            InteractiveGuards.CanUseComponents(paginator);
#endif

            if (paginator.InputType == InputType.Messages)
            {
                throw new NotSupportedException("Paginators using messages as input are not supported (yet).");
            }
            if (paginator.InputType == InputType.SelectMenus)
            {
                throw new NotSupportedException("Paginators using select menus as input are not supported (yet).");
            }

            var cancelSource = new TaskCompletionSource<InteractiveMessageResult>();
            var timeoutProvider = new TimeoutProvider<InteractiveMessageResult>(timeout ?? DefaultTimeout, resetTimeoutOnInput);

            message = await SendOrModifyMessageAsync(paginator, message, channel).ConfigureAwait(false);

            var paginatorTask = WaitForPaginatorTimeoutOrCancellationAsync(paginator, timeout, message, cancelSource, timeoutProvider, cancellationToken)
                .ConfigureAwait(false);

            if (!doNotWait) return await paginatorTask;

            if (paginator.MaxPageIndex > 1)
            {
                _ = paginatorTask;
            }

            return new InteractiveMessageResult(TimeSpan.Zero, InteractiveStatus.Success, message);
        }

        private async Task<InteractiveMessageResult> WaitForPaginatorTimeoutOrCancellationAsync(Paginator paginator,
            TimeSpan? timeout, IUserMessage message, TaskCompletionSource<InteractiveMessageResult> cancelSource,
            TimeoutProvider<InteractiveMessageResult> timeoutProvider, CancellationToken cancellationToken)
        {
            var startTime = DateTimeOffset.UtcNow;
            var cancellationRegistration = cancellationToken.Register(
                () => cancelSource.SetResult(new InteractiveMessageResult(DateTimeOffset.UtcNow - startTime, InteractiveStatus.Canceled, message)));

            var cancelTask = cancelSource.Task;
            var timeoutTask = timeoutProvider.WaitAsync();

            try
            {
                switch (paginator.InputType)
                {
                    case InputType.Messages:
                        break;

                    case InputType.Reactions:
                        _client.ReactionAdded += ReactionAdded;
                        break;

                    case InputType.Buttons:
#if DNETLABS
                        _client.InteractionCreated += InteractionCreated;
#endif
                        break;

                    case InputType.SelectMenus:
                        break;
                }

                _ = paginator.InitializeMessageAsync(message).ConfigureAwait(false);
                var taskResult = await Task.WhenAny(timeoutTask, cancelTask).ConfigureAwait(false);

                var result = taskResult == cancelTask
                    ? await cancelTask.ConfigureAwait(false)
                    : new InteractiveMessageResult(timeout ?? DefaultTimeout, InteractiveStatus.TimedOut);

                await ApplyActionOnStopAsync(paginator, result).ConfigureAwait(false);

                return result;
            }
            finally
            {
                switch (paginator.InputType)
                {
                    case InputType.Messages:
                        break;

                    case InputType.Reactions:
                        _client.ReactionAdded -= ReactionAdded;
                        break;

                    case InputType.Buttons:
#if DNETLABS
                        _client.InteractionCreated -= InteractionCreated;
#endif
                        break;

                    case InputType.SelectMenus:
                        break;
                }

                cancellationRegistration.Dispose();
                timeoutProvider.TryDispose();
            }

            Task ReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
            {
                _ = Task.Run(async () =>
                {
                    await HandleReactionForPaginatorAsync(reaction, paginator, message, cancelSource, timeoutProvider, startTime).ConfigureAwait(false);
                }, CancellationToken.None);

                return Task.CompletedTask;
            }

#if DNETLABS
            Task InteractionCreated(SocketInteraction interaction)
            {
                _ = Task.Run(async () =>
                {
                    await HandleInteractionForPaginatorAsync(interaction, paginator, message, cancelSource, timeoutProvider, startTime).ConfigureAwait(false);
                }, CancellationToken.None);

                return Task.CompletedTask;
            }
#endif
        }

        /// <summary>
        /// Sends a selection to the given message channel.
        /// </summary>
        /// <typeparam name="TOption">The type of the options the selection contains.</typeparam>
        /// <param name="selection">The selection to send.</param>
        /// <param name="channel">The channel to send the selection to.</param>
        /// <param name="timeout">The time until the selection times out.</param>
        /// <param name="message">A message to be used for the selection instead of a new one.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the selection.</param>
        /// <returns>
        /// A task that represents the asynchronous operation for sending the selection and waiting for a valid input, a timeout or a cancellation.<br/>
        /// The task result contains an <see cref="InteractiveMessageResult{T}"/> with the selected value (if valid), the message used for the selection
        /// (which may not be valid if the message has been deleted), the elapsed time and the status.<br/>
        /// </returns>
        public async Task<InteractiveMessageResult<TOption>> SendSelectionAsync<TOption>(BaseSelection<TOption> selection, IMessageChannel channel,
            TimeSpan? timeout = null, IUserMessage message = null, CancellationToken cancellationToken = default)
        {
            InteractiveGuards.NotNull(selection, nameof(selection));
            InteractiveGuards.NotNull(channel, nameof(channel));
            InteractiveGuards.MessageFromCurrentUser(_client, message);
            InteractiveGuards.DeleteAndDisableInputNotSet(selection.ActionOnTimeout, nameof(selection.ActionOnTimeout));
            InteractiveGuards.DeleteAndDisableInputNotSet(selection.ActionOnCancellation, nameof(selection.ActionOnCancellation));
            InteractiveGuards.DeleteAndDisableInputNotSet(selection.ActionOnSuccess, nameof(selection.ActionOnSuccess));
#if !DNETLABS
            InteractiveGuards.CanUseComponents(selection);
#endif

            message = await SendOrModifyMessageAsync(selection, message, channel).ConfigureAwait(false);

            var startTime = DateTimeOffset.UtcNow;

            var selectionSource = new TaskCompletionSource<InteractiveMessageResult<TOption>>();
            var cancelSource = new TaskCompletionSource<InteractiveMessageResult<TOption>>();

            var cancellationRegistration = cancellationToken.Register(
                () => cancelSource.SetResult(new InteractiveMessageResult<TOption>(default, DateTimeOffset.UtcNow - startTime,
                    InteractiveStatus.Canceled, message)));

            var selectionTask = selectionSource.Task;
            var cancelTask = cancelSource.Task;
            var timeoutTask = Task.Delay(timeout ?? DefaultTimeout, CancellationToken.None);

            try
            {
                switch (selection.InputType)
                {
                    case InputType.Messages:
                        _client.MessageReceived += MessageReceived;

                        break;

                    case InputType.Reactions:
                        _client.ReactionAdded += ReactionAdded;
                        break;

                    case InputType.Buttons:
                    case InputType.SelectMenus:
#if DNETLABS
                        _client.InteractionCreated += InteractionCreated;
#endif
                        break;
                }

                _ = selection.InitializeMessageAsync(message).ConfigureAwait(false);
                var taskResult = await Task.WhenAny(selectionTask, timeoutTask, cancelTask).ConfigureAwait(false);

                InteractiveMessageResult<TOption> result;
                if (taskResult == selectionTask)
                {
                    result = await selectionTask.ConfigureAwait(false);
                }
                else if (taskResult == cancelTask)
                {
                    result = await cancelTask.ConfigureAwait(false);
                }
                else
                {
                    result = new InteractiveMessageResult<TOption>(default, timeout ?? DefaultTimeout, InteractiveStatus.TimedOut, message);
                }

                await ApplyActionOnStopAsync(selection, result).ConfigureAwait(false);

                return result;
            }
            finally
            {
                switch (selection.InputType)
                {
                    case InputType.Messages:
                        _client.MessageReceived -= MessageReceived;

                        break;

                    case InputType.Reactions:
                        _client.ReactionAdded -= ReactionAdded;
                        break;

                    case InputType.Buttons:
                    case InputType.SelectMenus:
#if DNETLABS
                        _client.InteractionCreated -= InteractionCreated;
#endif
                        break;
                }
                cancellationRegistration.Dispose();
            }

            Task MessageReceived(SocketMessage msg)
            {
                _ = Task.Run(async () =>
                {
                    await HandleMessageForSelectionAsync(msg, selection, message, startTime, selectionSource, cancelSource).ConfigureAwait(false);
                }, CancellationToken.None);

                return Task.CompletedTask;
            }

            Task ReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
            {
                _ = Task.Run(async () =>
                {
                    await HandleReactionForSelectionAsync(reaction, selection, message, startTime, selectionSource, cancelSource).ConfigureAwait(false);
                }, CancellationToken.None);

                return Task.CompletedTask;
            }

#if DNETLABS
            Task InteractionCreated(SocketInteraction interaction)
            {
                _ = Task.Run(async () =>
                {
                    await HandleInteractionForSelectionAsync(interaction, selection, message, startTime, selectionSource, cancelSource).ConfigureAwait(false);
                }, CancellationToken.None);

                return Task.CompletedTask;
            }
#endif
        }

        private static async Task<IUserMessage> SendOrModifyMessageAsync<TOption>(IInteractiveElement<TOption> element,
            IUserMessage message, IMessageChannel channel)
        {
            var page = element switch
            {
                Paginator paginator => await paginator.GetOrLoadCurrentPageAsync().ConfigureAwait(false),
                BaseSelection<TOption> selection => selection.SelectionPage,
                _ => throw new ArgumentException("Unknown interactive element.", nameof(element))
            };

#if DNETLABS
            MessageComponent component = null;
            bool moreThanOnePage = !(element is Paginator pag) || pag.MaxPageIndex > 1;
            if ((element.InputType == InputType.Buttons || element.InputType == InputType.SelectMenus) && moreThanOnePage)
            {
                component = element.BuildComponents(false);
            }
#endif

            if (message != null)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = page.Text;
                    x.Embed = page.Embed;
#if DNETLABS
                    x.Components = component;
#endif
                }).ConfigureAwait(false);
            }
            else
            {
#if DNETLABS
                message = await channel.SendMessageAsync(page.Text,
                    embed: page.Embed, component: component).ConfigureAwait(false);
#else
                message = await channel.SendMessageAsync(page.Text,
                    embed: page.Embed).ConfigureAwait(false);
#endif
            }

            return message;
        }

        private static async Task ApplyActionOnStopAsync<TOption>(IInteractiveElement<TOption> element, IInteractiveMessageResult result)
        {
            var action = result.Status switch
            {
                InteractiveStatus.TimedOut => element.ActionOnTimeout,
                InteractiveStatus.Canceled => element.ActionOnTimeout,
                InteractiveStatus.Success when element is BaseSelection<TOption> selection => selection.ActionOnSuccess,
                InteractiveStatus.Unknown => throw new InvalidOperationException("Unknown action."),
                _ => throw new InvalidOperationException("Unknown action.")
            };

            if (action == ActionOnStop.None)
            {
                return;
            }

            if (action.HasFlag(ActionOnStop.DeleteMessage))
            {
                try
                {
                    await result.Message.DeleteAsync().ConfigureAwait(false);
                }
                catch (HttpException e) when (e.HttpCode == HttpStatusCode.NotFound)
                {
                    // We want to delete the message so we don't care if the message has been already deleted.
                }
                return;
            }

            Page page = null;
            if (action.HasFlag(ActionOnStop.ModifyMessage))
            {
                page = result.Status switch
                {
                    InteractiveStatus.TimedOut => element.TimedOutPage,
                    InteractiveStatus.Canceled => element.CanceledPage,
                    InteractiveStatus.Success when element is BaseSelection<TOption> selection => selection.SuccessPage,
                    InteractiveStatus.Unknown => throw new InvalidOperationException("Unknown action."),
                    _ => throw new InvalidOperationException("Unknown action.")
                };
            }

#if DNETLABS
            MessageComponent components = null;
            if (action.HasFlag(ActionOnStop.DisableInput))
            {
                if (element.InputType == InputType.Buttons || element.InputType == InputType.SelectMenus)
                {
                    components = element.BuildComponents(true);
                }
            }
            else if (action.HasFlag(ActionOnStop.DeleteInput) && element.InputType != InputType.Reactions)
            {
                components = new ComponentBuilder().Build();
            }

            if (page?.Text != null || page?.Embed != null || components != null)
#else
            if (page?.Text != null || page?.Embed != null)
#endif
            {
                try
                {
                    await result.Message.ModifyAsync(x =>
                    {
                        x.Embed = page?.Embed ?? new Optional<Embed>();
                        x.Content = page?.Text ?? new Optional<string>();
#if DNETLABS
                        x.Components = components ?? new Optional<MessageComponent>();
#endif
                    }).ConfigureAwait(false);
                }
                catch (HttpException ex) when (ex.DiscordCode == 10008)
                {
                    // Ignore 10008 (Unknown Message) error.
                }
            }

            if (action.HasFlag(ActionOnStop.DeleteInput) && element.InputType == InputType.Reactions)
            {
                bool manageMessages = result.Message.Channel is SocketGuildChannel guildChannel
                                      && guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

                if (manageMessages)
                {
                    await result.Message.RemoveAllReactionsAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task HandleReactionForPaginatorAsync(SocketReaction reaction, Paginator paginator, IUserMessage message,
            TaskCompletionSource<InteractiveMessageResult> cancelSource, TimeoutProvider<InteractiveMessageResult> timeoutProvider,
            DateTimeOffset startTime)
        {
            if (reaction.MessageId != message.Id || reaction.UserId == _client.CurrentUser.Id)
            {
                return;
            }

            bool manageMessages = message.Channel is SocketGuildChannel guildChannel
                                  && guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

            bool valid = paginator.Emotes.TryGetValue(reaction.Emote, out var action)
                         && (!paginator.IsUserRestricted || paginator.Users.Any(x => x.Id == reaction.UserId));

            if (manageMessages)
            {
                switch (valid)
                {
                    case false when paginator.Deletion.HasFlag(DeletionOptions.Invalid):
                    case true when paginator.Deletion.HasFlag(DeletionOptions.Valid):
                        await message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);
                        break;
                }
            }

            if (!valid)
            {
                return;
            }

            if (action == PaginatorAction.Exit)
            {
                cancelSource.SetResult(new InteractiveMessageResult(DateTimeOffset.UtcNow - startTime, InteractiveStatus.Canceled, message));
                return;
            }

            timeoutProvider.TryReset();
            bool refreshPage = await paginator.ApplyActionAsync(action).ConfigureAwait(false);
            if (refreshPage)
            {
                var currentPage = await paginator.GetOrLoadCurrentPageAsync().ConfigureAwait(false);
                await message.ModifyAsync(x => { x.Embed = currentPage.Embed; x.Content = currentPage.Text; })
                    .ConfigureAwait(false);
            }
        }

#if DNETLABS
        private async Task HandleInteractionForPaginatorAsync(SocketInteraction interaction, Paginator paginator, IUserMessage message,
            TaskCompletionSource<InteractiveMessageResult> cancelSource, TimeoutProvider<InteractiveMessageResult> timeoutProvider,
            DateTimeOffset startTime)
        {
            if (interaction.Type != InteractionType.MessageComponent || !(interaction is SocketMessageComponent componentInteraction))
            {
                return;
            }

            if (componentInteraction.Message.Id != message.Id || interaction.User?.Id == _client.CurrentUser.Id)
            {
                return;
            }

            bool canInteract = !paginator.IsUserRestricted || paginator.Users.Any(x => x.Id == interaction.User?.Id);
            if (!canInteract)
            {
                return;
            }

            var emote = ((ButtonComponent)componentInteraction
                .Message
                .Components
                .FirstOrDefault()?
                .Components?
                .FirstOrDefault(x => x is ButtonComponent button && button.CustomId == componentInteraction.Data.CustomId))?
                .Emote;

            if (emote is null || !paginator.Emotes.TryGetValue(emote, out var action))
            {
                return;
            }

            if (action == PaginatorAction.Exit)
            {
                await interaction.AcknowledgeAsync().ConfigureAwait(false);
                cancelSource.SetResult(new InteractiveMessageResult(DateTimeOffset.UtcNow - startTime, InteractiveStatus.Canceled, message));
                return;
            }

            timeoutProvider.TryReset();
            bool refreshPage = await paginator.ApplyActionAsync(action).ConfigureAwait(false);
            if (refreshPage)
            {
                var currentPage = await paginator.GetOrLoadCurrentPageAsync().ConfigureAwait(false);
                var buttons = paginator.BuildComponents(false);

                await interaction.RespondAsync(currentPage.Text, embed: currentPage.Embed,
                    type: InteractionResponseType.UpdateMessage, component: buttons).ConfigureAwait(false);
            }
        }
#endif

        private async Task HandleMessageForSelectionAsync<TOption>(SocketMessage msg, BaseSelection<TOption> selection,
            IUserMessage message, DateTimeOffset startTime, TaskCompletionSource<InteractiveMessageResult<TOption>> selectionSource,
            TaskCompletionSource<InteractiveMessageResult<TOption>> cancelSource)
        {
            if (msg.Author.Id == _client.CurrentUser.Id || msg.Channel.Id != message.Channel.Id || msg.Source != MessageSource.User)
            {
                return;
            }

            bool manageMessages = message.Channel is SocketGuildChannel guildChannel
                                  && guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

            bool canInteract = !selection.IsUserRestricted || selection.Users.Any(x => x.Id == msg.Author.Id);
            if (!canInteract)
            {
                return;
            }

            TOption selected = default;
            string selectedString = null;
            foreach (var value in selection.Options)
            {
                string temp = selection.StringConverter(value);
                if (temp != msg.Content) continue;
                selectedString = temp;
                selected = value;
                break;
            }

            if (selectedString == null)
            {
                if (manageMessages && selection.Deletion.HasFlag(DeletionOptions.Invalid))
                {
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                return;
            }

            bool isCanceled = selection.AllowCancel && selection.StringConverter(selection.CancelOption) == selectedString;
            var result = new InteractiveMessageResult<TOption>(selected, DateTimeOffset.UtcNow - startTime,
                isCanceled ? InteractiveStatus.Canceled : InteractiveStatus.Success, message);

            if (isCanceled)
            {
                cancelSource.SetResult(result);
                return;
            }

            if (manageMessages && selection.Deletion.HasFlag(DeletionOptions.Valid))
            {
                await msg.DeleteAsync().ConfigureAwait(false);
            }

            selectionSource.SetResult(result);
        }

        private async Task HandleReactionForSelectionAsync<TOption>(SocketReaction reaction, BaseSelection<TOption> selection, IUserMessage message,
            DateTimeOffset startTime, TaskCompletionSource<InteractiveMessageResult<TOption>> selectionSource,
            TaskCompletionSource<InteractiveMessageResult<TOption>> cancelSource)
        {
            if (reaction.MessageId != message.Id || reaction.UserId == _client.CurrentUser.Id)
            {
                return;
            }

            bool manageMessages = message.Channel is SocketGuildChannel guildChannel
                                  && guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

            bool canInteract = !selection.IsUserRestricted || selection.Users.Any(x => x.Id == reaction.UserId);
            if (!canInteract)
            {
                return;
            }

            TOption selected = default;
            IEmote selectedEmote = null;
            foreach (var value in selection.Options)
            {
                var temp = selection.EmoteConverter(value);
                if (temp.Name != reaction.Emote.Name) continue;
                selectedEmote = temp;
                selected = value;
                break;
            }

            if (selectedEmote is null)
            {
                if (manageMessages && selection.Deletion.HasFlag(DeletionOptions.Invalid))
                {
                    await message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);
                }
                return;
            }

            bool isCanceled = selection.AllowCancel && selection.EmoteConverter(selection.CancelOption).Name == selectedEmote.Name;
            var result = new InteractiveMessageResult<TOption>(selected, DateTimeOffset.UtcNow - startTime,
                isCanceled ? InteractiveStatus.Canceled : InteractiveStatus.Success, message);

            if (isCanceled)
            {
                cancelSource.SetResult(result);
                return;
            }

            if (manageMessages && selection.Deletion.HasFlag(DeletionOptions.Valid))
            {
                await message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);
            }

            selectionSource.SetResult(result);
        }

#if DNETLABS
        private async Task HandleInteractionForSelectionAsync<TOption>(SocketInteraction interaction, BaseSelection<TOption> selection,
            IUserMessage message, DateTimeOffset startTime, TaskCompletionSource<InteractiveMessageResult<TOption>> selectionSource,
            TaskCompletionSource<InteractiveMessageResult<TOption>> cancelSource)
        {
            if (interaction.Type != InteractionType.MessageComponent || !(interaction is SocketMessageComponent componentInteraction))
            {
                return;
            }

            if (componentInteraction.Message.Id != message.Id || interaction.User?.Id == _client.CurrentUser.Id)
            {
                return;
            }

            bool canInteract = !selection.IsUserRestricted || selection.Users.Any(x => x.Id == interaction.User?.Id);
            if (!canInteract)
            {
                return;
            }

            TOption selected = default;
            string selectedString = null;
            string customId = selection.InputType switch
            {
                InputType.Buttons => componentInteraction.Data.CustomId,
                InputType.SelectMenus => (componentInteraction
                    .Message
                    .Components
                    .FirstOrDefault()?
                    .Components
                    .FirstOrDefault() as SelectMenu)?
                    .Options
                    .FirstOrDefault(x => x.Value == componentInteraction.Data.Values.FirstOrDefault())?
                    .Value,
                _ => null
            };

            if (customId == null)
            {
                return;
            }

            foreach (var value in selection.Options)
            {
                string stringValue = selection.EmoteConverter?.Invoke(value)?.ToString() ?? selection.StringConverter?.Invoke(value);
                if (customId != stringValue) continue;
                selected = value;
                selectedString = stringValue;
                break;
            }

            if (selectedString == null)
            {
                return;
            }

            await interaction.AcknowledgeAsync().ConfigureAwait(false);

            bool isCanceled = selection.AllowCancel
                && (selection.EmoteConverter?.Invoke(selection.CancelOption)?.ToString()
                ?? selection.StringConverter?.Invoke(selection.CancelOption)) == selectedString;

            var result = new InteractiveMessageResult<TOption>(selected, DateTimeOffset.UtcNow - startTime,
                isCanceled ? InteractiveStatus.Canceled : InteractiveStatus.Success, message);

            if (isCanceled)
            {
                cancelSource.SetResult(result);
                return;
            }

            selectionSource.SetResult(result);
        }
#endif
    }
}