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

        var client = services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("autocomplete");

        var policy = services
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<HttpResponseMessage>>("AutocompletePolicy");

        string locale = autocompleteInteraction.GetLocale("wt-wt").ToLowerInvariant();
        var temp = locale.Split('-');
        if (temp.Length == 2)
        {
            locale = $"{temp[1]}-{temp[0]}";
        }

        bool isNsfw = context.Channel.IsNsfw();

        client.DefaultRequestHeaders.TryAddWithoutValidation("cookie", $"p={(isNsfw ? -2 : 1)}");

        string url = $"https://duckduckgo.com/ac/?q={Uri.EscapeDataString(text)}&kl={locale}";

        var response = await policy.ExecuteAsync(_ => client.GetAsync(new Uri(url)), new Context($"{url}-nsfw:{isNsfw}"));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = JsonDocument.Parse(bytes);

        var results = document
            .RootElement
            .EnumerateArray()
            .Select(x => new AutocompleteResult(x.GetProperty("phrase").GetString(), x.GetProperty("phrase").GetString()))
            .PrependCurrentIfNotPresent(text)
            .Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}