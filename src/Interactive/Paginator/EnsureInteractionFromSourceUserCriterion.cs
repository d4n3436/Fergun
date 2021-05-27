using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// A criterion that ensures the interaction is from the source user.
    /// </summary>
    public class EnsureInteractionFromSourceUserCriterion : ICriterion<SocketInteraction>
    {
        /// <inheritdoc/>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketInteraction parameter)
            => Task.FromResult(parameter.User?.Id == sourceContext.User.Id);
    }
}