using System.Collections.Generic;

namespace Fergun.APIs.GTranslate
{
    /// <summary>
    /// Represents the translation result.
    /// </summary>
    public class TranslationResult
    {
        internal TranslationResult(string translation, string source, string targetLanguage, string sourceLanguage,
            string transliteration, double confidence, IReadOnlyList<AlternativeTranslation> alternativeTranslations,
            IReadOnlyList<LanguageDetection> languageDetections)
        {
            Translation = translation;
            Source = source;
            TargetLanguage = targetLanguage;
            SourceLanguage = sourceLanguage;
            Transliteration = transliteration;
            Confidence = confidence;
            AlternativeTranslations = alternativeTranslations;
            LanguageDetections = languageDetections;
        }

        /// <summary>
        /// Gets the translated text.
        /// </summary>
        public string Translation { get; }

        /// <summary>
        /// Gets the source text.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the target language.
        /// </summary>
        public string TargetLanguage { get; }

        /// <summary>
        /// Gets the source language.
        /// </summary>
        public string SourceLanguage { get; }

        /// <summary>
        /// Gets the transliteration of the translation.
        /// </summary>
        public string Transliteration { get; }

        /// <summary>
        /// Gets the translation confidence.
        /// </summary>
        public double Confidence { get; }

        /// <summary>
        /// Gets the alternative translations.
        /// </summary>
        public IReadOnlyList<AlternativeTranslation> AlternativeTranslations { get; }

        public IReadOnlyList<LanguageDetection> LanguageDetections { get; }
    }

    /// <summary>
    /// Represents an alternative translation.
    /// </summary>
    public class AlternativeTranslation
    {
        internal AlternativeTranslation(string translation, int score)
        {
            Translation = translation;
            Score = score;
        }

        /// <summary>
        /// Gets the translation.
        /// </summary>
        public string Translation { get; }

        /// <summary>
        /// Gets the score.
        /// </summary>
        public int Score { get; }
    }

    /// <summary>
    /// Represents a language detection.
    /// </summary>
    public class LanguageDetection
    {
        internal LanguageDetection(string sourceLanguage, double confidence)
        {
            SourceLanguage = sourceLanguage;
            Confidence = confidence;
        }

        /// <summary>
        /// Gets the source language.
        /// </summary>
        public string SourceLanguage { get; }

        /// <summary>
        /// Gets the confidence.
        /// </summary>
        public double Confidence { get; }
    }
}