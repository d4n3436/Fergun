using Discord;
using Discord.Interactions;
using Fergun.Apis.Musixmatch;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;
using Polly;

namespace Fergun.Modules.Handlers;

public class MusixmatchAutocompleteHandler : AutocompleteHandler
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

        var musixmatchClient = scope
            .ServiceProvider
            .GetRequiredService<IMusixmatchClient>();

        var policy = scope
            .ServiceProvider
            .GetRequiredService<IReadOnlyPolicyRegistry<string>>()
            .Get<IAsyncPolicy<IMusixmatchSong[]>>("MusixmatchPolicy");

        var songs = await policy.ExecuteAsync(async _ => (await musixmatchClient.SearchSongsAsync(text)).ToArray(), new Context(text));

        var results = songs
            .Where(x => !x.IsInstrumental && x.HasLyrics && !x.IsRestricted)
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.ArtistName} - {x.Title}".Truncate(100), x.Id));

        return AutocompletionResult.FromSuccess(results);
    }
}