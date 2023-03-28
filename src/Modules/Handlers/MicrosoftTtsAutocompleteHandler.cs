using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate.Translators;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Modules.Handlers;

public class MicrosoftTtsAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string? text = (autocompleteInteraction.Data.Current.Value as string)?.Trim();

        var translator = services
            .GetRequiredService<MicrosoftTranslator>();

        var voices = MicrosoftTranslator.DefaultVoices.Values;
        var task = translator.GetTTSVoicesAsync();
        if (task.IsCompletedSuccessfully)
        {
            voices = await task;
        }

        if (string.IsNullOrEmpty(text))
        {
            if (context.Interaction.TryGetLanguage(out var userLanguage))
            {
                voices = voices
                    .Where(x => x.Locale.StartsWith(userLanguage.ISO6391, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                return AutocompletionResult.FromSuccess();
            }
        }
        else
        {
            voices = voices
                .Where(x => x.DisplayName.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                            x.Locale.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                            x.Gender.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.DisplayName);
        }

        var results = voices
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.DisplayName} ({x.Gender}, {x.Locale})", x.ShortName));

        return AutocompletionResult.FromSuccess(results);
    }
}