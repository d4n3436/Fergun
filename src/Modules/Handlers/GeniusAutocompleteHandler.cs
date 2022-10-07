using Discord;
using Discord.Interactions;
using Fergun.Apis.Genius;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;
using Polly;

namespace Fergun.Modules.Handlers;

public class GeniusAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string? text = (autocompleteInteraction.Data.Current.Value as string)?.Trim();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return AutocompletionResult.FromSuccess();
        }

        await using var scope = services.CreateAsyncScope();

        var geniusClient = scope
            .ServiceProvider
            .GetRequiredService<IGeniusClient>();

        var policy = scope
            .ServiceProvider
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<IGeniusSong[]>>("MusixmatchPolicy");

        var songs = await policy.ExecuteAsync(async _ => (await geniusClient.SearchSongsAsync(text)).ToArray(), new Context(text));

        var results = songs
            .Where(x => !x.IsInstrumental && x.LyricsState != "unreleased")
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.ArtistNames} - {x.Title}".Truncate(100), x.Id));

        return AutocompletionResult.FromSuccess(results);
    }
}