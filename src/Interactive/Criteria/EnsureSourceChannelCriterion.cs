using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    public class EnsureSourceChannelCriterion : ICriterion<SocketMessage>
    {
        /// <summary>
        /// Returns true if the channel is the source channel
        /// </summary>
        /// <param name="sourceContext">
        /// The source context.
        /// </param>
        /// <param name="parameter">
        /// The parameter.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketMessage parameter)
        {
            var ok = sourceContext.Channel.Id == parameter.Channel.Id;
            return Task.FromResult(ok);
        }
    }
}