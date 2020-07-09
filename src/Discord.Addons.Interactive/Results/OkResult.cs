using Discord.Commands;

namespace Discord.Addons.Interactive
{
    /// <summary>
    /// The ok result.
    /// </summary>
    public class OkResult : RuntimeResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OkResult"/> class.
        /// </summary>
        /// <param name="reason">
        /// The reason.
        /// </param>
        public OkResult(string reason = null) : base(null, reason) { }
    }
}