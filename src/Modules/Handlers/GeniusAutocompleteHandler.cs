using Discord;
using Discord.Interactions;
using Fergun.Apis.Genius;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Modules.Handlers;

public class GeniusAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return AutocompletionResult.FromSuccess();
        }

        var geniusClient = services
            .GetRequiredService<IGeniusClient>();

        var songs = await geniusClient.SearchSongsAsync(text);

        var results = songs
            .Where(x => !x.IsInstrumental)
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.ArtistNames} - {x.Title}".Truncate(100), x.Id));

        return AutocompletionResult.FromSuccess(results);
    }


}