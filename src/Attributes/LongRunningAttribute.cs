using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Services;
using Fergun.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Attributes
{
    /// <summary>
    /// An attribute that sends the typing state to the current channel (useful for long-running commands).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class LongRunningAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            IUserMessage response;
            ulong messageId = 0;
            bool? found = services.GetService<CommandCacheService>()?.TryGetValue(context.Message.Id, out messageId);
            var cache = services.GetService<MessageCacheService>();

            if ((found ?? false) && (response = (IUserMessage)await context.Channel.GetMessageAsync(cache, messageId)) != null)
            {
                await response.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = new EmbedBuilder()
                        .WithDescription($"{FergunClient.Config.LoadingEmote} {GuildUtils.Locate("Loading", context.Channel)}")
                        .WithColor(FergunClient.Config.EmbedColor)
                        .Build();
                    x.Components = new ComponentBuilder().Build(); // Remove components
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