using Discord.Commands;

namespace Fergun
{
    public class FergunResult : RuntimeResult
    {
        /// <summary>
        /// Gets whether this result is silent.
        /// </summary>
        public bool IsSilent { get; }

        public FergunResult(CommandError? error, string reason, bool isSilent) : base(error, reason)
        {
            IsSilent = isSilent;
        }

        public static FergunResult FromError(string reason, bool isSilent = false) => new FergunResult(CommandError.Unsuccessful, reason, isSilent);

        public static FergunResult FromSuccess(string reason = null, bool isSilent = false) => new FergunResult(null, reason, isSilent);
    }
}