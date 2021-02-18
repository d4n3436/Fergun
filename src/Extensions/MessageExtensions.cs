using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace Fergun.Extensions
{
    public static class MessageExtensions
    {
        /// <summary>
        /// Tries to delete this message.
        /// </summary>
        public static async Task<bool> TryDeleteAsync(this IMessage message)
        {
            if (message == null) return false;

            message = await message.Channel.GetMessageAsync(message.Id);

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
        public static async Task<bool> TryRemoveAllReactionsAsync(this IMessage message)
        {
            if (message == null) return false;

            // get the updated message with the reactions
            message = await message.Channel.GetMessageAsync(message.Id);

            if (message == null || message.Reactions.Count == 0) return false;

            bool manageMessages = message.Author is IGuildUser guildUser && guildUser.GetPermissions((IGuildChannel)message.Channel).ManageMessages;

            if (manageMessages)
            {
                await message.RemoveAllReactionsAsync();
            }
            else
            {
                var botReactions = message.Reactions.Where(x => x.Value.IsMe).Select(x => x.Key).ToArray();
                if (botReactions.Length == 0) return false;

                await (message as IUserMessage).RemoveReactionsAsync(message.Author, botReactions);
            }
            return true;
        }
    }
}