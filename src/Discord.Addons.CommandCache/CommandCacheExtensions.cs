using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;

namespace Discord.Addons.CommandCache
{
    public static class CommandCacheExtensions
    {
        /// <summary>
        /// Initialises and adds a command cache to the dependency map.
        /// </summary>
        /// <param name="services">The IServiceCollection that the service should be added to.</param>
        /// <param name="capacity">The maximum capacity of the cache. Must be a number greater than 0 or CommandCacheService.UNLIMITED.</param>
        /// <param name="log">A method to use for logging.</param>
        /// <returns>The client that this method was called on.</returns>
        public static DiscordSocketClient UseCommandCache(this DiscordSocketClient client, IServiceCollection services, int capacity = 200,
            Func<SocketMessage, Task> cmdHandler = null, Func<LogMessage, Task> log = null)
        {
            services.AddSingleton(new CommandCacheService(client, capacity, cmdHandler, log));
            return client;
        }

        /// <summary>
        /// Sends a message to a channel, then adds it to the command cache.
        /// </summary>
        /// <param name="cache">The command cache that the messages should be added to.</param>
        /// <param name="commandId">The ID of the command message.</param>
        /// <param name="text">The content of the message.</param>
        /// <param name="prependZWSP">Whether or not to prepend the message with a zero-width space.</param>
        /// <returns>The message that was sent.</returns>
        public static async Task<IUserMessage> SendCachedMessageAsync(this IMessageChannel channel, CommandCacheService cache, ulong commandId, string text, bool prependZWSP = false)
        {
            var message = await channel.SendMessageAsync(prependZWSP ? "\x200b" + text : text);
            cache.Add(commandId, message.Id);

            return message;
        }


        /// <summary>
        /// Sends a file to this message channel with an optional caption, then adds it to the command cache.
        /// </summary>
        /// <param name="cache">The command cache that the messages should be added to.</param>
        /// <param name="commandId">The ID of the command message.</param>
        /// <param name="stream">The <see cref="Stream" /> of the file to be sent.</param>
        /// <param name="text">The message to be sent.</param>
        /// <param name="isTTS">Whether the message should be read aloud by Discord or not.</param>
        /// <param name="embed">The <see cref="Discord.EmbedType.Rich" /> <see cref="Embed" /> to be sent.</param>
        /// <param name="options">The options to be used when sending the request.</param>
        /// <param name="isSpoiler">Whether the message attachment should be hidden as a spoiler.</param>
        /// <param name="allowedMentions">
        /// Specifies if notifications are sent for mentioned users and roles in the message <paramref name="text"/>. If <c>null</c>, all mentioned roles and users will be notified.
        /// </param>
        /// <returns>
        /// A task that represents an asynchronous send operation for delivering the message. The task result contains the sent or edited message.
        /// </returns>
        public static async Task<IUserMessage> SendCachedFileAsync(this IMessageChannel channel, CommandCacheService cache, ulong commandId, Stream stream, string filename,
            string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null)
        {
            var response = await channel.SendFileAsync(stream, filename, text, isTTS, embed, options, isSpoiler, allowedMentions);

            if (cache.ContainsKey(commandId))
            {
                cache.Remove(commandId);
            }
            cache.Add(commandId, response.Id);

            return response;
        }

        /// <summary>
        /// Adds multiple values to a ConcurrentBag.
        /// </summary>
        /// <typeparam name="T">The type of values contained in the bag.</typeparam>
        /// <param name="values">The values to add.</param>
        internal static ConcurrentBag<T> AddMany<T>(this ConcurrentBag<T> bag, IEnumerable<T> values)
        {
            foreach (T item in values)
            {
                bag.Add(item);
            }
            return bag;
        }
    }
}