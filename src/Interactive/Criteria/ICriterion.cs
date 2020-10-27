using System.Threading.Tasks;
using Discord.Commands;

namespace Fergun.Interactive
{
    /// <summary>
    /// The base of all criteria.
    /// </summary>
    /// <typeparam name="T">The type of the parameter to judge.</typeparam>
    public interface ICriterion<in T>
    {
        /// <summary>
        /// Ensures that all the criteria are successful.
        /// </summary>
        /// <param name="sourceContext">
        /// The source context.
        /// </param>
        /// <param name="parameter">
        /// The parameter to judge.
        /// </param>
        /// <returns>
        /// A task that represents the operation.
        /// </returns>
        Task<bool> JudgeAsync(SocketCommandContext sourceContext, T parameter);
    }
}