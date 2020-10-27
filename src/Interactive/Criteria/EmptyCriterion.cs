using System.Threading.Tasks;
using Discord.Commands;

namespace Fergun.Interactive
{
    /// <summary>
    /// A criterion that is always successful.
    /// </summary>
    /// <typeparam name="T">The type of the parameter to judge.</typeparam>
    public class EmptyCriterion<T> : ICriterion<T>
    {
        /// <inheritdoc/>
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, T parameter)
            => Task.FromResult(true);
    }
}