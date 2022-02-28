using System.Text.Json;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;

namespace Fergun.Modules.Handlers;

public class GoogleAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim().Truncate(100, string.Empty);

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        string language = autocompleteInteraction.GetLanguageCode();

        var suggestions = await GetGoogleSuggestionsAsync(text, services, language);

        var results = suggestions
            .Select(x => new AutocompleteResult(x, x))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }

    public static async Task<string?[]> GetGoogleSuggestionsAsync(string text, IServiceProvider services, string language = "en")
    {
        var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        string url = $"https://www.google.com/complete/search?q={Uri.EscapeDataString(text)}&client=chrome&hl={language}&xhr=t";
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