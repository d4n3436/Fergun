using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.Dictionary
{
    public class DefinitionCategory
    {
        [JsonProperty("word")]
        public string Word { get; set; }

        [JsonProperty("phonetics")]
        public IReadOnlyList<Phonetic> Phonetics { get; set; } = new List<Phonetic>();

        [JsonProperty("origin")]
        public string Origin { get; set; }

        [JsonProperty("meanings")]
        public IReadOnlyList<Meaning> Meanings { get; set; } = new List<Meaning>();
    }

    public class Meaning
    {
        [JsonProperty("partOfSpeech")]
        public string PartOfSpeech { get; set; }

        [JsonProperty("definitions")]
        public IReadOnlyList<DefinitionInfo> Definitions { get; set; } = new List<DefinitionInfo>();
    }

    public class DefinitionInfo
    {
        [JsonProperty("definition")]
        public string Definition { get; set; }

        [JsonProperty("example")]
        public string Example { get; set; }

        [JsonProperty("synonyms")]
        public IReadOnlyList<string> Synonyms { get; set; } = new List<string>();

        [JsonProperty("antonyms")]
        public IReadOnlyList<string> Antonyms { get; set; } = new List<string>();
    }

    public class Phonetic
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("audio")]
        public string Audio { get; set; }
    }

    public class SimpleDefinitionInfo : DefinitionInfo
    {
        public string Word { get; set; }

        public string PartOfSpeech { get; set; }
    }
}