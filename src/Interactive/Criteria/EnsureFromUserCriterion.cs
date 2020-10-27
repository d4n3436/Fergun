using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// A criterion that ensures the user is the passed one.
    /// </summary>
    public class EnsureFromUserCriterion : ICriterion<SocketMessage>
    {
        private readonly ulong _id;

        public EnsureFromUserCriterion(ulong id) => _id = id;

        public EnsureFromUserCriterion(IUser user) => _id = user.Id;

        /// <inheritdoc/>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketMessage parameter)
            => Task.FromResult(_id == parameter.Author.Id);
    }
}