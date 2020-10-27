using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// A criterion that ensures the message content is an integer.
    /// </summary>
    public class EnsureIsIntegerCriterion : ICriterion<SocketMessage>
    {
        /// <inheritdoc/>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketMessage parameter)
            => Task.FromResult(int.TryParse(parameter.Content, out _));
    }
}