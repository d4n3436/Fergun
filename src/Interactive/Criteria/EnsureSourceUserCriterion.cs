using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// A criterion that ensures the user is the source user.
    /// </summary>
    public class EnsureSourceUserCriterion : ICriterion<SocketMessage>
    {
        /// <inheritdoc/>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketMessage parameter)
            => Task.FromResult(sourceContext.User.Id == parameter.Author.Id);
    }
}