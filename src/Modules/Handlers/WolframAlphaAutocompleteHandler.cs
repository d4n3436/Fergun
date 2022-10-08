using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Fergun.Apis.WolframAlpha;
using Polly;
using Polly.Registry;

namespace Fergun.Modules.Handlers;

public class WolframAlphaAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string? input = (autocompleteInteraction.Data.Current.Value as string)?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            return AutocompletionResult.FromSuccess();
        }

        await using var scope = services.CreateAsyncScope();

        var wolframAlphaClient = scope
            .ServiceProvider
            .GetRequiredService<IWolframAlphaClient>();

        var policy = scope
            .ServiceProvider
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<IReadOnlyList<string>>>("WolframPolicy");

        var results = await policy.ExecuteAsync((_, ct) =>  wolframAlphaClient.GetAutocompleteResultsAsync(input, ct), new Context(input), CancellationToken.None);

        var suggestions = results
            .Take(25)
            .Select(x => new AutocompleteResult(x, x));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}