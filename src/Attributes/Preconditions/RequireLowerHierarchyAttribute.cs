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
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class RequireLowerHierarchyAttribute : ParameterPreconditionAttribute
    {
        public string ErrorMessage { get; }

        public bool IgnoreNotGuildContext { get; }

        public RequireLowerHierarchyAttribute()
        {
        }

        public RequireLowerHierarchyAttribute(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public RequireLowerHierarchyAttribute(string errorMessage, bool ignoreNotGuildContext) : this(errorMessage)
        {
            IgnoreNotGuildContext = ignoreNotGuildContext;
        }

        /// <inheritdoc />
        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services)
        {
            if (!(value is IGuildUser user))
            {
                return IgnoreNotGuildContext
                    ? PreconditionResult.FromSuccess()
                    : PreconditionResult.FromError("Command requires Guild context.");
            }

            int botHierarchy = (await user.Guild.GetCurrentUserAsync()).GetHierarchy();
            int userHierarchy = user.GetHierarchy();
            return botHierarchy > userHierarchy
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(ErrorMessage ?? "Specified user must be lower in hierarchy.");
        }
    }
}