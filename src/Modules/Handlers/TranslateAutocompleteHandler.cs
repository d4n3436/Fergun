using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate;

namespace Fergun.Modules.Handlers;

public class TranslateAutocompleteHandler : AutocompleteHandler
{
    /// <inheritdoc />
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var text = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        IEnumerable<Language> languages = Language
            .LanguageDictionary
            .Values
            .Where(x => x.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                        x.ISO6391.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                        x.ISO6393.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name);

        if (parameter.Name == "source")
        {
            string? target = autocompleteInteraction.Data.Options.FirstOrDefault(x => x.Name == "target")?.Value as string;

            if (string.IsNullOrWhiteSpace(target) || !Language.TryGetLanguage(target, out var language))
                return Task.FromResult(AutocompletionResult.FromSuccess());

            // Return only languages that supports both target and source languages
            languages = languages
                .Where(x => x.IsServiceSupported(language.SupportedServices));
        }
        else
        {
            if (context.Interaction.TryGetLanguage(out var language))
            {
                languages = languages.Where(x => !x.Equals(language)).Prepend(language);
            }
        }

        var results = languages
            .Select(x => new AutocompleteResult($"{x.Name} ({x.ISO6391})", x.ISO6391))
            .Take(25);

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}