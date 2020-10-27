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
    public class PaginatedMessageCallback : IReactionCallback
    {
        /// <summary>
        /// The run mode.
        /// </summary>
        public RunMode RunMode => RunMode.Sync;

        /// <summary>
        /// The timeout.
        /// </summary>
        public TimeSpan? Timeout => Options.Timeout;

        /// <summary>
        /// The options.
        /// </summary>
        private PaginatedAppearanceOptions Options => _pager.Options;

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
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PaginatedMessageCallback"/> class.
        /// </summary>
        /// <param name="interactive">
        /// The interactive.
        /// </param>
        /// <param name="sourceContext">
        /// The source context.
        /// </param>
        /// <param name="pager">
        /// The pager.
        /// </param>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        public PaginatedMessageCallback(InteractiveService interactive, 
            SocketCommandContext sourceContext,
            PaginatedMessage pager,
            ICriterion<SocketReaction> criterion = null)
        {
            Interactive = interactive;
            Context = sourceContext;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            _pager = pager;
            _pages = _pager.Pages.Count();
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

        public List<ulong> ShowingInfo { get; private set; } = new List<ulong>();

        /// <summary>
        /// Sends the paginated message with the provided reaction list.
        /// </summary>
        /// <param name="reactionList">
        /// The reactions to add.
        /// </param>
        /// <param name="oldMessage">
        /// An old message to use.
        /// </param>
        public async Task DisplayAsync(ReactionList reactionList, IUserMessage oldMessage)
        {
            var embed = BuildEmbed();
            if (oldMessage == null)
            {
                Message = await Context.Channel.SendMessageAsync(_pager.Content, embed: embed).ConfigureAwait(false);
            }
            else
            {
                // TODO: A better way to update the reactions
                //await OnStopAsync(oldMessage, ActionOnTimeout.DeleteReactions);
                Interactive.RemoveReactionCallback(oldMessage);

                await oldMessage.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = embed;
                }).ConfigureAwait(false);
                Message = (IUserMessage)await Context.Channel.GetMessageAsync(oldMessage.Id).ConfigureAwait(false);
            }
            //this.Message = Message;
            if (_pages == 1)
            {
                return;
            }
            Interactive.AddReactionCallback(Message, this);

            // reactionList take a while to add, don't wait for them
            _ = Task.Run(async () =>
            {
                if (reactionList.First) await Message.AddReactionAsync(Options.First).ConfigureAwait(false);
                if (reactionList.Backward) await Message.AddReactionAsync(Options.Back).ConfigureAwait(false);
                if (reactionList.Forward) await Message.AddReactionAsync(Options.Next).ConfigureAwait(false);
                if (reactionList.Last) await Message.AddReactionAsync(Options.Last).ConfigureAwait(false);

                bool manageMessages = Context.Channel is IGuildChannel guildChannel &&
                                     (Context.User as IGuildUser).GetPermissions(guildChannel).ManageMessages;

                if (reactionList.Jump)
                {
                    if (Options.JumpDisplayOptions == JumpDisplayOption.Always || (Options.JumpDisplayOptions == JumpDisplayOption.WithManageMessages && manageMessages))
                    {
                        await Message.AddReactionAsync(Options.Jump).ConfigureAwait(false);
                    }
                }

                if (reactionList.Stop)
                {
                    await Message.AddReactionAsync(Options.Stop).ConfigureAwait(false);
                }

                if (reactionList.Info)
                {
                    await Message.AddReactionAsync(Options.Info).ConfigureAwait(false);
                }

                if (Timeout.HasValue)
                {
                    await Task.Delay(Timeout.Value).ConfigureAwait(false);
                    await OnStopAsync(Message, Options.ActionOnTimeout).ConfigureAwait(false);
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
        public async Task OnStopAsync(IUserMessage message, ActionOnTimeout actionOnStop)
        {
            Interactive.RemoveReactionCallback(message);
            switch (actionOnStop)
            {
                case ActionOnTimeout.DeleteMessage:
                    await message.DeleteAsync().ConfigureAwait(false);
                    break;
                case ActionOnTimeout.DeleteReactions:
                    bool manageMessages = message.Channel is SocketGuildChannel guildChannel &&
                             guildChannel.GetUser(message.Author.Id).GetPermissions(guildChannel).ManageMessages;
                    if (manageMessages)
                        await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                    else
                        await message.RemoveReactionsAsync(message.Author, message.Reactions.Where(x => x.Value.IsMe).Select(x => x.Key).ToArray()).ConfigureAwait(false);
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
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            //bool render = true;
            var emote = reaction.Emote;
            if (emote.Equals(Options.First))
            {
                if (_page == 1)
                    return false;
                _page = 1;
            }
            else if (emote.Equals(Options.Next))
            {
                if (_page >= _pages)
                    return false;
                ++_page;
            }
            else if (emote.Equals(Options.Back))
            {
                if (_page <= 1)
                    return false;
                --_page;
            }
            else if (emote.Equals(Options.Last))
            {
                if (_page == _pages)
                    return false;
                _page = _pages;
            }
            else if (emote.Equals(Options.Stop))
            {
                _ = OnStopAsync(Message, Options.ActionOnStop).ConfigureAwait(false);
                return false;
            }
            else if (emote.Equals(Options.Jump))
            {
                //render = false;
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
                        //_ = response.DeleteAsync().ConfigureAwait(false);
                        //await Interactive.ReplyAndDeleteAsync(Context, Options.Stop.Name);
                        return;
                    }

                    _page = requestedPage;

                    _ = Message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);

                    await RenderAsync().ConfigureAwait(false);
                });
                return false;
            }
            else if (emote.Equals(Options.Info))
            {
                if (ShowingInfo.Contains(Message.Id))
                {
                    return false;
                }
                var msg = await Context.Channel.SendMessageAsync(Options.InformationText).ConfigureAwait(false);
                ShowingInfo.Add(Message.Id);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(Options.InfoTimeout).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                    ShowingInfo.Remove(Message.Id);
                });

                //await Interactive.ReplyAndDeleteAsync(Context, Options.InformationText, timeout: Options.InfoTimeout);
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
        protected Embed BuildEmbed()
        {
            var current = _pager.Pages.ElementAt(_page - 1);

            var builder = new EmbedBuilder
            {
                Author = current.Author ?? _pager.Author,
                Title = current.Title ?? _pager.Title,
                Url = current.Url ?? _pager.Url,
                Description = current.Description ?? _pager.Description,
                ImageUrl = current.ImageUrl ?? _pager.ImageUrl,
                Color = current.Color ?? _pager.Color,
                Fields = current.Fields ?? _pager.Fields,
                Footer = current.FooterOverride ?? _pager.FooterOverride ?? new EmbedFooterBuilder
                {
                    Text = string.Format(Options.FooterFormat, _page, _pages)
                },
                ThumbnailUrl = current.ThumbnailUrl ?? _pager.ThumbnailUrl,
                Timestamp = current.TimeStamp ?? _pager.TimeStamp
            };

            /*var builder = new EmbedBuilder()
                .WithAuthor(pager.Author)
                .WithColor(pager.Color)
                .WithDescription(pager.Pages.ElementAt(page - 1).Description)
                .WithImageUrl(current.ImageUrl ?? pager.DefaultImageUrl)
                .WithUrl(current.Url)
                .WithFooter(f => f.Text = string.Format(options.FooterFormat, page, pages))
                .WithTitle(current.Title ?? pager.Title);*/
            //builder.Fields = pager.Pages.ElementAt(page - 1).Fields;
            builder.Fields = current.Fields;

            return builder.Build();
        }

        /// <summary>
        /// Renders an embed page.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private Task RenderAsync() => Message.ModifyAsync(m => m.Embed = BuildEmbed());
    }
}