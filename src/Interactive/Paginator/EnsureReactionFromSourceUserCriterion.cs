using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// A criterion that ensures the reaction is from the source user.
    /// </summary>
    public class EnsureReactionFromSourceUserCriterion : ICriterion<SocketReaction>
    {
        /// <inheritdoc/>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketReaction parameter)
            => Task.FromResult(parameter.UserId == sourceContext.User.Id);
    }
}