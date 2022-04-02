using Discord;
using Discord.Interactions;
using Fergun.Apis.Wikipedia;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Modules.Handlers;

public class WikipediaAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim().Truncate(100, string.Empty);

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        var urbanDictionary = services
            .GetRequiredService<IWikipediaClient>();

        var results = (await urbanDictionary.GetAutocompleteResultsAsync(text, autocompleteInteraction.GetLanguageCode()))
            .Select(x => new AutocompleteResult(x, x))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}