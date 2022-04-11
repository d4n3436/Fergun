using System.Globalization;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;

namespace Fergun.Modules;

public class UserModule : InteractionModuleBase
{
    private readonly IFergunLocalizer<UserModule> _localizer;

    public UserModule(IFergunLocalizer<UserModule> localizer)
    {
        _localizer = localizer;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [UserCommand("Avatar")]
    public async Task Avatar(IUser user)
    {
        string url = (user as IGuildUser)?.GetGuildAvatarUrl(size: 2048) ?? user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl();

        var builder = new EmbedBuilder
        {
            Title = user.ToString(),
            ImageUrl = url,
            Color = Color.Orange
        };

        await RespondAsync(embed: builder.Build());
    }

    [UserCommand("User Info")]
    public async Task UserInfo(IUser user)
    {
        string activities = "";
        if (user.Activities.Count > 0)
        {
            activities = string.Join('\n', user.Activities.Select(x =>
                x.Type == ActivityType.CustomStatus
                    ? ((CustomStatusGame)x).ToString()
                    : $"{x.Type} {x.Name}"));
        }

        if (string.IsNullOrWhiteSpace(activities))
            activities = $"({_localizer["None"]})";

        string clients = "?";
        if (user.ActiveClients.Count > 0)
        {
            clients = string.Join(' ', user.ActiveClients.Select(x =>
                x switch
                {
                    ClientType.Desktop => "🖥",
                    ClientType.Mobile => "📱",
                    ClientType.Web => "🌐",
                    _ => ""
                }));
        }

        if (string.IsNullOrWhiteSpace(clients))
            clients = "?";

        var guildUser = user as IGuildUser;
        string avatarUrl = guildUser?.GetGuildAvatarUrl(size: 2048) ?? user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["User Info"])
            .AddField(_localizer["Name"], user.ToString())
            .AddField("Nickname", guildUser?.Nickname ?? $"({_localizer["None"]})")
            .AddField("ID", user.Id)
            .AddField(_localizer["Activities"], activities, true)
            .AddField("Active Clients", clients, true)
            .AddField(_localizer["Is Bot"], user.IsBot)
            .AddField(_localizer["Created At"], GetTimestamp(user.CreatedAt))
            .AddField(_localizer["Server Join Date"], GetTimestamp(guildUser?.JoinedAt))
            .AddField(_localizer["Boosting Since"], GetTimestamp(guildUser?.PremiumSince))
            .WithThumbnailUrl(avatarUrl)
            .WithColor(Color.Orange);

        await RespondAsync(embed: builder.Build());

        static string GetTimestamp(DateTimeOffset? dateTime)
            => dateTime == null ? "N/A" : $"{dateTime.Value.ToDiscordTimestamp()} ({dateTime.Value.ToDiscordTimestamp('R')})";
    }
}