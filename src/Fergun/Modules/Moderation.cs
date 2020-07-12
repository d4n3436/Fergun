using System;
using System.Linq;
using System.Threading.Tasks;
using Fergun.Attributes.Preconditions;
using Discord;
using Discord.Commands;

namespace Fergun.Modules
{
    [Ratelimit(3, FergunClient.GlobalCooldown, Measure.Minutes)]
    [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
    public class Moderation : FergunBase
    {
        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("ban")]
        [Summary("banSummary")]
        [Alias("hardban")]
        public async Task<RuntimeResult> Ban([Summary("banParam1"), RequireLowerHierarchy("UserNotLowerHierarchy")] IGuildUser user,
            [Remainder, Summary("banParam2")] string reason = null)
        {
            if (user.Id == Context.User.Id)
            {
                await ReplyAsync(Locate("BanSameUser"));
                return FergunResult.FromSuccess();
            }
            if (user.Id == Context.Client.CurrentUser.Id)
            {
                await ReplyAsync(Locate("BanMyself"));
                return FergunResult.FromSuccess();
            }

            var BanList = await Context.Guild.GetBansAsync();
            if (BanList.Any(x => x.User.Id == user.Id))
            {
                return FergunResult.FromError(Locate("AlreadyBanned"));
            }

            await user.BanAsync(0, reason);
            await SendEmbedAsync(string.Format(Locate("Banned"), user.Mention));
            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.ManageMessages, ErrorMessage = "UserRequireManageMessages")]
        [RequireBotPermission(GuildPermission.ManageMessages, ErrorMessage = "BotRequireManageMessages")]
        [Command("clear", RunMode = RunMode.Async)]
        [Summary("clearSummary")]
        [Alias("purge", "prune")]
        [Remarks("clearRemarks")]
        public async Task<RuntimeResult> Clear([Summary("clearParam1")] int count,
            [Remainder, Summary("clearParam2")] IGuildUser user = null)
        {
            count = Math.Min(count, DiscordConfig.MaxMessagesPerBatch);
            if (count < 1)
            {
                return FergunResult.FromError(string.Format(Locate("ClearOutOfIndex"), 1, DiscordConfig.MaxMessagesPerBatch));
            }

            var messages = await Context.Channel.GetMessagesAsync(count + 1).FlattenAsync();
            if (user != null)
            {
                messages = messages.Where(x => x.Author.Id == user.Id);
                if (!messages.Any())
                {
                    return FergunResult.FromError(string.Format(Locate("ClearNotFound"), user.Mention, count));
                }
            }
            int totalMsgs = messages.Count();
            messages = messages.Where(x => x.CreatedAt > DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(14)));
            if (messages.Count() == 1)
            {
                return FergunResult.FromError(Locate("MessagesOlderThan2Weeks"));
            }
            await (Context.Channel as ITextChannel).DeleteMessagesAsync(messages);
            string message;
            if (user != null)
            {
                message = string.Format(Locate("DeletedMessagesByUser"), messages.Count() - 1, user.Mention);
            }
            else
            {
                // Here I use Messages.Count() instead of count because there's a chance there are less messages in the channel that the number to delete.
                message = string.Format(Locate("DeletedMessages"), messages.Count() - 1);
            }
            if (totalMsgs != messages.Count())
            {
                message += "\n" + string.Format(Locate("SomeMessagesNotDeleted"), totalMsgs - messages.Count());
            }

            var builder = new EmbedBuilder
            {
                Description = message,
                Color = new Color(FergunConfig.EmbedColor)
            };

            await ReplyAndDeleteAsync(null, false, builder.Build(), TimeSpan.FromSeconds(5));

            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("hackban")]
        [Summary("hackbanSummary")]
        public async Task<RuntimeResult> Hackban([Summary("hackbanParam1")] ulong userId,
            [Remainder, Summary("hackbanParam2")] string reason = null)
        {
            var BanList = await Context.Guild.GetBansAsync();
            if (BanList.Any(x => x.User.Id == userId))
            {
                return FergunResult.FromError(Locate("AlreadyBanned"));
            }
            var user = await Context.Client.Rest.GetUserAsync(userId);
            if (user == null)
            {
                return FergunResult.FromError(Locate("InvalidID"));
            }

            await Context.Guild.AddBanAsync(userId, 0, reason);
            await SendEmbedAsync(string.Format(Locate("Hackbanned"), user.ToString()));
            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.KickMembers, ErrorMessage = "UserRequireKickMembers")]
        [RequireBotPermission(GuildPermission.KickMembers, ErrorMessage = "BotRequireKickMembers")]
        [Command("kick")]
        [Summary("kickSummary")]
        public async Task<RuntimeResult> Kick(
            [Summary("kickParam1"), RequireLowerHierarchy("UserNotLowerHierarchy")] IGuildUser user,
            [Remainder, Summary("kickParam2")] string reason = null)
        {
            await user.KickAsync(reason);
            await SendEmbedAsync(string.Format(Locate("Kicked"), user.Mention));
            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.ManageNicknames, ErrorMessage = "UserRequireManageNicknames")]
        [RequireBotPermission(GuildPermission.ManageNicknames, ErrorMessage = "BotRequireManageNicknames")]
        [Command("nick")]
        [Summary("nickSummary")]
        [Alias("nickname")]
        public async Task<RuntimeResult> Nick(
            [Summary("nickParam1"), RequireLowerHierarchy("UserNotLowerHierarchy")] IGuildUser user,
            [Remainder, Summary("nickParam2")] string newNick = null)
        {
            if (user.Nickname == newNick)
            {
                if (newNick == null)
                {
                    return FergunResult.FromSuccess();
                }
                return FergunResult.FromError(Locate("CurrentNewNickEqual"));
            }

            await user.ModifyAsync(x => x.Nickname = newNick);
            if (Context.Guild.CurrentUser.GuildPermissions.AddReactions)
            {
                await Context.Message.AddReactionAsync(new Emoji("\u2705"));
            }
            return FergunResult.FromSuccess();
        }

        /*
        public async Task Nick([Remainder] string newNick)
        {
        	//idk why Context.User doesn't have ModifyAsync()
        	var user = Context.Guild.GetUser(Context.User.Id);
        	await user.ModifyAsync(x => x.Nickname = newNick);
        }
        */

        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("softban")]
        [Summary("softbanSummary")]
        public async Task<RuntimeResult> Softban(
            [Summary("softbanParam1"), RequireLowerHierarchy("UserNotLowerHierarchy", true)] IUser user,
            [Summary("softbanParam2")] uint days = 7,
            [Remainder, Summary("softbanParam3")] string reason = null)
        {
            if (user.Id == Context.User.Id)
            {
                await ReplyAsync(Locate("SoftbanSameUser"));
                return FergunResult.FromSuccess();
            }
            if (user.Id == Context.Client.CurrentUser.Id)
            {
                await ReplyAsync(Locate("SoftbanMyself"));
                return FergunResult.FromSuccess();
            }

            var banList = await Context.Guild.GetBansAsync();
            if (banList.Any(x => x.User.Id == user.Id))
            {
                return FergunResult.FromError(Locate("AlreadyBanned"));
            }
            if (days > 7)
            {
                return FergunResult.FromError(string.Format(Locate("MustBeLowerThan"), nameof(days), 7));
            }

            await Context.Guild.AddBanAsync(user.Id, (int)days, reason);
            await Context.Guild.RemoveBanAsync(user.Id);
            await SendEmbedAsync(string.Format(Locate("Softbanned"), user.ToString()));

            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("unban")]
        [Summary("unbanSummary")]
        public async Task<RuntimeResult> Unban([Summary("unbanParam1")] ulong userId)
        {
            var banList = await Context.Guild.GetBansAsync();
            var user = banList.FirstOrDefault(x => x.User.Id == userId);
            if (user != null)
            {
                return FergunResult.FromError(Locate("UserNotBanned"));
            }
            //if (!banList.Any(x => x.User.Id == userId))
            //{
            //    return FergunResult.FromError(Locate("UserNotBanned"));
            //}

            await Context.Guild.RemoveBanAsync(userId);
            //var user = await Context.Client.Rest.GetUserAsync(userId);
            await SendEmbedAsync(string.Format(Locate("Unbanned"), user.ToString()));

            return FergunResult.FromSuccess();
        }
    }
}