using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <inheritdoc/>
    public class InteractiveBase : InteractiveBase<SocketCommandContext>
    {
    }

    /// <summary>
    /// The interactive base.
    /// </summary>
    public class InteractiveBase<T> : ModuleBase<T>
        where T : SocketCommandContext
    {
        /// <summary>
        /// Gets or sets the interactive service.
        /// </summary>
        public InteractiveService Interactive { get; set; }

        public Task<SocketMessage> NextMessageAsync(ICriterion<SocketMessage> criterion, TimeSpan? timeout = null)
            => Interactive.NextMessageAsync(Context, criterion, timeout);

        public Task<SocketMessage> NextMessageAsync(bool fromSourceUser = true, bool inSourceChannel = true, TimeSpan? timeout = null)
            => Interactive.NextMessageAsync(Context, fromSourceUser, inSourceChannel, timeout);

        public Task<IUserMessage> ReplyAndDeleteAsync(string content, bool isTTS = false, Embed embed = null, TimeSpan? timeout = null, RequestOptions options = null)
            => Interactive.ReplyAndDeleteAsync(Context, content, isTTS, embed, timeout, options);

        public Task<IUserMessage> InlineReactionReplyAsync(ReactionCallbackData data, bool fromSourceUser = true)
            => Interactive.SendMessageWithReactionCallbacksAsync(Context, data, fromSourceUser);

        public Task<IUserMessage> PagedReplyAsync(PaginatedMessage pager, ReactionList reactions, bool fromSourceUser = true, string notCommandUserText = null)
        {
            var criterion = new Criteria<SocketInteraction>();
            if (fromSourceUser)
                criterion.AddCriterion(new EnsureInteractionFromSourceUserCriterion());
            return PagedReplyAsync(pager, criterion, reactions, notCommandUserText);
        }

        public Task<IUserMessage> PagedReplyAsync(PaginatedMessage pager, ICriterion<SocketInteraction> criterion, ReactionList reactions, string notCommandUserText)
            => Interactive.SendPaginatedMessageAsync(Context, pager, reactions, criterion);

        public Task<SocketInteraction> NextInteractionAsync(Func<SocketInteraction, bool> filter = null, TimeSpan? timeout = null)
            => Interactive.NextInteractionAsync(filter, timeout);

        public RuntimeResult Ok(string reason = null) => new OkResult(reason);
    }
}