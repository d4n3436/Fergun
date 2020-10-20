using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Extensions;

namespace Fergun.Attributes.Preconditions
{
    /// <summary>
    ///     Indicates this parameter must be a <see cref="IGuildUser"/>
    ///     whose Hierarchy value must be
    ///     lower than that of the Bot.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class RequireLowerHierarchyAttribute : ParameterPreconditionAttribute
    {
        private readonly string _errorMessage;
        private readonly bool _ignoreNotGuildContext;

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
        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, ParameterInfo __, object value, IServiceProvider ___)
        {
            if (value is IGuildUser user)
            {
                int botHierarchy = (await user.Guild.GetCurrentUserAsync()).GetHierarchy();
                int userHierarchy = user.GetHierarchy();
                return botHierarchy > userHierarchy
                    ? PreconditionResult.FromSuccess()
                    : PreconditionResult.FromError(_errorMessage ?? "Specified user must be lower in hierarchy.");
            }
            return _ignoreNotGuildContext
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("Command requires Guild context.");
        }
    }
}