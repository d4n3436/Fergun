using System.Threading.Tasks;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;

namespace Fergun;

/// <summary>
/// Represents an aggregation of translators where the order of the translators can be randomized.
/// </summary>
public class FergunTranslator : IFergunTranslator
{
    private readonly ITranslator[] _translators;
    private readonly AggregateTranslator _innerTranslator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FergunTranslator"/> class.
    /// </summary>
    /// <param name="googleTranslator">The Google Translator.</param>
    /// <param name="googleTranslator2">The new Google Translator.</param>
    /// <param name="microsoftTranslator">The Microsoft translator.</param>
    /// <param name="yandexTranslator">The Yandex Translator.</param>
    public FergunTranslator(GoogleTranslator googleTranslator, GoogleTranslator2 googleTranslator2,
        MicrosoftTranslator microsoftTranslator, YandexTranslator yandexTranslator)
    {
        _translators = new ITranslator[]
        {
            googleTranslator,
            googleTranslator2,
            microsoftTranslator,
            yandexTranslator
        };

        _innerTranslator = new AggregateTranslator(_translators);
    }

    /// <inheritdoc/>
    public string Name => nameof(FergunTranslator);

    public void Randomize() => _translators.Shuffle();

    /// <inheritdoc />
    public Task<ITranslationResult> TranslateAsync(string text, string toLanguage, string? fromLanguage = null)
        => _innerTranslator.TranslateAsync(text, toLanguage, fromLanguage);

    /// <inheritdoc />
    public Task<ITranslationResult> TranslateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null)
        => _innerTranslator.TranslateAsync(text, toLanguage, fromLanguage);

    /// <inheritdoc />
    public Task<ITransliterationResult> TransliterateAsync(string text, string toLanguage, string? fromLanguage = null)
        => _innerTranslator.TransliterateAsync(text, toLanguage, fromLanguage);

    /// <inheritdoc />
    public Task<ITransliterationResult> TransliterateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null)
        => _innerTranslator.TransliterateAsync(text, toLanguage, fromLanguage);

    /// <inheritdoc />
    public Task<ILanguage> DetectLanguageAsync(string text) => _innerTranslator.DetectLanguageAsync(text);

    /// <inheritdoc />
    public bool IsLanguageSupported(string language) => _innerTranslator.IsLanguageSupported(language);

    /// <inheritdoc />
    public bool IsLanguageSupported(ILanguage language) => _innerTranslator.IsLanguageSupported(language);
}