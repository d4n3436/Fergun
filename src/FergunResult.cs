using Discord;
using Discord.Commands;

namespace Fergun
{
    public class FergunResult : RuntimeResult
    {
        /// <summary>
        /// Gets whether this result is silent.
        /// </summary>
        public bool IsSilent { get; }

        /// <summary>
        /// Gets a response message associated to this result.
        /// Currently used for detached response messages
        /// (messages that don't have an explicit user command message) in commands that use interactive messages.
        /// </summary>
        public IUserMessage ResponseMessage { get; }

        public FergunResult(CommandError? error, string reason, bool isSilent, IUserMessage responseMessage) : base(error, reason)
        {
            IsSilent = isSilent;
            ResponseMessage = responseMessage;
        }

        public static FergunResult FromError(string reason, bool isSilent = false, IUserMessage responseMessage = null)
            => new FergunResult(CommandError.Unsuccessful, reason, isSilent, responseMessage);

        public static FergunResult FromSuccess(string reason = null, bool isSilent = false, IUserMessage responseMessage = null)
            => new FergunResult(null, reason, isSilent, responseMessage);
    }
}