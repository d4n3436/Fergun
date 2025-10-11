using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Genius;
using Humanizer;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;

namespace Fergun.Modules.Handlers;

[UsedImplicitly]
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
            .Get<IAsyncPolicy<IReadOnlyList<IGeniusSong>>>("GeniusPolicy");

        var songs = await policy.ExecuteAsync((_, ct) => geniusClient.SearchSongsAsync(text, ct), new Context(text), CancellationToken.None);

        var results = songs
            .Where(x => !x.IsInstrumental && x.LyricsState != "unreleased")
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.ArtistNames} - {x.Title}".Truncate(100), x.Id));

        return AutocompletionResult.FromSuccess(results);
    }
}