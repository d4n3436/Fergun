using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Musixmatch;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;

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
            .Get<IAsyncPolicy<IReadOnlyList<IMusixmatchSong>>>("MusixmatchPolicy");

        var songs = await policy.ExecuteAsync((_, ct) => musixmatchClient.SearchSongsAsync(text, true, ct), new Context(text), CancellationToken.None);

        var results = songs
            .Where(x => x is { IsInstrumental: false, HasLyrics: true, IsRestricted: false })
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.ArtistName} - {x.Title}".Truncate(100), x.Id));

        return AutocompletionResult.FromSuccess(results);
    }
}