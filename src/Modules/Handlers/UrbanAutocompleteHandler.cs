using Discord;
using Discord.Interactions;
using Fergun.Apis;
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
            .GetRequiredService<UrbanDictionary>();

        var results = (await urbanDictionary.GetAutocompleteResultsAsync(text))
            .Select(x => new AutocompleteResult(x, x))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}