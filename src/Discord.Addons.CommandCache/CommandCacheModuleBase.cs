using System.Collections.Concurrent;
using System.Threading.Tasks;
using Discord.Commands;

namespace Discord.Addons.CommandCache
{
    /// <summary>
    /// An extension of <see cref="ModuleBase{T}"/> that facilitates use of any <see cref="ICommandCache{TKey, TValue}"/> implementation.
    /// </summary>
    /// <typeparam name="TCommandCache">The <see cref="ICommandCache{TKey, TValue}"/> implementation to use.</typeparam>
    /// <typeparam name="TCacheKey">The type of the cache's key.</typeparam>
    /// <typeparam name="TCacheValue">The type of the cache's value.</typeparam>
    /// <typeparam name="TCommandContext">The <see cref="ICommandContext"/> implementation to use.</typeparam>
    public abstract class CommandCacheModuleBase<TCommandCache, TCacheKey, TCacheValue, TCommandContext> : ModuleBase<TCommandContext>
        where TCommandCache : ICommandCache<TCacheKey, TCacheValue>
        where TCommandContext : class, ICommandContext
    {
        public TCommandCache Cache { get; set; }

        /// <summary>
        /// Sends a message to the channel the command was invoked in, and adds the response to the cache.
        /// </summary>
        /// <param name="message">The message's contents.</param>
        /// <param name="isTTS">Whether or not the message should use text to speech.</param>
        /// <param name="embed">The message's rich embed.</param>
        /// <param name="options">Options to modify the API request.</param>
        /// <returns>The response message that was sent.</returns>
        //protected async override Task<IUserMessage> ReplyAsync(string message, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null)
        //{
        //    var response = await Context.Channel.SendMessageAsync(message, isTTS, embed, options, allowedMentions).ConfigureAwait(false);
        //    Cache.Add(Context.Message, response);
        //    return response;
        //}
    }

    /// <summary>
    /// An extension of <see cref="ModuleBase{T}"/> that facilitates use of <see cref="CommandCacheService"/>.
    /// </summary>
    /// <typeparam name="TCommandContext">The <see cref="ICommandContext"/> implementation to use.</typeparam>
    public abstract class CommandCacheModuleBase<TCommandContext> : CommandCacheModuleBase<CommandCacheService, ulong, ulong, TCommandContext>
        where TCommandContext : class, ICommandContext
    {
        /// <summary>
        /// Sends or edits a message to the source channel, and adds the response to the cache if the message is new.
        /// </summary>
        /// <param name="message">The message's contents.</param>
        /// <param name="isTTS">Specifies if Discord should read this message aloud using text-to-speech.</param>
        /// <param name="embed">Contents of the message; optional only if embed is specified.</param>
        /// <param name="options">Options to modify the API request.</param>
        /// <param name="allowedMentions">Specifies if notifications are sent for mentioned users and roles in the message text. If null, all mentioned roles and users will be notified.</param>
        /// <returns>A task that represents the send or edit operation. The task contains the sent or edited message.</returns>
        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null)
        {
            IUserMessage response;
            bool found = Cache.TryGetValue(Context.Message.Id, out ulong messageId);
            if (found && (response = (IUserMessage)await Context.Channel.GetMessageAsync(messageId)) != null)
            {
                await response.ModifyAsync(x =>
                {
                    x.Content = message;
                    x.Embed = embed;
                }).ConfigureAwait(false);

                response = (IUserMessage)await Context.Channel.GetMessageAsync(messageId).ConfigureAwait(false);
            }
            else
            {
                response = await Context.Channel.SendMessageAsync(message, isTTS, embed, options, allowedMentions).ConfigureAwait(false);
                Cache.Add(Context.Message, response);
            }
            return response;
        }
    }
}