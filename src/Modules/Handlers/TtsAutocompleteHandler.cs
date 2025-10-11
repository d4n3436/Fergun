using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Translators;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Modules.Handlers;

[UsedImplicitly]
public class TtsAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var localizer = services.GetRequiredService<IFergunLocalizer<SharedModule>>();
        localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());

        string text = (autocompleteInteraction.Data.Current.Value as string)?.Trim() ?? string.Empty;

        IEnumerable<Language> languages = GoogleTranslator2
            .TextToSpeechLanguages
            .Cast<Language>()
            .Where(x => x.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                        x.NativeName.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                        x.ISO6391.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                        x.ISO6393.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name);

        if (context.Interaction.TryGetLanguage(out var userLanguage) && (string.IsNullOrEmpty(text) ||
                userLanguage.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                userLanguage.NativeName.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                userLanguage.ISO6391.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                userLanguage.ISO6393.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
        {
            // Move language to top
            languages = languages.Where(x => !x.Equals(userLanguage)).Prepend(userLanguage);
        }

        var array = languages.ToArray();
        int last = Math.Min(25, array.Length);
        int excess = array.Length - 25;

        var results = array
            .Select((x, i) =>
            {
                string name = x.Name;
                name += x.Name == x.NativeName ? string.Empty : $" ({x.NativeName})";
                name += $" ({x.ISO6391})";
                name += i == last - 1 && excess > 0 ? $" ({localizer["NumberExcess", excess]})" : string.Empty;
                return new AutocompleteResult(name, x.ISO6391);
            })
            .Take(25);

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}