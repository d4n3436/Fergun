using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Services;

namespace Fergun.Modules
{
    [Order(2)]
    [RequireBotPermission(Constants.MinimumRequiredPermissions)]
    [Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
    public class Moderation : FergunBase
    {
        private static MessageCacheService _messageCache;

        public Moderation(MessageCacheService messageCache)
        {
            _messageCache ??= messageCache;
        }

        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("ban")]
        [Summary("banSummary")]
        [Alias("hardban")]
        [Example("Fergun#6839")]
        public async Task<RuntimeResult> Ban([Summary("banParam1"), RequireLowerHierarchy("UserNotLowerHierarchy")] IUser user,
            [Remainder, Summary("banParam2")] string reason = null)
        {
            if (user.Id == Context.User.Id)
            {
                await ReplyAsync(Locate("BanSameUser"));
                return FergunResult.FromSuccess();
            }
            if (await Context.Guild.GetBanAsync(user) != null)
            {
                return FergunResult.FromError(Locate("AlreadyBanned"));
            }
            if (!(user is IGuildUser))
            {
                return FergunResult.FromError(Locate("UserNotFound"));
            }

            await Context.Guild.AddBanAsync(user, 0, reason?.Truncate(512));
            await SendEmbedAsync(string.Format(Locate("Banned"), user.Mention));
            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.ManageMessages, ErrorMessage = "UserRequireManageMessages")]
        [RequireBotPermission(GuildPermission.ManageMessages, ErrorMessage = "BotRequireManageMessages")]
        [Command("clear", RunMode = RunMode.Async)]
        [Summary("clearSummary")]
        [Alias("purge", "prune")]
        [Remarks("clearRemarks")]
        [Example("10")]
        public async Task<RuntimeResult> Clear([Summary("clearParam1")] int count,
            [Remainder, Summary("clearParam2")] IUser user = null)
        {
            count = Math.Min(count, DiscordConfig.MaxMessagesPerBatch);
            if (count < 1)
            {
                return FergunResult.FromError(string.Format(Locate("NumberOutOfIndex"), 1, DiscordConfig.MaxMessagesPerBatch));
            }

            var messages = await Context.Channel.GetMessagesAsync(_messageCache, Context.Message, Direction.Before, count).Flatten().ToListAsync();

            // Get the total message count before being filtered.
            int totalMessages = messages.Count;

            if (totalMessages == 0)
            {
                return FergunResult.FromError(Locate("NothingToDelete"));
            }

            if (user != null)
            {
                // Get messages by user
                messages.RemoveAll(x => x.Author.Id != user.Id);
                if (messages.Count == 0)
                {
                    return FergunResult.FromError(string.Format(Locate("ClearNotFound"), user.Mention, count));
                }
            }

            // Get messages younger than 2 weeks
            messages.RemoveAll(x => x.CreatedAt <= DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(14)));
            if (messages.Count == 0)
            {
                return FergunResult.FromError(Locate("MessagesOlderThan2Weeks"));
            }

            try
            {
                await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages.Append(Context.Message));
            }
            catch (HttpException e) when (e.HttpCode == HttpStatusCode.NotFound) { }

            string message = user != null
                ? string.Format(Locate("DeletedMessagesByUser"), messages.Count, user.Mention)
                : string.Format(Locate("DeletedMessages"), messages.Count);

            if (totalMessages != messages.Count)
            {
                message += "\n" + string.Format(Locate("SomeMessagesNotDeleted"), totalMessages - messages.Count);
            }

            var builder = new EmbedBuilder
            {
                Description = message,
                Color = new Color(FergunClient.Config.EmbedColor)
            };

            await ReplyAndDeleteAsync(null, false, builder.Build(), TimeSpan.FromSeconds(5));

            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("hackban")]
        [Summary("hackbanSummary")]
        [Example("666963870385923507 spam")]
        public async Task<RuntimeResult> Hackban([Summary("hackbanParam1")] ulong userId,
            [Remainder, Summary("hackbanParam2")] string reason = null)
        {
            if (await Context.Guild.GetBanAsync(userId) != null)
            {
                return FergunResult.FromError(Locate("AlreadyBanned"));
            }
            var user = await Context.Client.Rest.GetUserAsync(userId);
            if (user == null)
            {
                return FergunResult.FromError(Locate("InvalidID"));
            }
            var guildUser = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, userId);
            if (guildUser != null && Context.Guild.CurrentUser.Hierarchy <= guildUser.GetHierarchy())
            {
                return FergunResult.FromError(Locate("UserNotLowerHierarchy"));
            }

            await Context.Guild.AddBanAsync(userId, 0, reason?.Truncate(512));
            await SendEmbedAsync(string.Format(Locate("Hackbanned"), user));
            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.KickMembers, ErrorMessage = "UserRequireKickMembers")]
        [RequireBotPermission(GuildPermission.KickMembers, ErrorMessage = "BotRequireKickMembers")]
        [Command("kick")]
        [Summary("kickSummary")]
        [Example("Fergun#6839 test")]
        public async Task<RuntimeResult> Kick(
            [Summary("kickParam1"), RequireLowerHierarchy("UserNotLowerHierarchy")] IUser user,
            [Remainder, Summary("kickParam2")] string reason = null)
        {
            if (!(user is IGuildUser guildUser))
            {
                return FergunResult.FromError(Locate("UserNotFound"));
            }
            await guildUser.KickAsync(reason?.Truncate(512));
            await SendEmbedAsync(string.Format(Locate("Kicked"), user.Mention));

            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.ManageNicknames, ErrorMessage = "UserRequireManageNicknames")]
        [RequireBotPermission(GuildPermission.ManageNicknames, ErrorMessage = "BotRequireManageNicknames")]
        [Command("nick")]
        [Summary("nickSummary")]
        [Alias("nickname")]
        [Example("Fergun#6839 fer")]
        public async Task<RuntimeResult> Nick(
            [Summary("nickParam1"), RequireLowerHierarchy("UserNotLowerHierarchy")] IUser user,
            [Remainder, Summary("nickParam2")] string newNick = null)
        {
            if (!(user is IGuildUser guildUser))
            {
                return FergunResult.FromError(Locate("UserNotFound"));
            }

            newNick = newNick?.Truncate(32);
            if (guildUser.Nickname == newNick)
            {
                return newNick == null
                    ? FergunResult.FromSuccess()
                    : FergunResult.FromError(Locate("CurrentNewNickEqual"));
            }

            await guildUser.ModifyAsync(x => x.Nickname = newNick);
            if (Context.Guild.CurrentUser.GuildPermissions.AddReactions)
            {
                await Context.Message.AddReactionAsync(new Emoji("\u2705"));
            }
            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("softban")]
        [Summary("softbanSummary")]
        [Example("Fergun#6839 10 test")]
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

            if (await Context.Guild.GetBanAsync(user) != null)
            {
                return FergunResult.FromError(Locate("AlreadyBanned"));
            }
            if (user is IGuildUser guildUser && Context.Guild.CurrentUser.Hierarchy <= guildUser.GetHierarchy())
            {
                return FergunResult.FromError(Locate("UserNotLowerHierarchy"));
            }
            if (days > 7)
            {
                return FergunResult.FromError(string.Format(Locate("MustBeLowerThan"), nameof(days), 7));
            }

            await Context.Guild.AddBanAsync(user.Id, (int)days, reason?.Truncate(512));
            await Context.Guild.RemoveBanAsync(user.Id);
            await SendEmbedAsync(string.Format(Locate("Softbanned"), user));

            return FergunResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "UserRequireBanMembers")]
        [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "BotRequireBanMembers")]
        [Command("unban")]
        [Summary("unbanSummary")]
        [Example("666963870385923507")]
        public async Task<RuntimeResult> Unban([Summary("unbanParam1")] ulong userId)
        {
            var ban = await Context.Guild.GetBanAsync(userId);
            if (ban == null)
            {
                return FergunResult.FromError(Locate("UserNotBanned"));
            }

            await Context.Guild.RemoveBanAsync(userId);
            await SendEmbedAsync(string.Format(Locate("Unbanned"), ban.User));

            return FergunResult.FromSuccess();
        }
    }
}