using System.Runtime.CompilerServices;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;

namespace Fergun;

/// <summary>
/// Represents an aggregation of translators where the order of the translators can be modified.
/// </summary>
public class FergunTranslator : IFergunTranslator
{
    /// <inheritdoc/>
    public string Name => nameof(FergunTranslator);

    private readonly ITranslator[] _translators;
    private WrapBackEnumerable<ITranslator> _wrappedTranslators;

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
        _translators = new ITranslator[] {googleTranslator, googleTranslator2, microsoftTranslator, yandexTranslator};
        _wrappedTranslators = new WrapBackEnumerable<ITranslator>(_translators);
    }

    /// <inheritdoc/>
    public void Next()
    {
        _wrappedTranslators.Index = _wrappedTranslators.Index == _translators.Length - 1 ? 0 : _wrappedTranslators.Index + 1;
    }

    /// <inheritdoc/>
    public void Randomize()
    {
        _translators.Shuffle();
        _wrappedTranslators.Index = Random.Shared.Next(0, _translators.Length);
    }

    /// <summary>
    /// Translates a text using the available translation services.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="toLanguage">The target language.</param>
    /// <param name="fromLanguage">The source language.</param>
    /// <returns>A task containing the translation result.</returns>
    /// <remarks>This method will attempt to use all the translation services passed in the constructor, in the order they were provided.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this translator has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="toLanguage"/> are null.</exception>
    /// <exception cref="TranslatorException">Thrown when no translator supports <paramref name="toLanguage"/> or <paramref name="fromLanguage"/>.</exception>
    /// <exception cref="AggregateException">Thrown when all translators fail to provide a valid result.</exception>
    public async Task<ITranslationResult> TranslateAsync(string text, string toLanguage, string? fromLanguage = null)
    {
        LanguageSupported(this, toLanguage, fromLanguage);

        List<Exception> exceptions = null!;
        foreach (var translator in _wrappedTranslators)
        {
            if (!translator.IsLanguageSupported(toLanguage) || fromLanguage != null && !translator.IsLanguageSupported(fromLanguage))
            {
                continue;
            }

            try
            {
                return await translator.TranslateAsync(text, toLanguage, fromLanguage).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(e);
            }
        }

        if (exceptions is null)
        {
            throw new TranslatorException($"No available translator supports the translation of the provided text from \"{fromLanguage}\" to \"{toLanguage}\".");
        }

        throw new AggregateException("No translator provided a valid result.", exceptions);
    }

    /// <inheritdoc cref="TranslateAsync(string, string, string)"/>
    public async Task<ITranslationResult> TranslateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null)
    {
        LanguageSupported(this, toLanguage, fromLanguage);

        List<Exception> exceptions = null!;
        foreach (var translator in _wrappedTranslators)
        {
            if (!translator.IsLanguageSupported(toLanguage) || fromLanguage != null && !translator.IsLanguageSupported(fromLanguage))
            {
                continue;
            }

            try
            {
                return await translator.TranslateAsync(text, toLanguage, fromLanguage).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(e);
            }
        }

        if (exceptions is null)
        {
            throw new TranslatorException($"No available translator supports the translation of the provided text from \"{fromLanguage}\" to \"{toLanguage}\".");
        }

        throw new AggregateException("No translator provided a valid result.", exceptions);
    }

    /// <summary>
    /// Transliterates a text using the available translation services.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="toLanguage">The target language.</param>
    /// <param name="fromLanguage">The source language.</param>
    /// <returns>A task containing the transliteration result.</returns>
    /// <remarks>This method will attempt to use all the translation services passed in the constructor, in the order they were provided.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this translator has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="toLanguage"/> are null.</exception>
    /// <exception cref="ArgumentException">Thrown when a <see cref="Language"/> could not be obtained from <paramref name="toLanguage"/> or <paramref name="fromLanguage"/>.</exception>
    /// <exception cref="TranslatorException">Thrown when no translator supports <paramref name="toLanguage"/> or <paramref name="fromLanguage"/>.</exception>
    /// <exception cref="AggregateException">Thrown when all translators fail to provide a valid result.</exception>
    public async Task<ITransliterationResult> TransliterateAsync(string text, string toLanguage, string? fromLanguage = null)
    {
        LanguageFound(toLanguage, out var toLang, "Unknown target language.");
        LanguageFound(fromLanguage, out var fromLang, "Unknown source language.");

        return await TransliterateAsync(text, toLang, fromLang).ConfigureAwait(false);
    }

    /// <inheritdoc cref="TransliterateAsync(string, string, string)"/>
    public async Task<ITransliterationResult> TransliterateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null)
    {
        LanguageSupported(this, toLanguage, fromLanguage);

        List<Exception> exceptions = null!;
        foreach (var translator in _wrappedTranslators)
        {
            if (!translator.IsLanguageSupported(toLanguage) || fromLanguage != null && !translator.IsLanguageSupported(fromLanguage))
            {
                continue;
            }

            try
            {
                return await translator.TransliterateAsync(text, toLanguage, fromLanguage).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(e);
            }
        }

        if (exceptions is null)
        {
            throw new TranslatorException($"No available translator supports the transliteration of the provided text from \"{fromLanguage}\" to \"{toLanguage}\".");
        }

        throw new AggregateException("No translator provided a valid result.", exceptions);
    }

    /// <summary>
    /// Detects the language of a text using the available translation services.
    /// </summary>
    /// <param name="text">The text to detect its language.</param>
    /// <returns>A task that represents the asynchronous language detection operation. The task contains the detected language.</returns>
    /// <remarks>This method will attempt to use all the translation services passed in the constructor, in the order they were provided.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this translator has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="AggregateException">Thrown when all translators fail to provide a valid result.</exception>
    public async Task<ILanguage> DetectLanguageAsync(string text)
    {
        List<Exception> exceptions = null!;
        foreach (var translator in _wrappedTranslators)
        {
            try
            {
                return await translator.DetectLanguageAsync(text).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(e);
            }
        }

        throw new AggregateException("No translator provided a valid result.", exceptions);
    }

    /// <summary>
    /// Returns whether at least one translator supports the specified language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns><see langword="true"/> if the language is supported by at least one translator, otherwise <see langword="false"/>.</returns>
    public bool IsLanguageSupported(string language)
    {
        foreach (var translator in _wrappedTranslators)
        {
            if (translator.IsLanguageSupported(language))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc cref="IsLanguageSupported(string)"/>
    public bool IsLanguageSupported(ILanguage language)
    {
        foreach (var translator in _wrappedTranslators)
        {
            if (translator.IsLanguageSupported(language))
            {
                return true;
            }
        }

        return false;
    }

    private static void LanguageFound(string? language, out Language lang, string message = "Unknown language.",
        [CallerArgumentExpression("language")] string? parameterName = null)
    {
        Language temp = null!;
        if (language is not null && !Language.TryGetLanguage(language, out temp!))
        {
            throw new ArgumentException(message, parameterName);
        }

        lang = temp;
    }

    private static void LanguageSupported(ITranslator translator, string toLanguage, string? fromLanguage)
    {
        if (!translator.IsLanguageSupported(toLanguage))
        {
            throw new TranslatorException($"No available translator supports the target language \"{toLanguage}\".", translator.Name);
        }

        if (!string.IsNullOrEmpty(fromLanguage) && !translator.IsLanguageSupported(fromLanguage))
        {
            throw new TranslatorException($"No available translator supports the source language \"{fromLanguage}\".", translator.Name);
        }
    }

    private static void LanguageSupported(ITranslator translator, ILanguage toLanguage, ILanguage? fromLanguage)
    {
        if (!translator.IsLanguageSupported(toLanguage))
        {
            throw new TranslatorException($"No available translator supports the target language \"{toLanguage.ISO6391}\".", translator.Name);
        }

        if (fromLanguage != null && !translator.IsLanguageSupported(fromLanguage))
        {
            throw new TranslatorException($"No available translator supports the source language \"{fromLanguage.ISO6391}\".", translator.Name);
        }
    }
}