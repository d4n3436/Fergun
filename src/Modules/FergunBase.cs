using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Services;
using Fergun.Utils;

namespace Fergun.Modules
{
    /// <inheritdoc/>
    public abstract class FergunBase : FergunBase<SocketCommandContext>
    {
    }

    /// <summary>
    /// The command module base that Fergun uses in its modules.
    /// </summary>
    public abstract class FergunBase<T> : CommandCacheModuleBase<T>
        where T : SocketCommandContext
    {
        /// <summary>
        /// Gets or sets the interactive service.
        /// </summary>
        public InteractiveService Interactive { get; set; }

        /// <summary>
        /// Waits for the next message in the source channel.
        /// </summary>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        /// <param name="timeout">
        /// The timeout.
        /// </param>
        /// <returns>
        /// A task representing the wait operation. The result contains the message, or <c>null</c> if no message was sent before the timeout.
        /// </returns>
        public Task<SocketMessage> NextMessageAsync(ICriterion<SocketMessage> criterion, TimeSpan? timeout = null)
            => Interactive.NextMessageAsync(Context, criterion, timeout);

        /// <summary>
        /// Waits for the next message in the channel.
        /// </summary>
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
        /// A task representing the wait operation. The result contains the message, or null if no message was sent before the timeout.
        /// </returns>
        public Task<SocketMessage> NextMessageAsync(bool fromSourceUser = true, bool inSourceChannel = true, TimeSpan? timeout = null)
            => Interactive.NextMessageAsync(Context, fromSourceUser, inSourceChannel, timeout);

        /// <summary>
        /// Sends a message to the source channel and then deletes the message after the provided timeout.
        /// </summary>
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
        public Task<IUserMessage> ReplyAndDeleteAsync(string text, bool isTTS = false, Embed embed = null, TimeSpan? timeout = null, RequestOptions options = null)
            => Interactive.ReplyAndDeleteAsync(Context, text, isTTS, embed, timeout, options);

        /// <summary>
        /// Sends a message with reaction callbacks to the source channel.
        /// </summary>
        /// <param name="callbackData">
        /// The callback data.
        /// </param>
        /// <param name="fromSourceUser">
        /// Determines whether the user have to be the source user or not.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. The result contains the message.
        /// </returns>
        public async Task<IUserMessage> InlineReactionReplyAsync(ReactionCallbackData callbackData, bool fromSourceUser = true)
        {
            var response = await Interactive.SendMessageWithReactionCallbacksAsync(Context, callbackData, fromSourceUser);

            if (!Cache.IsDisabled)
            {
                Cache.Add(Context.Message, response);
            }

            return response;
        }

        /// <summary>
        /// Sends a paginated message to the source channel.
        /// </summary>
        /// <param name="pager">
        /// The pager.
        /// </param>
        /// <param name="reactions">
        /// The reactions.
        /// </param>
        /// <param name="fromSourceUser">
        /// Determines whether the user have to be the source user or not.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. The result contains the message.
        /// </returns>
        public Task<IUserMessage> PagedReplyAsync(PaginatedMessage pager, ReactionList reactions, bool fromSourceUser = true)
        {
            var criterion = new Criteria<SocketReaction>();
            if (fromSourceUser)
                criterion.AddCriterion(new EnsureReactionFromSourceUserCriterion());
            return PagedReplyAsync(pager, reactions, criterion);
        }

        /// <summary>
        /// Sends a paginated message to the source channel.
        /// </summary>
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
        /// A task representing the asynchronous operation. The result contains the message.
        /// </returns>
        public async Task<IUserMessage> PagedReplyAsync(PaginatedMessage pager, ReactionList reactions, ICriterion<SocketReaction> criterion)
        {
            IUserMessage response;
            if (Cache.TryGetValue(Context.Message.Id, out ulong messageId))
            {
                response = (IUserMessage)await Context.Channel.GetMessageAsync(messageId).ConfigureAwait(false);

                response = await Interactive.SendPaginatedMessageAsync(Context, pager, reactions, criterion, response).ConfigureAwait(false);
            }
            else
            {
                response = await Interactive.SendPaginatedMessageAsync(Context, pager, reactions, criterion).ConfigureAwait(false);

                if (!Cache.IsDisabled)
                {
                    Cache.Add(Context.Message, response);
                }
            }

            return response;
        }

        /// <summary>
        /// Returns the prefix of the source channel.
        /// </summary>
        /// <returns>The prefix of the channel.</returns>
        public string GetPrefix() => GuildUtils.GetPrefix(Context.Channel);

        /// <summary>
        /// Returns the configuration of a guild using the source channel.
        /// </summary>
        /// <returns>The configuration of the guild, or <c>null</c> if the guild cannot be found in the database.</returns>
        public GuildConfig GetGuildConfig() => GuildUtils.GetGuildConfig(Context.Channel);

        /// <summary>
        /// Returns the language of the source channel.
        /// </summary>
        /// <returns>The language of the source channel.</returns>
        public string GetLanguage() => GuildUtils.GetLanguage(Context.Channel);

        /// <summary>
        /// Returns the localized value of a resource key.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        /// <returns>The localized text, or <paramref name="key"/> if the value cannot be found.</returns>
        public string Locate(string key) => GuildUtils.Locate(key, Context.Channel);

        /// <summary>
        /// Returns the localized value of a boolean.
        /// </summary>
        /// <param name="boolean">The boolean to localize.</param>
        /// <returns>The localized boolean.</returns>
        public string Locate(bool boolean) => GuildUtils.Locate(boolean ? "Yes" : "No", Context.Channel);

        /// <summary>
        /// Returns the localized value of a resource key in the specified language.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        /// <param name="language">The language to localize the resource key.</param>
        /// <returns>The localized text, or <paramref name="key"/> if the value cannot be found.</returns>
        public string Locate(string key, string language) => GuildUtils.Locate(key, language);

        /// <summary>
        /// Sends or edits an embed to the source channel, and adds the response to the cache if the message is new.
        /// </summary>
        /// <param name="text">The embed description.</param>
        /// <returns>A task that represents the send or edit operation. The task contains the sent or edited message.</returns>
        public async Task<IUserMessage> SendEmbedAsync(string text)
        {
            var builder = new EmbedBuilder()
                .WithDescription(text)
                .WithColor(FergunClient.Config.EmbedColor);

            return await ReplyAsync(embed: builder.Build());
        }
    }
}