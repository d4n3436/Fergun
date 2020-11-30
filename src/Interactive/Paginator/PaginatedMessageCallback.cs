using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// The paginated message callback.
    /// </summary>
    internal class PaginatedMessageCallback : IReactionCallback
    {
        /// <summary>
        /// The timeout.
        /// </summary>
        public TimeSpan? Timeout => _pager.Options.Timeout;

        /// <summary>
        /// The page count.
        /// </summary>
        private readonly int _pages;

        /// <summary>
        /// The current page.
        /// </summary>
        private int _page = 1;

        /// <summary>
        /// The paginated message.
        /// </summary>
        private readonly PaginatedMessage _pager;

        private readonly List<ulong> _showingInfo = new List<ulong>();

        /// <summary>
        /// Initializes a new instance of the <see cref="PaginatedMessageCallback"/> class.
        /// </summary>
        /// <param name="interactive">
        /// The interactive service.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="pager">
        /// The pager.
        /// </param>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        public PaginatedMessageCallback(InteractiveService interactive,
            SocketCommandContext context,
            PaginatedMessage pager,
            ICriterion<SocketReaction> criterion = null)
        {
            Interactive = interactive;
            Context = context;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            _pager = pager;
            _pages = _pager.Pages?.Count() ?? default;
        }

        /// <summary>
        /// Gets the command context.
        /// </summary>
        public SocketCommandContext Context { get; }

        /// <summary>
        /// Gets the interactive service.
        /// </summary>
        public InteractiveService Interactive { get; }

        /// <summary>
        /// Gets the criterion.
        /// </summary>
        public ICriterion<SocketReaction> Criterion { get; }

        /// <summary>
        /// Gets the message.
        /// </summary>
        public IUserMessage Message { get; private set; }

        /// <summary>
        /// Sends the paginated message with the provided reaction list.
        /// </summary>
        /// <param name="reactionList">
        /// The reactions to add.
        /// </param>
        /// <param name="oldMessage">
        /// An old message to reuse.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        internal async Task DisplayAsync(ReactionList reactionList, IUserMessage oldMessage)
        {
            var embed = BuildEmbed();
            if (oldMessage == null)
            {
                Message = await Context.Channel.SendMessageAsync(_pager.Text, embed: embed).ConfigureAwait(false);
            }
            else
            {
                // Remove the old message callback
                Interactive.RemoveReactionCallback(oldMessage);
                if (oldMessage.Reactions.Count > 0)
                {
                    // There's still reactions (that means the bot doesn't have ManageMessages perms)
                    await oldMessage.DeleteAsync();
                    Message = await Context.Channel.SendMessageAsync(_pager.Text, embed: embed).ConfigureAwait(false);
                }
                else
                {
                    await oldMessage.ModifyAsync(x =>
                    {
                        x.Content = _pager.Text;
                        x.Embed = embed;
                    }).ConfigureAwait(false);

                    Message = oldMessage;
                }
            }
            if (_pages <= 1)
            {
                return;
            }
            Interactive.AddReactionCallback(Message, this);

            // reactionList take a while to add, don't wait for them
            _ = Task.Run(async () =>
            {
                if (reactionList.First) await Message.AddReactionAsync(_pager.Options.First).ConfigureAwait(false);
                if (reactionList.Backward) await Message.AddReactionAsync(_pager.Options.Back).ConfigureAwait(false);
                if (reactionList.Forward) await Message.AddReactionAsync(_pager.Options.Next).ConfigureAwait(false);
                if (reactionList.Last) await Message.AddReactionAsync(_pager.Options.Last).ConfigureAwait(false);
                if (reactionList.Jump) await Message.AddReactionAsync(_pager.Options.Jump).ConfigureAwait(false);
                if (reactionList.Stop) await Message.AddReactionAsync(_pager.Options.Stop).ConfigureAwait(false);
                if (reactionList.Info) await Message.AddReactionAsync(_pager.Options.Info).ConfigureAwait(false);
                if (Timeout.HasValue)
                {
                    await Task.Delay(Timeout.Value).ConfigureAwait(false);
                    await OnStopAsync(Message, _pager.Options.ActionOnTimeout).ConfigureAwait(false);
                }
            });
        }

        /// <summary>
        /// Executes the action in <paramref name="actionOnStop"/>.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="actionOnStop">
        /// The action to do.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        private async Task OnStopAsync(IMessage message, ActionOnTimeout actionOnStop)
        {
            if (!Interactive.ContainsKey(message.Id)) return;
            Interactive.RemoveReactionCallback(message);
            switch (actionOnStop)
            {
                case ActionOnTimeout.DeleteMessage:
                    await message.DeleteAsync().ConfigureAwait(false);
                    break;

                case ActionOnTimeout.DeleteReactions:
                    bool manageMessages = !Context.IsPrivate && Context.Guild.CurrentUser.GetPermissions((IGuildChannel)Context.Channel).ManageMessages;
                    if (manageMessages)
                    {
                        await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                    }
                    break;

                case ActionOnTimeout.Nothing:
                default:
                    break;
            }
        }

        /// <summary>
        /// Handles the reaction callback.
        /// </summary>
        /// <param name="reaction">
        /// The reaction.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            var emote = reaction.Emote;
            if (emote.Equals(_pager.Options.First))
            {
                if (_page == 1)
                    return false;
                _page = 1;
            }
            else if (emote.Equals(_pager.Options.Next))
            {
                if (_page >= _pages)
                    return false;
                ++_page;
            }
            else if (emote.Equals(_pager.Options.Back))
            {
                if (_page <= 1)
                    return false;
                --_page;
            }
            else if (emote.Equals(_pager.Options.Last))
            {
                if (_page == _pages)
                    return false;
                _page = _pages;
            }
            else if (emote.Equals(_pager.Options.Stop))
            {
                _ = OnStopAsync(Message, _pager.Options.ActionOnStop).ConfigureAwait(false);
                return false;
            }
            else if (emote.Equals(_pager.Options.Jump))
            {
                _ = Task.Run(async () =>
                {
                    var criteria = new Criteria<SocketMessage>()
                        .AddCriterion(new EnsureSourceChannelCriterion())
                        .AddCriterion(new EnsureFromUserCriterion(reaction.UserId))
                        .AddCriterion(new EnsureIsIntegerCriterion());

                    var response = await Interactive.NextMessageAsync(Context, criteria, TimeSpan.FromSeconds(15)).ConfigureAwait(false);

                    if (response == null || !int.TryParse(response.Content, out int requestedPage))
                    {
                        return;
                    }

                    _ = response.DeleteAsync().ConfigureAwait(false);

                    if (requestedPage < 1 || requestedPage == _page || requestedPage > _pages)
                    {
                        return;
                    }

                    _page = requestedPage;

                    _ = Message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);

                    await RenderAsync().ConfigureAwait(false);
                });
                return false;
            }
            else if (emote.Equals(_pager.Options.Info))
            {
                lock (_showingInfo)
                {
                    if (_showingInfo.Contains(Message.Id))
                    {
                        return false;
                    }
                }

                _ = Task.Run(async () =>
                {
                    var msg = await Context.Channel.SendMessageAsync(_pager.Options.InformationText).ConfigureAwait(false);
                    lock (_showingInfo)
                    {
                        _showingInfo.Add(Message.Id);
                    }
                    await Task.Delay(_pager.Options.InfoTimeout).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                    lock (_showingInfo)
                    {
                        _showingInfo.Remove(Message.Id);
                    }
                });

                return false;
            }
            else
            {
                return false;
            }

            _ = Message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);

            await RenderAsync().ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Builds the embed with the current page.
        /// </summary>
        /// <returns>
        /// The <see cref="Embed"/>.
        /// </returns>
        private Embed BuildEmbed()
        {
            if (_pages == 0) return _pager.Build();

            var current = _pager.Pages.ElementAt(_page - 1);
            current.Title ??= _pager.Title;
            current.Description ??= _pager.Description;
            current.Url ??= _pager.Url;
            current.ThumbnailUrl ??= _pager.ThumbnailUrl;
            current.ImageUrl ??= _pager.ImageUrl;
            current.Fields = current.Fields?.Count == 0 ? _pager.Fields : current.Fields;
            current.Timestamp ??= _pager.Timestamp;
            current.Color ??= _pager.Color;
            current.Author ??= _pager.Author;
            current.Footer ??= _pager.Footer ?? new EmbedFooterBuilder
            {
                Text = string.Format(_pager.Options.FooterFormat, _page, _pages)
            };

            return current.Build();
        }

        /// <summary>
        /// Renders an embed page.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        private Task RenderAsync() => Message.ModifyAsync(m => m.Embed = BuildEmbed());
    }
}