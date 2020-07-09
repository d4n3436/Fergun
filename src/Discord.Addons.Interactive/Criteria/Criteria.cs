using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;

namespace Discord.Addons.Interactive
{
    public class Criteria<T> : ICriterion<T>
    {
        /// <summary>
        /// The criteria.
        /// </summary>
        private readonly List<ICriterion<T>> criteria = new List<ICriterion<T>>();

        /// <summary>
        /// adds a criterion
        /// </summary>
        /// <param name="criterion">
        /// The criterion.
        /// </param>
        /// <returns>
        /// The <see cref="criterion"/>.
        /// </returns>
        public Criteria<T> AddCriterion(ICriterion<T> criterion)
        {
            criteria.Add(criterion);
            return this;
        }

        /// <summary>
        /// The judge async.
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
        public async Task<bool> JudgeAsync(SocketCommandContext sourceContext, T parameter)
        {
            foreach (var criterion in criteria)
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