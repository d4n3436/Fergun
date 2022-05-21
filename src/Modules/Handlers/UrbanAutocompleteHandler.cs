using Discord;
using Discord.Interactions;
using Fergun.Apis.Urban;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Modules.Handlers;

public class UrbanAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim().Truncate(100, string.Empty);

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        var urbanDictionary = services
            .GetRequiredService<IUrbanDictionary>();

        var results = (await urbanDictionary.GetAutocompleteResultsAsync(text))
            .Select(x => new AutocompleteResult(x.Truncate(100), x.Truncate(100)))
            .PrependCurrentIfNotPresent(text)
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}