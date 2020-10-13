using System;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.CommandCache;
using Discord.Commands;
using Fergun.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Attributes
{
    /// <summary>
    /// An attribute that sends the typing state to the current channel (useful for long-running commands).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class LongRunningAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo _, IServiceProvider services)
        {
            IUserMessage response;
            bool found = services.GetRequiredService<CommandCacheService>().TryGetValue(context.Message.Id, out ulong messageId);
            if (found && (response = (IUserMessage)await context.Channel.GetMessageAsync(messageId)) != null)
            {
                await response.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = new EmbedBuilder()
                    .WithDescription($"{Constants.LoadingEmote} {GuildUtils.Locate("Loading", context.Channel)}")
                    .WithColor(FergunConfig.EmbedColor)
                    .Build();
                });
            }
            else
            {
                await context.Channel.TriggerTypingAsync();
            }
            return PreconditionResult.FromSuccess();
        }
    }
}