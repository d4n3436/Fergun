using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.BingTranslator
{
    public class BingResult
    {
        /// <summary>
        /// Gets the language detection info.
        /// </summary>
        [JsonProperty("detectedLanguage")]
        public DetectedLanguage DetectedLanguage { get; } = new DetectedLanguage();

        /// <summary>
        /// Gets a read-only list of translations.
        /// </summary>
        [JsonProperty("translations")]
        public IReadOnlyList<Translation> Translations { get; private set; } = Array.Empty<Translation>();
    }

    public class DetectedLanguage
    {
        /// <summary>
        /// Gets the detected language.
        /// </summary>
        [JsonProperty("language")]
        public string Language { get; private set; }

        /// <summary>
        /// Gets the detection score.
        /// </summary>
        [JsonProperty("score")]
        public float Score { get; private set; }
    }

    public class Translation
    {
        /// <summary>
        /// Gets the translated text.
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; private set; }

        /// <summary>
        /// Gets the target language.
        /// </summary>
        [JsonProperty("to")]
        public string To { get; private set; }

        /// <summary>
        /// Gets the info about the sent text line lengths.
        /// </summary>
        [JsonProperty("sentLen")]
        public SentLen SentLen { get; private set; }
    }

    public class SentLen
    {
        /// <summary>
        /// Gets a read-only list containing the length of every line in the source text.
        /// </summary>
        [JsonProperty("srcSentLen")]
        public IReadOnlyList<int> SrcSentLen { get; private set; } = Array.Empty<int>();

        /// <summary>
        /// Gets a read-only list containing the length of every line in the translated text.
        /// </summary>
        [JsonProperty("transSentLen")]
        public IReadOnlyList<int> TransSentLen { get; private set; } = Array.Empty<int>();
    }
}