using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Data;
using Fergun.Data.Models;
using Fergun.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[RequireOwner]
[Group("blacklist", "Blacklist commands.")]
public class BlacklistModule : InteractionModuleBase
{
    private readonly ILogger<BlacklistModule> _logger;
    private readonly IFergunLocalizer<BlacklistModule> _localizer;
    private readonly FergunContext _db;

    public BlacklistModule(ILogger<BlacklistModule> logger, IFergunLocalizer<BlacklistModule> localizer, FergunContext db)
    {
        _logger = logger;
        _localizer = localizer;
        _db = db;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("add", "Adds a user to the blacklist.")]
    public async Task<RuntimeResult> AddAsync([Summary(description: "The user to add.")] IUser user,
        [Summary(description: "The blacklist reason.")] string? reason = null,
        [Summary(description: "Whether the user should be \"shadow\"-blacklisted.")] bool shadow = false)
    {
        var dbUser = await _db.Users.FindAsync(user.Id);
        if (dbUser?.BlacklistStatus is BlacklistStatus.Blacklisted or BlacklistStatus.ShadowBlacklisted)
        {
            return FergunResult.FromError(_localizer["UserAlreadyBlacklisted", user]);
        }

        if (dbUser is null)
        {
            dbUser = new User { Id = user.Id };
            await _db.AddAsync(dbUser);
        }

        dbUser.BlacklistStatus = shadow ? BlacklistStatus.ShadowBlacklisted : BlacklistStatus.Blacklisted;
        dbUser.BlacklistReason = reason;

        await _db.SaveChangesAsync();
        _logger.LogInformation("User {User} ({Id}) has been added to the blacklist (reason: {Reason}, shadow: {Shadow})", user, user.Id, reason ?? "(None)", shadow);

        var builder = new EmbedBuilder()
            .WithDescription(_localizer["UserBlacklisted", user])
            .WithColor(Constants.DefaultColor);

        await Context.Interaction.RespondAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }

    [SlashCommand("remove", "Removes a user from the blacklist.")]
    public async Task<RuntimeResult> RemoveAsync([Summary(description: "The user to remove.")] IUser user)
    {
        var dbUser = await _db.Users.FindAsync(user.Id);
        if (dbUser is null or { BlacklistStatus: BlacklistStatus.None })
        {
            return FergunResult.FromError(_localizer["UserNotBlacklisted", user]);
        }

        dbUser.BlacklistStatus = BlacklistStatus.None;
        dbUser.BlacklistReason = null;

        await _db.SaveChangesAsync();
        _logger.LogInformation("User {User} ({Id}) has been removed from the blacklist", user, user.Id);

        var builder = new EmbedBuilder()
            .WithDescription(_localizer["UserRemovedFromBlacklist", user])
            .WithColor(Constants.DefaultColor);

        await Context.Interaction.RespondAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }
}