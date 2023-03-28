using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        string? text = (autocompleteInteraction.Data.Current.Value as string)?.Trim();

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        using var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        string url = $"https://search.brave.com/api/suggest?q={Uri.EscapeDataString(text)}&source=web";

        var response = await policy.ExecuteAsync((_, ct) => client.GetAsync(new Uri(url), ct), new Context(url), CancellationToken.None);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        var results = document
            .RootElement[1]
            .EnumerateArray()
            .Select(x => new AutocompleteResult(x.GetString(), x.GetString()))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}