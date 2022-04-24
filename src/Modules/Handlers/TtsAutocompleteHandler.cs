using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Translators;

namespace Fergun.Modules.Handlers;

public class TtsAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        string text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

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
            languages = languages.Where(x => !x.Equals(userLanguage)).Prepend(userLanguage);
        }

        var results = languages
            .Select(x => new AutocompleteResult($"{x.Name}{(x.Name == x.NativeName ? "" : $" ({x.NativeName})")} ({x.ISO6391})", x.ISO6391))
            .Take(25);

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}