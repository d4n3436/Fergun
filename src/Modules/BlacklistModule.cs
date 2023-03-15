using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Data;
using Fergun.Data.Models;
using Fergun.Extensions;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

[RequireOwner]
[Group("blacklist", "Blacklist commands.")]
public class BlacklistModule : InteractionModuleBase
{
    private readonly ILogger<OtherModule> _logger;
    private readonly IFergunLocalizer<OtherModule> _localizer;
    private readonly FergunContext _db;

    public BlacklistModule(ILogger<OtherModule> logger, IFergunLocalizer<OtherModule> localizer, FergunContext db)
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
            return FergunResult.FromError(_localizer["{0} is already blacklisted.", user]);
        }

        if (dbUser is null)
        {
            dbUser = new User { Id = user.Id };
            await _db.AddAsync(dbUser);
        }

        dbUser.BlacklistStatus = shadow ? BlacklistStatus.ShadowBlacklisted : BlacklistStatus.Blacklisted;
        dbUser.BlacklistReason = reason;

        await _db.SaveChangesAsync();

        var builder = new EmbedBuilder()
            .WithDescription(_localizer["{0} has been blacklisted.", user])
            .WithColor(Color.Orange);

        await Context.Interaction.RespondAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }
    
    [SlashCommand("remove", "Removes a user from the blacklist.")]
    public async Task<RuntimeResult> RemoveAsync([Summary(description: "The user to remove.")] IUser user)
    {
        var dbUser = await _db.Users.FindAsync(user.Id);
        if (dbUser is null or { BlacklistStatus: BlacklistStatus.None })
        {
            return FergunResult.FromError(_localizer["{0} is not blacklisted.", user]);
        }

        dbUser.BlacklistStatus = BlacklistStatus.None;
        dbUser.BlacklistReason = null;

        await _db.SaveChangesAsync();

        var builder = new EmbedBuilder()
            .WithDescription(_localizer["{0} has been removed from the blacklist.", user])
            .WithColor(Color.Orange);

        await Context.Interaction.RespondAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }
}