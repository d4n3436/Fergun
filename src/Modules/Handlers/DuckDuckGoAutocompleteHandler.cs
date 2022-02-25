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
        var value = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        if (string.IsNullOrEmpty(value))
            return AutocompletionResult.FromSuccess();

        var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        bool isNsfw = context.Channel.IsNsfw();
        client.DefaultRequestHeaders.TryAddWithoutValidation("cookie", $"p={(isNsfw ? -2 : 1)}");

        string locale = autocompleteInteraction.GetLocale("wt-wt").ToLowerInvariant();
        string url = $"https://duckduckgo.com/ac/?q={Uri.EscapeDataString(value)}&kl={locale}";

        var response = await policy.ExecuteAsync(_ => client.GetAsync(new Uri(url)), new Context($"{url}-nsfw:{isNsfw}"));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        var results = document
            .RootElement
            .EnumerateArray()
            .Select(x => new AutocompleteResult(x.GetProperty("phrase").GetString(), x.GetProperty("phrase").GetString()))
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}