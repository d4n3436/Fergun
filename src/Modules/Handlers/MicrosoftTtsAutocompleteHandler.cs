using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Translators;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Modules.Handlers;

public class MicrosoftTtsAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        var translator = services
            .GetRequiredService<MicrosoftTranslator>();

        var voices = MicrosoftTranslator.DefaultVoices.Values;
        var task = translator.GetTTSVoicesAsync();
        if (task.IsCompletedSuccessfully)
        {
            voices = task.GetAwaiter().GetResult();
        }

        voices = voices
            .Where(x => x.DisplayName.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                        x.Locale.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                        x.Gender.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DisplayName);

        if (context.Interaction.TryGetLanguage(out var userLanguage) && string.IsNullOrEmpty(text))
        {
            var matchingVoices = voices
                .Where(x => x.Locale.StartsWith(userLanguage.ISO6391, StringComparison.OrdinalIgnoreCase))
                .ToHashSet();

            matchingVoices.UnionWith(voices);
            voices = matchingVoices;
            
        }

        var results = voices
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.DisplayName} ({x.Gender}, {x.Locale})", x.ShortName));

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }

    
}