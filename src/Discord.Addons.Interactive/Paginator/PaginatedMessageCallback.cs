using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Discord.Addons.Interactive
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
        private PaginatedAppearanceOptions Options => pager.Options;

        /// <summary>
        /// The page count.
        /// </summary>
        private readonly int pages;

        /// <summary>
        /// The current page.
        /// </summary>
        private int page = 1;
        
        /// <summary>
        /// The paginated message
        /// </summary>
        private readonly PaginatedMessage pager;
        
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
            this.pager = pager;
            pages = this.pager.Pages.Count();
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
        /// Sends the embed and the reactions.
        /// </summary>
        /// <param name="reactionList">
        /// The reactions.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task DisplayAsync(ReactionList reactionList, IUserMessage oldMessage)
        {
            var embed = BuildEmbed();
            if (oldMessage == null)
            {
                Message = await Context.Channel.SendMessageAsync(pager.Content, embed: embed).ConfigureAwait(false);
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
            if (pages == 1)
            {
                return;
            }
            Interactive.AddReactionCallback(Message, this);

            // reactionList take a while to add, don't wait for them
            _ = Task.Run(async () =>
            {
                if (reactionList.First) await Message.AddReactionAsync(Options.First);
                if (reactionList.Backward) await Message.AddReactionAsync(Options.Back);
                if (reactionList.Forward) await Message.AddReactionAsync(Options.Next);
                if (reactionList.Last) await Message.AddReactionAsync(Options.Last);

                bool manageMessages = Context.Channel is IGuildChannel guildChannel &&
                                     (Context.User as IGuildUser).GetPermissions(guildChannel).ManageMessages;

                if (reactionList.Jump)
                {
                    if (Options.JumpDisplayOptions == JumpDisplayOptions.Always || (Options.JumpDisplayOptions == JumpDisplayOptions.WithManageMessages && manageMessages))
                    {
                        await Message.AddReactionAsync(Options.Jump);
                    }
                }

                if (reactionList.Stop)
                {
                    await Message.AddReactionAsync(Options.Stop);
                }

                if (reactionList.Info)
                {
                    await Message.AddReactionAsync(Options.Info);
                }
            });
            if (Timeout.HasValue)
            {
                _ = Task.Delay(Timeout.Value)
                    .ContinueWith(_ => OnStopAsync(Message, Options.ActionOnTimeout).ConfigureAwait(false))
                    .ConfigureAwait(false);
            }
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
                    await message.DeleteAsync();
                    break;
                case ActionOnTimeout.DeleteReactions:
                    bool manageMessages = message.Channel is SocketGuildChannel guildChannel &&
                             guildChannel.GetUser(message.Author.Id).GetPermissions(guildChannel).ManageMessages;
                    if (manageMessages)
                        await message.RemoveAllReactionsAsync();
                    else
                        await message.RemoveReactionsAsync(message.Author, message.Reactions.Where(x => x.Value.IsMe).Select(x => x.Key).ToArray());
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
                if (page == 1)
                    return false;
                page = 1;
            }
            else if (emote.Equals(Options.Next))
            {
                if (page >= pages)
                    return false;
                ++page;
            }
            else if (emote.Equals(Options.Back))
            {
                if (page <= 1)
                    return false;
                --page;
            }
            else if (emote.Equals(Options.Last))
            {
                if (page == pages)
                    return false;
                page = pages;
            }
            else if (emote.Equals(Options.Stop))
            {
                _ = OnStopAsync(Message, Options.ActionOnStop);
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
                    var response = await Interactive.NextMessageAsync(Context, criteria, TimeSpan.FromSeconds(15));

                    if (response == null || !int.TryParse(response.Content, out int requestedPage))
                    {
                        return;
                    }

                    _ = response.DeleteAsync().ConfigureAwait(false);
                    if (requestedPage < 1 || requestedPage == page || requestedPage > pages)
                    {
                        //_ = response.DeleteAsync().ConfigureAwait(false);
                        //await Interactive.ReplyAndDeleteAsync(Context, Options.Stop.Name);
                        return;
                    }

                    page = requestedPage;

                    _ = Message.RemoveReactionAsync(reaction.Emote, reaction.UserId);

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
                var msg = await Context.Channel.SendMessageAsync(Options.InformationText);
                ShowingInfo.Add(Message.Id);
                _ = Task.Delay(Options.InfoTimeout)
                    .ContinueWith(_ =>
                    {
                        _ = msg.DeleteAsync();
                        ShowingInfo.Remove(Message.Id);
                    }).ConfigureAwait(false);
                //await Interactive.ReplyAndDeleteAsync(Context, Options.InformationText, timeout: Options.InfoTimeout);
                return false;
            }
            else
            {
                return false;
            }

            _ = Message.RemoveReactionAsync(reaction.Emote, reaction.UserId);

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
            var current = pager.Pages.ElementAt(page - 1);

            var builder = new EmbedBuilder
            {
                Author = current.Author ?? pager.Author,
                Title = current.Title ?? pager.Title,
                Url = current.Url ?? pager.Url,
                Description = current.Description ?? pager.Description,
                ImageUrl = current.ImageUrl ?? pager.ImageUrl,
                Color = current.Color ?? pager.Color,
                Fields = current.Fields ?? pager.Fields,
                Footer = current.FooterOverride ?? pager.FooterOverride ?? new EmbedFooterBuilder
                {
                    Text = string.Format(Options.FooterFormat, page, pages)
                },
                ThumbnailUrl = current.ThumbnailUrl ?? pager.ThumbnailUrl,
                Timestamp = current.TimeStamp ?? pager.TimeStamp
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
        /// Renders an embed page
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private Task RenderAsync()
        {
            //var embed = BuildEmbed();
            return Message.ModifyAsync(m => m.Embed = BuildEmbed());
        }
    }
}