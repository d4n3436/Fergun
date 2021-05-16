using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Fergun.Services;
using Fergun.Utils;

namespace Fergun.Extensions
{
    public static class ChannelExtensions
    {
        private static readonly Regex _linkRegex = new Regex(
            @"^(http:\/\/www\.|https:\/\/www\.|http:\/\/|https:\/\/)?[a-z0-9]+([\-\.]{1}[a-z0-9]+)*\.[a-z]{2,5}(:[0-9]{1,5})?(\/.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Tries to delete a message.
        /// </summary>
        /// <param name="channel">The source channel.</param>
        /// <param name="message">The message.</param>
        /// <param name="cache">The message cache service.</param>
        public static async Task<bool> TryDeleteMessageAsync(this IMessageChannel channel, IMessage message, MessageCacheService cache = null)
            => await channel.TryDeleteMessageAsync(message.Id, cache);

        /// <summary>
        /// Tries to delete a message.
        /// </summary>
        /// <param name="channel">The source channel.</param>
        /// <param name="messageId">The message Id.</param>
        /// <param name="cache">The message cache service.</param>
        public static async Task<bool> TryDeleteMessageAsync(this IMessageChannel channel, ulong messageId, MessageCacheService cache = null)
        {
            var message = await channel.GetMessageAsync(cache, messageId);
            return await message.TryDeleteAsync();
        }

        public static bool IsPrivate(this IMessageChannel channel) => channel is IPrivateChannel;

        /// <summary>
        /// Gets the last url in the last <paramref name="messageCount"/> messages.
        /// </summary>
        /// <param name="channel">The channel to search.</param>
        /// <param name="messageCount">The number of messages to search.</param>
        /// <param name="cache">The message cache service.</param>
        /// <param name="onlyImage">Get only urls of images.</param>
        /// <param name="message">An optional message to search first before searching in the channel.</param>
        /// <param name="url">An optional url to use before searching in the channel.</param>
        /// <param name="maxSize">The maximum file size in bytes, <see cref="Constants.AttachmentSizeLimit"/> by default.</param>
        /// <returns>A task that represents an asynchronous search operation.</returns>
        public static async Task<(string url, UrlFindResult result)> GetLastUrlAsync(this IMessageChannel channel,  int messageCount,
            MessageCacheService cache = null, bool onlyImage = false, IMessage message = null, string url = null, long maxSize = Constants.AttachmentSizeLimit)
        {
            long? size = null;
            if (message != null && message.Attachments.Count > 0)
            {
                var attachment = message.Attachments.First();
                if (onlyImage && attachment.Width == null && attachment.Height == null)
                {
                    return (null, UrlFindResult.AttachmentNotImage);
                }
                url = attachment.Url;
            }
            if (url != null)
            {
                if (onlyImage && !await StringUtils.IsImageUrlAsync(url))
                {
                    return (null, UrlFindResult.UrlNotImage);
                }
                size = await StringUtils.GetUrlContentLengthAsync(url);
                return size > maxSize ? (null, UrlFindResult.UrlFileTooLarge) : (url, UrlFindResult.UrlFound);
            }

            // Get the last x messages of the current channel
            var messages = await channel.GetMessagesAsync(cache, messageCount).FlattenAsync();

            // Try to get the last message with any attachment, embed image url or that contains a url
            var filtered = messages.FirstOrDefault(x =>
            x.Attachments.Any(y => !onlyImage || y.Width != null && y.Height != null)
            || x.Embeds.Any(y => !onlyImage || y.Image != null || y.Thumbnail != null)
            || _linkRegex.IsMatch(x.Content));

            // No results
            if (filtered == null)
            {
                return (null, UrlFindResult.UrlNotFound);
            }

            // Note: attachments and embeds can contain text but I'm prioritizing the previous ones
            // Priority order: attachments > embeds > text (message content)
            if (filtered.Attachments.Count > 0)
            {
                url = filtered.Attachments.First().Url;
                size = filtered.Attachments.First().Size;
            }
            else if (filtered.Embeds.Count > 0)
            {
                var embed = filtered.Embeds.First();
                var image = embed.Image;
                var thumbnail = embed.Thumbnail;
                if (onlyImage)
                {
                    if (image?.Height != null && image.Value.Width != null)
                    {
                        url = image.Value.Url;
                    }
                    else if (thumbnail?.Height != null && thumbnail.Value.Width != null)
                    {
                        url = thumbnail.Value.Url;
                    }
                    else
                    {
                        return (null, UrlFindResult.UrlNotFound);
                    }

                    // the image can still be invalid
                    if (!await StringUtils.IsImageUrlAsync(url))
                    {
                        return (null, UrlFindResult.UrlNotFound);
                    }
                }
                else
                {
                    url = embed.Url ?? image?.Url ?? thumbnail?.Url;
                }
            }
            else
            {
                string match = _linkRegex.Match(filtered.Content).Value;
                if (onlyImage && !await StringUtils.IsImageUrlAsync(match))
                {
                    return (null, UrlFindResult.UrlNotFound);
                }
                url = match;
            }
            if (filtered.Attachments.Count == 0)
            {
                size = await StringUtils.GetUrlContentLengthAsync(url);
            }

            return size > maxSize ? (null, UrlFindResult.UrlFileTooLarge) : (url, UrlFindResult.UrlFound);
        }
    }

    public enum UrlFindResult
    {
        UrlFound,
        UrlNotFound,
        UrlNotImage,
        UrlFileTooLarge,
        AttachmentNotImage
    }
}