using Discord;
using Discord.Interactions;
using Fergun.Apis.Urban;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;

namespace Fergun.Modules.Handlers;

public class UrbanAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string? text = (autocompleteInteraction.Data.Current.Value as string)?.Trim().Truncate(100, string.Empty);

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        await using var scope = services.CreateAsyncScope();

        var urbanDictionary = scope
            .ServiceProvider
            .GetRequiredService<IUrbanDictionary>();

        var policy = scope
            .ServiceProvider
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<IReadOnlyList<string>>>("UrbanPolicy");

        var results = await policy.ExecuteAsync(_ => urbanDictionary.GetAutocompleteResultsAsync(text), new Context(text));

        var suggestions = results
            .Select(x => new AutocompleteResult(x.Truncate(100), x.Truncate(100)))
            .PrependCurrentIfNotPresent(text)
            .Take(25);

        return AutocompletionResult.FromSuccess(suggestions);
    }
}