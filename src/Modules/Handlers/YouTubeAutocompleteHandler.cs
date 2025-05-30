using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Modules.Handlers;

public class YouTubeAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string? text = (autocompleteInteraction.Data.Current.Value as string)?.Trim().Truncate(100, string.Empty);

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        using var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        string language = autocompleteInteraction.GetLanguageCode();

        string url = $"https://suggestqueries-clients6.youtube.com/complete/search?client=youtube&hl={language}&gs_ri=youtube&ds=yt&q={Uri.EscapeDataString(text)}&xhr=t";

        var response = await policy.ExecuteAsync((_, ct) => client.GetAsync(new Uri(url), ct), new Context(url), CancellationToken.None);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        var results = document
            .RootElement[1]
            .EnumerateArray()
            .Select(x => new AutocompleteResult(x[0].GetString(), x[0].GetString()))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}