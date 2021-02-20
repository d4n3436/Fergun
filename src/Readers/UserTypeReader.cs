using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    ///     Edited by d4n to get user by mention/id by REST.
    /// </summary>
    /// <typeparam name="T">The type to be checked; must implement <see cref="IUser"/>.</typeparam>
    public class UserTypeReader<T> : TypeReader
        where T : class, IUser
    {
        /// <inheritdoc />
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var results = new Dictionary<ulong, TypeReaderValue>();

            //By Mention or Id (1.0)
            if (MentionUtils.TryParseUser(input, out ulong id) ||
                ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id))
            {
                var user = await GetUserFromIdAsync(context, id);
                if (user != null)
                {
                    return TypeReaderResult.FromSuccess(user as T);
                }
            }

            IAsyncEnumerable<IUser> channelUsers = context.Channel.GetUsersAsync(CacheMode.CacheOnly).Flatten(); // it's better

            IReadOnlyCollection<IGuildUser> guildUsers = ImmutableArray.Create<IGuildUser>();

            if (context.Guild != null)
            {
                // If the application has guild members intent, use the search endpoint
                // Ideally, the application flags should be used here, but it returns 0 for some reason
                if ((Constants.ClientConfig.GatewayIntents & GatewayIntents.GuildMembers) == GatewayIntents.GuildMembers)
                {
                    guildUsers = await context.Guild.SearchUsersAsync(input, 10).ConfigureAwait(false);
                    foreach (var guildUser in guildUsers)
                    {
                        AddResult(results, guildUser as T, 0.60f);
                    }
                }

                guildUsers = await context.Guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false);
            }

            //By Username + Discriminator (0.7-0.8)
            int index = input.LastIndexOf('#');
            if (index >= 0)
            {
                string username = input.Substring(0, index);
                if (ushort.TryParse(input.Substring(index + 1), out ushort discriminator))
                {
                    var channelUser = await channelUsers.FirstOrDefaultAsync(x => x.DiscriminatorValue == discriminator &&
                        string.Equals(username, x.Username, StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
                    AddResult(results, channelUser as T, channelUser?.Username == username ? 0.80f : 0.70f);

                    var guildUser = guildUsers.FirstOrDefault(x => x.DiscriminatorValue == discriminator &&
                        string.Equals(username, x.Username, StringComparison.OrdinalIgnoreCase));
                    AddResult(results, guildUser as T, guildUser?.Username == username ? 0.80f : 0.70f);
                }
            }

            //By Username (0.5-0.6)
            {
                await channelUsers
                    .Where(x => string.Equals(input, x.Username, StringComparison.OrdinalIgnoreCase))
                    .ForEachAsync(channelUser => AddResult(results, channelUser as T, channelUser.Username == input ? 0.60f : 0.50f))
                    .ConfigureAwait(false);

                foreach (var guildUser in guildUsers.Where(x => string.Equals(input, x.Username, StringComparison.OrdinalIgnoreCase)))
                    AddResult(results, guildUser as T, guildUser.Username == input ? 0.60f : 0.50f);
            }

            //By Nickname (0.5-0.6)
            {
                await channelUsers
                    .Where(x => string.Equals(input, (x as IGuildUser)?.Nickname, StringComparison.OrdinalIgnoreCase))
                    .ForEachAsync(channelUser => AddResult(results, channelUser as T, (channelUser as IGuildUser).Nickname == input ? 0.60f : 0.50f))
                    .ConfigureAwait(false);

                foreach (var guildUser in guildUsers.Where(x => string.Equals(input, x.Nickname, StringComparison.OrdinalIgnoreCase)))
                    AddResult(results, guildUser as T, guildUser.Nickname == input ? 0.60f : 0.50f);
            }

            return results.Count > 0
                ? TypeReaderResult.FromSuccess(results.Values.ToImmutableArray())
                : TypeReaderResult.FromError(CommandError.ObjectNotFound, "User not found.");
        }

        private static async Task<IUser> GetUserFromIdAsync(ICommandContext context, ulong id)
        {
            IUser user;
            if (context.Guild != null)
            {
                user = await context.Guild.GetUserAsync(id)
                       ?? await (context.Client as DiscordSocketClient).Rest.GetGuildUserAsync(context.Guild.Id, id).ConfigureAwait(false);
            }
            else
            {
                user = await context.Channel.GetUserAsync(id, CacheMode.CacheOnly).ConfigureAwait(false);
            }

            return user ?? await (context.Client as DiscordSocketClient).Rest.GetUserAsync(id).ConfigureAwait(false);
        }

        private static void AddResult(IDictionary<ulong, TypeReaderValue> results, T user, float score)
        {
            if (user != null && !results.ContainsKey(user.Id))
                results.Add(user.Id, new TypeReaderValue(user, score));
        }
    }
}