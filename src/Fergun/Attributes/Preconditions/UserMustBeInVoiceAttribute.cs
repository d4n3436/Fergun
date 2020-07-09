using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Fergun.Attributes.Preconditions
{
    /// <summary>
    ///     Indicates that this command can only be used while
    ///     the user is in a voice channel in the same Guild.
    ///     This precondition automatically applies <see cref="RequireContextAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class UserMustBeInVoiceAttribute : RequireContextAttribute
    {
        /// <inheritdoc />
        public override string ErrorMessage { get; set; }

        public UserMustBeInVoiceAttribute()
            : base(ContextType.Guild)
        {
        }

        /// <inheritdoc />
        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var baseResult = await base.CheckPermissionsAsync(context, command, services);
            if (!baseResult.IsSuccess)
                return baseResult;

            if (command.Name == "lyrics")
                return PreconditionResult.FromSuccess();

            var current = (context.User as IVoiceState)?.VoiceChannel?.Id;
            return (await context.Guild.GetVoiceChannelsAsync()).Any(v => v.Id == current)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(ErrorMessage ?? "Command must be invoked while in a voice channel in this guild.");
        }
    }
}
