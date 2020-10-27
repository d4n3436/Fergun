using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;

namespace Fergun.Interactive
{
    /// <summary>
    /// Represents a collection of criterion.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Criteria<T> : ICriterion<T>
    {
        /// <summary>
        /// The criteria.
        /// </summary>
        private readonly List<ICriterion<T>> _criteria = new List<ICriterion<T>>();

        /// <summary>
        /// Adds a criterion.
        /// </summary>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        /// <returns>
        /// The criteria with the criterion added.
        /// </returns>
        public Criteria<T> AddCriterion(ICriterion<T> criterion)
        {
            _criteria.Add(criterion);
            return this;
        }

        /// <inheritdoc/>
        public async Task<bool> JudgeAsync(SocketCommandContext sourceContext, T parameter)
        {
            foreach (var criterion in _criteria)
            {
                var result = await criterion.JudgeAsync(sourceContext, parameter).ConfigureAwait(false);
                if (!result)
                {
                    return false;
                }
            }

            return true;
        }
    }
}