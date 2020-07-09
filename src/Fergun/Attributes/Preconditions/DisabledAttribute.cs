using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace Fergun.Attributes.Preconditions
{
    /// <summary>
    /// Disables the command or module on a specific guild.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class DisabledAttribute : PreconditionAttribute
    {
        public DisabledAttribute(ulong guildId)
        {
             _guildId = guildId;
        }

        /// <inheritdoc />
        public override string ErrorMessage { get; set; }

        private readonly ulong _guildId;

        /// <inheritdoc />
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo _, IServiceProvider _1)
        {
            await Task.CompletedTask;
            return context.Guild.Id != _guildId
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(ErrorMessage ?? "This module is disabled in this guild.");
        }
    }
}