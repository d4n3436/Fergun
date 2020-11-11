using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// The inline reaction callback.
    /// </summary>
    public class InlineReactionCallback : IReactionCallback
    {
        private readonly InteractiveService _interactive;
        private readonly ReactionCallbackData _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="InlineReactionCallback"/> class.
        /// </summary>
        /// <param name="interactive">
        /// The interactive service.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="data">
        /// The callback data.
        /// </param>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        public InlineReactionCallback(
            InteractiveService interactive,
            SocketCommandContext context,
            ReactionCallbackData data,
            ICriterion<SocketReaction> criterion = null)
        {
            _interactive = interactive;
            Context = context;
            _data = data;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            Timeout = data.Timeout ?? TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Gets the criterion.
        /// </summary>
        public ICriterion<SocketReaction> Criterion { get; }

        /// <summary>
        /// Gets the timeout.
        /// </summary>
        public TimeSpan? Timeout { get; }

        /// <summary>
        /// Gets the context.
        /// </summary>
        public SocketCommandContext Context { get; }

        /// <summary>
        /// Gets the message.
        /// </summary>
        public IUserMessage Message { get; private set; }

        /// <summary>
        /// Displays the message and the reactions.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task DisplayAsync()
        {
            var message = await Context.Channel.SendMessageAsync(_data.Text, embed: _data.Embed).ConfigureAwait(false);
            Message = message;
            _interactive.AddReactionCallback(message, this);

            _ = Task.Run(async () =>
            {
                foreach (var item in _data.Callbacks)
                {
                    await message.AddReactionAsync(item.Reaction);
                }
                if (Timeout.HasValue)
                {
                    await Task.Delay(Timeout.Value);
                    _interactive.RemoveReactionCallback(message);
                    _data.TimeoutCallback?.Invoke(Context);
                }
            });
        }

        /// <summary>
        /// Handles the callbacks (reactions from users).
        /// </summary>
        /// <param name="reaction">
        /// The reaction.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            // If reaction is not specified in our Callback List, ignore
            var reactionCallbackItem = _data.Callbacks.FirstOrDefault(t => t.Reaction.Equals(reaction.Emote));
            if (reactionCallbackItem == null)
            {
                return false;
            }

            if (_data.SingleUsePerUser)
            {
                // Ensure that we only allow users to react a single time.
                if (!_data.ReactorIDs.Contains(reaction.UserId))
                {
                    await reactionCallbackItem.Callback(Context, reaction);
                    _data.ReactorIDs.Add(reaction.UserId);
                }
            }
            else
            {
                await reactionCallbackItem.Callback(Context, reaction);
            }

            return _data.ExpiresAfterUse;
        }
    }
}