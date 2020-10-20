using Discord.Commands;

namespace Fergun
{
    public class FergunResult : RuntimeResult
    {
        public FergunResult(CommandError? error, string reason) : base(error, reason)
        {
        }

        public static FergunResult FromError(string reason) => new FergunResult(CommandError.Unsuccessful, reason);

        public static FergunResult FromSuccess(string reason = null) => new FergunResult(null, reason);
    }
}