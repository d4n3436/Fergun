using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Services;
using Fergun.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Attributes.Preconditions
{
    /// <summary>
    ///     Indicates that this command can only be used while
    ///     the user is in a voice channel in the same Guild,
    ///     and the Lavalink node is connected.
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

        public UserMustBeInVoiceAttribute(params string[] exceptions)
            : this()
        {
            _exceptions = exceptions;
        }

        private readonly string[] _exceptions;

        /// <inheritdoc />
        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var baseResult = await base.CheckPermissionsAsync(context, command, services);
            if (!baseResult.IsSuccess)
                return baseResult;

            if (Array.Exists(_exceptions, x => x == command.Name))
                return PreconditionResult.FromSuccess();

            var musicService = services.GetService<MusicService>();
            if (musicService == null || !musicService.LavaNode.IsConnected)
                return PreconditionResult.FromError(ErrorMessage ?? GuildUtils.Locate("LavalinkNotConnected", context.Channel));

            var current = (context.User as IVoiceState)?.VoiceChannel?.Id;
            return (await context.Guild.GetVoiceChannelsAsync()).Any(v => v.Id == current)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(ErrorMessage ?? GuildUtils.Locate("UserNotInVC", context.Channel));
        }
    }
}