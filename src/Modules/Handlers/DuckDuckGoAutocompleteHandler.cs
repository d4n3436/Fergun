using System.Text.Json;
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
        var text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        if (string.IsNullOrEmpty(text))
            return AutocompletionResult.FromSuccess();

        string locale = autocompleteInteraction.GetLocale("wt-wt").ToLowerInvariant();
        bool isNsfw = context.Channel.IsNsfw();

        var suggestions = await GetDuckDuckGoSuggestionsAsync(text, services, locale, isNsfw);

        var results = suggestions
            .Select(x => new AutocompleteResult(x, x))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }

    public static async Task<string?[]> GetDuckDuckGoSuggestionsAsync(string text, IServiceProvider services, string locale = "wt-wt", bool isNsfw = false)
    {
        var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        client.DefaultRequestHeaders.TryAddWithoutValidation("cookie", $"p={(isNsfw ? -2 : 1)}");

        string url = $"https://duckduckgo.com/ac/?q={Uri.EscapeDataString(text)}&kl={locale}";

        var response = await policy.ExecuteAsync(_ => client.GetAsync(new Uri(url)), new Context($"{url}-nsfw:{isNsfw}"));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        return document
            .RootElement
            .EnumerateArray()
            .Select(x => x.GetProperty("phrase").GetString())
            .ToArray();
    }
}