using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace Fergun.Extensions
{
    public static class IMessageExtension
    {
        /// <summary>
        /// Try to delete a message.
        /// </summary>
        /// <param name="messageId">The message Id.</param>
        public static async Task<bool> TryDeleteMessageAsync(this ISocketMessageChannel channel, ulong messageId)
        {
            var msg = await channel.GetMessageAsync(messageId);
            return await TryDeleteMessageInternalAsync(msg);
        }

        /// <summary>
        /// Try to delete a message.
        /// </summary>
        /// <param name="message">The message.</param>
        public static async Task<bool> TryDeleteMessageAsync(this ISocketMessageChannel channel, IMessage message)
        {
            var msg = await channel.GetMessageAsync(message.Id);
            return await TryDeleteMessageInternalAsync(msg);
        }

        /// <summary>
        /// Try to delete this message.
        /// </summary>
        public static async Task<bool> TryDeleteAsync(this IMessage message)
        {
            if (message == null) return false;
            var msg = await message.Channel.GetMessageAsync(message.Id);
            return await TryDeleteMessageInternalAsync(msg);
        }

        internal static async Task<bool> TryDeleteMessageInternalAsync(IMessage message)
        {
            if (message == null)
            {
                // The message is already deleted.
                return false;
            }

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

        public static async Task TryRemoveAllReactionsAsync(this IMessage message)
        {
            if (message == null) return;

            // get the updated message with the reactions
            message = await message.Channel.GetMessageAsync(message.Id);
            if (message.Reactions.Count == 0) return;

            bool manageMessages = message.Author is IGuildUser guildUser && guildUser.GetPermissions((IGuildChannel)message.Channel).ManageMessages;

            if (manageMessages)
                await message.RemoveAllReactionsAsync();
            else
                await (message as IUserMessage).RemoveReactionsAsync(message.Author, message.Reactions.Where(x => x.Value.IsMe).Select(x => x.Key).ToArray());
        }
    }
}