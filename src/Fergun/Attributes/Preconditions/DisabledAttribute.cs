using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace Fergun.Attributes.Preconditions
{
    /// <summary>
    /// Disables the command or module globally or on a specific guild.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class DisabledAttribute : RequireContextAttribute
    {
        public DisabledAttribute() : base(ContextType.Guild)
        {
        }

        public DisabledAttribute(params ulong[] guildIds) : this()
        {
             _guildIds = guildIds;
        }

        /// <inheritdoc />
        public override string ErrorMessage { get; set; }

        private readonly ulong[] _guildIds = Array.Empty<ulong>();

        /// <inheritdoc />
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo _, IServiceProvider _1)
        {
            return _guildIds.Length == 0 || Array.Exists(_guildIds, x => x == context.Guild.Id)
                ? Task.FromResult(PreconditionResult.FromError(ErrorMessage ?? "Disabled command / module."))
                : Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}