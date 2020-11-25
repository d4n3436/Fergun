using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Readers
{
    /// <summary>
    ///     A <see cref="TypeReader"/> for parsing objects implementing <see cref="IUser"/>.
    ///     Modified from the original to get user by mention/id from REST and use the "Search Guild Members" endpoint.
    /// </summary>
    /// <typeparam name="T">The type to be checked; must implement <see cref="IUser"/>.</typeparam>
    public class UserTypeReader<T> : TypeReader
        where T : class, IUser
    {
        /// <inheritdoc />
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            //By Mention or Id (1.0)
            if (MentionUtils.TryParseUser(input, out ulong id) ||
                ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id))
            {
                var user = await GetUserFromIdAsync(context, id).ConfigureAwait(false);
                if (user != null)
                {
                    return TypeReaderResult.FromSuccess(user as T);
                }
            }

            var results = new Dictionary<ulong, TypeReaderValue>();

            var usersFromGuildSearch = context.Guild != null
                ? await context.Guild.SearchUsersAsync(input.Substring(0, Math.Min(input.Length, 100)), 10).ConfigureAwait(false)
                : Array.Empty<IGuildUser>();

            var guildUsers = context.Guild != null
                ? await context.Guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false)
                : Array.Empty<IGuildUser>();

            var channelUsers = await context.Channel.GetUsersAsync(CacheMode.CacheOnly).FlattenAsync().ConfigureAwait(false);

            var users = usersFromGuildSearch
                .Union(guildUsers, UserEqualityComparer.Instance)
                .Union(channelUsers, UserEqualityComparer.Instance)
                .ToArray();

            //By Username + Discriminator (0.7-0.8)
            int index = input.LastIndexOf('#');
            if (index >= 0 && ushort.TryParse(input.AsSpan().Slice(index + 1), out ushort discriminator))
            {
                var username = input.Substring(0, index);
                var user = users.FirstOrDefault(x => x.DiscriminatorValue == discriminator && string.Equals(username, x.Username, StringComparison.OrdinalIgnoreCase));
                if (user != null)
                {
                    AddResult(results, user as T, user.Username == username ? 0.80f : 0.70f);
                }
            }

            //By Username (0.5-0.6)
            foreach (var user in users)
            {
                if (string.Equals(input, user.Username, StringComparison.OrdinalIgnoreCase))
                {
                    AddResult(results, user as T, user.Username == input ? 0.60f : 0.50f);
                }
            }

            //By Nickname (0.5-0.6)
            foreach (var user in users)
            {
                if (user is IGuildUser guildUser && string.Equals(input, guildUser.Nickname, StringComparison.OrdinalIgnoreCase))
                {
                    AddResult(results, guildUser as T, guildUser.Nickname == input ? 0.60f : 0.50f);
                }
            }

            return results.Count > 0
                ? TypeReaderResult.FromSuccess(results.Values.ToArray())
                : TypeReaderResult.FromError(CommandError.ObjectNotFound, "User not found.");
        }

        private static async Task<IUser> GetUserFromIdAsync(ICommandContext context, ulong id)
        {
            IUser user;
            if (context.Guild != null)
            {
                user = await context.Guild.GetUserAsync(id).ConfigureAwait(false)
                    ?? await ((BaseSocketClient)context.Client).Rest.GetGuildUserAsync(context.Guild.Id, id).ConfigureAwait(false);
            }
            else
            {
                user = await context.Channel.GetUserAsync(id, CacheMode.CacheOnly).ConfigureAwait(false);
            }

            return user ?? await ((BaseSocketClient)context.Client).Rest.GetUserAsync(id).ConfigureAwait(false);
        }

        private static void AddResult(IDictionary<ulong, TypeReaderValue> results, T user, float score)
        {
            results.TryAdd(user.Id, new TypeReaderValue(user, score));
        }

        private class UserEqualityComparer : IEqualityComparer<IUser>
        {
            public static readonly UserEqualityComparer Instance = new UserEqualityComparer();

            public bool Equals(IUser x, IUser y) => x.Id == y.Id;

            public int GetHashCode(IUser obj) => obj.Id.GetHashCode();
        }
    }
}