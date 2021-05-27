using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Fergun.Services;

namespace Fergun.Extensions
{
    public static class MessageExtensions
    {
        /// <summary>
        /// Tries to delete this message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cache">The message cache service.</param>
        public static async Task<bool> TryDeleteAsync(this IMessage message, MessageCacheService cache = null)
        {
            if (message == null) return false;

            message = await message.Channel.GetMessageAsync(cache, message.Id);

            if (message == null) return false;

            if (message.Channel is SocketGuildChannel guildChannel)
            {
                if (!guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages)
                {
                    // Missing permissions
                    return false;
                }
            }
            else
            {
                // Not possible to delete other user's messages in DM
                if (message.Source == MessageSource.User) return false;
            }

            try
            {
                await message.DeleteAsync();
                return true;
            }
            catch (HttpException)
            {
                return false;
            }
        }

        /// <summary>
        /// Tries to remove all reactions from this message.
        /// </summary>
        public static async Task<bool> TryRemoveAllReactionsAsync(this IMessage message, MessageCacheService cache = null)
        {
            if (message == null) return false;

            // get the updated message with the reactions
            message = await message.Channel.GetMessageAsync(cache, message.Id);

            if (message == null || message.Reactions.Count == 0) return false;

            bool manageMessages = message.Channel is SocketGuildChannel guildChannel &&
                                  guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

            if (!manageMessages) return false;

            await message.RemoveAllReactionsAsync();
            return true;

        }

        /// <summary>
        /// Modifies this message or re-sends it if no longer exists.
        /// </summary>
        /// <returns>A new message or a modified one.</returns>
        public static async Task<IUserMessage> ModifyOrResendAsync(this IUserMessage message, string content = null, Embed embed = null,
            AllowedMentions allowedMentions = null, MessageCacheService cache = null)
        {
            bool isValid = await message.Channel.GetMessageAsync(cache, message.Id) != null;
            if (!isValid)
            {
                return await message.Channel.SendMessageAsync(content, embed: embed, allowedMentions: allowedMentions);
            }

            await message.ModifyAsync(x =>
            {
                x.Content = content;
                x.Embed = embed;
                //x.AllowedMentions = allowedMentions ?? Optional.Create<AllowedMentions>();
            });

            return await message.Channel.GetMessageAsync(cache, message.Id) as IUserMessage;
        }
    }
}