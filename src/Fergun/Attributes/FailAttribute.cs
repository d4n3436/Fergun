using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace Fergun.Attributes
{
    /// <summary>
    /// An attributes that fails on purpose.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class FailAttribute : PreconditionAttribute
    {
        /// <inheritdoc />
        public override string ErrorMessage { get; set; }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext _, CommandInfo _1, IServiceProvider _2)
        {
            return Task.FromResult(PreconditionResult.FromError(ErrorMessage ?? "Failed on purpose."));
        }
    }
}