using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Attributes.Preconditions
{
    /// <summary>
    ///     Indicates this parameter must be a <see cref="SocketGuildUser"/>
    ///     whose <see cref="SocketGuildUser.Hierarchy"/> value must be
    ///     lower than that of the Bot.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class RequireLowerHierarchyAttribute : ParameterPreconditionAttribute
    {
        private readonly string _errorMessage = null;
        private readonly bool _ignoreNotGuildContext = false;

        public RequireLowerHierarchyAttribute()
        {

        }

        public RequireLowerHierarchyAttribute(string errorMessage)
        {
            _errorMessage = errorMessage;
        }

        public RequireLowerHierarchyAttribute(string errorMessage, bool ignoreNotGuildContext) : this(errorMessage)
        {
            _ignoreNotGuildContext = ignoreNotGuildContext;
        }

        /// <inheritdoc />
        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext _, ParameterInfo __, object value, IServiceProvider ___)
        {
            if (value is SocketGuildUser user)
            {
                return (user.Guild.CurrentUser.Hierarchy > user.Hierarchy)
                    ? Task.FromResult(PreconditionResult.FromSuccess())
                    : Task.FromResult(PreconditionResult.FromError(_errorMessage ?? "Specified user must be lower in hierarchy."));
            }
            if (_ignoreNotGuildContext)
                return Task.FromResult(PreconditionResult.FromSuccess());

            return Task.FromResult(PreconditionResult.FromError("Command requires Guild context."));
        }
    }
}