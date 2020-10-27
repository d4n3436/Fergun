using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    /// <summary>
    /// A criterion that ensures the channel is the source channel.
    /// </summary>
    public class EnsureSourceChannelCriterion : ICriterion<SocketMessage>
    {
        /// <inheritdoc/>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketMessage parameter)
            => Task.FromResult(sourceContext.Channel.Id == parameter.Channel.Id);
    }
}