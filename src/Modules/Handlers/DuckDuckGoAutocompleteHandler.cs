using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;

namespace Fergun.Modules.Handlers;

public class DuckDuckGoAutocompleteHandler : AutocompleteHandler
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

        string locale = autocompleteInteraction.GetLocale("wt-wt").ToLowerInvariant();
        string[] temp = locale.Split('-');
        if (temp.Length == 2)
        {
            locale = $"{temp[1]}-{temp[0]}";
        }

        bool isNsfw = context.Channel.IsNsfw();

        string url = $"https://duckduckgo.com/ac/?q={Uri.EscapeDataString(text)}&kl={locale}&p={(isNsfw ? -1 : 1)}";

        var response = await policy.ExecuteAsync((_, ct) => client.GetAsync(new Uri(url), ct), new Context(url), CancellationToken.None);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        var results = document
            .RootElement
            .EnumerateArray()
            .Select(x => new AutocompleteResult(x.GetProperty("phrase"u8).GetString(), x.GetProperty("phrase"u8).GetString()))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}