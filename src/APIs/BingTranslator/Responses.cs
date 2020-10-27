using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.BingTranslator
{
    public class BingResult
    {
        [JsonProperty("detectedLanguage")]
        public DetectedLanguage DetectedLanguage { get; set; }

        [JsonProperty("translations")]
        public List<Translation> Translations { get; set; }
    }

    public class DetectedLanguage
    {
        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("score")]
        public long Score { get; set; }
    }

    public class Translation
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("sentLen")]
        public SentLen SentLen { get; set; }
    }

    public class SentLen
    {
        [JsonProperty("srcSentLen")]
        public List<long> SrcSentLen { get; set; }

        [JsonProperty("transSentLen")]
        public List<long> TransSentLen { get; set; }
    }
}