using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;

namespace Fergun.Common;

/// <summary>
/// Represents an aggregation of translators where the order of the translators can be randomized.
/// </summary>
#pragma warning disable CA1001 // The translators can't be disposed because we don't own them
public class FergunTranslator : IFergunTranslator
#pragma warning restore CA1001
{
    internal readonly ITranslator[] _translators;
    private readonly AggregateTranslator _innerTranslator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FergunTranslator"/> class.
    /// </summary>
    /// <param name="translators">The translators.</param>
    public FergunTranslator(IEnumerable<ITranslator> translators)
    {
        _translators = translators.ToArray();
        _innerTranslator = new AggregateTranslator(_translators);
    }

    /// <inheritdoc/>
    public string Name => nameof(FergunTranslator);

    /// <inheritdoc/>
    public void Randomize(Random? rng = null) => (rng ?? Random.Shared).Shuffle(_translators);

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