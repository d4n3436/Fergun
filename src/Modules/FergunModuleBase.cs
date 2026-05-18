using System.Globalization;
using Discord.Interactions;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Localization;
using Fergun.Services;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

public abstract class FergunModuleBase<TModule> : InteractionModuleBase
    where TModule : FergunModuleBase<TModule>
{
    protected readonly ILogger<TModule> _logger;
    protected readonly IFergunLocalizer<TModule> _localizer;
    protected readonly FergunEmoteProvider _emotes;
    protected readonly InteractiveService _interactive;

    protected FergunModuleBase(ILogger<TModule> logger, IFergunLocalizer<TModule> localizer, FergunEmoteProvider emotes, InteractiveService interactive)
    {
        _logger = logger;
        _localizer = localizer;
        _emotes = emotes;
        _interactive = interactive;
    }

    public override void BeforeExecute(ICommandInfo command)
        => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());
}