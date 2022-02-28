using System.Text.Json;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;

namespace Fergun.Modules.Handlers;

public class BraveAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        var suggestions = await GetBraveSuggestionsAsync(text, services);

        var results = suggestions
            .Select(x => new AutocompleteResult(x, x))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }

    public static async Task<string?[]> GetBraveSuggestionsAsync(string text, IServiceProvider services)
    {
        var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        string url = $"https://search.brave.com/api/suggest?q={Uri.EscapeDataString(text)}&source=web";

        var response = await policy.ExecuteAsync(_ => client.GetAsync(new Uri(url)), new Context(url));

        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        return document
            .RootElement[1]
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToArray();
    }
}