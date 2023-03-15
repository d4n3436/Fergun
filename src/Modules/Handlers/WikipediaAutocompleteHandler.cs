using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Wikipedia;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;
using Polly;

namespace Fergun.Modules.Handlers;

public class WikipediaAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string? text = (autocompleteInteraction.Data.Current.Value as string)?.Trim().Truncate(100, string.Empty);

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        string language = autocompleteInteraction.GetLanguageCode();

        await using var scope = services.CreateAsyncScope();

        var wikipediaClient = scope
            .ServiceProvider
            .GetRequiredService<IWikipediaClient>();

        var policy = scope
            .ServiceProvider
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<IReadOnlyList<IPartialWikipediaArticle>>>("WikipediaPolicy");

        var results = await policy.ExecuteAsync((_, ct) => wikipediaClient.SearchArticlesAsync(text, language, ct), new Context($"{text}-{language}"), CancellationToken.None);

        var suggestions = results
            .Take(25)
            .Select(x => new AutocompleteResult(x.Title.Truncate(100), x.Id));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}