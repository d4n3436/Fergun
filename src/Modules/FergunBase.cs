using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
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
        /// Gets or sets the message cache service.
        /// </summary>
        public MessageCacheService MessageCache { get; set; }

        public async Task<IUserMessage> SendPaginatorAsync(Paginator paginator, TimeSpan? timeout = null)
        {
            IUserMessage response = null;
            if (Cache.TryGetValue(Context.Message.Id, out ulong messageId))
            {
                response = (IUserMessage)await Context.Channel.GetMessageAsync(MessageCache, messageId).ConfigureAwait(false);

                 await Interactive.SendPaginatorAsync(paginator, Context.Channel, timeout, response, true).ConfigureAwait(false);
            }
            else
            {
                var result = await Interactive.SendPaginatorAsync(paginator, Context.Channel, timeout, null, true).ConfigureAwait(false);

                if (!Cache.IsDisabled)
                {
                    Cache.Add(Context.Message, result.Message);
                }
            }

            return response;
        }

        /// <inheritdoc/>
        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null,
            MessageReference messageReference = null, MessageComponent component = null)
        {
            component ??= new ComponentBuilder().Build(); // remove message components if null

            if (Cache.IsDisabled)
            {
                return await base.ReplyAsync(message, isTTS, embed, options, allowedMentions, messageReference, component);
            }

            IUserMessage response;
            bool found = Cache.TryGetValue(Context.Message.Id, out ulong messageId);
            if (found && (response = (IUserMessage)await Context.Channel.GetMessageAsync(MessageCache, messageId)) != null)
            {
                await response.ModifyAsync(x =>
                {
                    x.Content = message;
                    x.Embed = embed;
                    x.AllowedMentions = allowedMentions ?? Optional.Create<AllowedMentions>();
                    x.Components = component;
                }).ConfigureAwait(false);

                response = (IUserMessage)await Context.Channel.GetMessageAsync(MessageCache, messageId).ConfigureAwait(false);
            }
            else
            {
                response = await Context.Channel.SendMessageAsync(message, isTTS, embed, options, allowedMentions, messageReference, component).ConfigureAwait(false);
                Cache.Add(Context.Message, response);
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