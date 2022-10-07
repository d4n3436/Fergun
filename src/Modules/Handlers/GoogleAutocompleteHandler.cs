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
        string? text = (autocompleteInteraction.Data.Current.Value as string)?.Trim().Truncate(100, string.Empty);

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        string locale = autocompleteInteraction.GetLocale();

        string url = $"https://www.google.com/complete/search?q={Uri.EscapeDataString(text)}&client=chrome&hl={locale}&xhr=t";
        var response = await policy.ExecuteAsync(_ => client.GetAsync(new Uri(url)), new Context(url));
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        var results = document
            .RootElement[1]
            .EnumerateArray()
            .Select(x => new AutocompleteResult(x.GetString(), x.GetString()))
            .PrependCurrentIfNotPresent(text)
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}