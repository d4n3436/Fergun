namespace Fergun.APIs.GTranslate
{
    /// <summary>
    /// Represents the translation result.
    /// </summary>
    public class TranslationResult
    {
        internal TranslationResult(string translation, string source, string targetLanguage, string sourceLanguage)
        {
            Translation = translation;
            Source = source;
            TargetLanguage = targetLanguage;
            SourceLanguage = sourceLanguage;
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
    }
}