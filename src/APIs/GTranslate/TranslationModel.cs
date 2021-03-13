using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.GTranslate
{
    public class TranslationModel
    {
        [JsonProperty("sentences")]
        public List<Sentence> Sentences { get; set; } = new List<Sentence>();

        [JsonProperty("src")]
        public string Source { get; set; }

        [JsonProperty("alternative_translations")]
        public List<AltTranslation> AlternativeTranslations { get; set; } = new List<AltTranslation>();

        [JsonProperty("confidence")]
        public double Confidence { get; set; }

        [JsonProperty("ld_result")]
        public LdResult LanguageDetection { get; set; }

        [JsonProperty("dict", NullValueHandling = NullValueHandling.Ignore)]
        public List<Dict> Dict { get; set; } = new List<Dict>();

        [JsonProperty("query_inflections", NullValueHandling = NullValueHandling.Ignore)]
        public List<QueryInflection> QueryInflections { get; set; } = new List<QueryInflection>();
    }

    public class AltTranslation
    {
        [JsonProperty("src_phrase")]
        public string SrcPhrase { get; set; }

        [JsonProperty("alternative")]
        public List<Alternative> Alternative { get; set; }

        [JsonProperty("srcunicodeoffsets")]
        public List<SourceUnicodeOffset> SourceUnicodeOffsets { get; set; }

        [JsonProperty("raw_src_segment")]
        public string RawSrcSegment { get; set; }

        [JsonProperty("start_pos")]
        public int StartPos { get; set; }

        [JsonProperty("end_pos")]
        public int EndPos { get; set; }
    }

    public class Alternative
    {
        [JsonProperty("word_postproc")]
        public string WordPostProcess { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("has_preceding_space")]
        public bool HasPrecedingSpace { get; set; }

        [JsonProperty("attach_to_next_token")]
        public bool AttachToNextToken { get; set; }
    }

    public class SourceUnicodeOffset
    {
        [JsonProperty("begin")]
        public int Begin { get; set; }

        [JsonProperty("end")]
        public int End { get; set; }
    }

    public class Dict
    {
        [JsonProperty("pos")]
        public string Position { get; set; }

        [JsonProperty("terms")]
        public List<string> Terms { get; set; } = new List<string>();

        [JsonProperty("entry")]
        public List<Entry> Entry { get; set; } = new List<Entry>();

        [JsonProperty("base_form")]
        public string BaseForm { get; set; }

        [JsonProperty("pos_enum")]
        public int PosEnum { get; set; }
    }

    public class Entry
    {
        [JsonProperty("word")]
        public string Word { get; set; }

        [JsonProperty("reverse_translation")]
        public List<string> ReverseTranslation { get; set; } = new List<string>();

        [JsonProperty("score", NullValueHandling = NullValueHandling.Ignore)]
        public double? Score { get; set; }
    }

    public class LdResult
    {
        [JsonProperty("srclangs")]
        public List<string> SourceLanguages { get; set; } = new List<string>();

        [JsonProperty("srclangs_confidences")]
        public List<double> SourceLanguageConfidences { get; set; } = new List<double>();

        [JsonProperty("extended_srclangs")]
        public List<string> ExtendedSourceLanguages { get; set; } = new List<string>();
    }

    public class QueryInflection
    {
        [JsonProperty("written_form")]
        public string WrittenForm { get; set; }

        [JsonProperty("features")]
        public Feature Features { get; set; }
    }

    public class Feature
    {
        [JsonProperty("gender", NullValueHandling = NullValueHandling.Ignore)]
        public int? Gender { get; set; }

        [JsonProperty("number")]
        public int Number { get; set; }
    }

    public class Sentence
    {
        [JsonProperty("trans", NullValueHandling = NullValueHandling.Ignore)]
        public string Translation { get; set; }

        [JsonProperty("orig", NullValueHandling = NullValueHandling.Ignore)]
        public string Origin { get; set; }

        [JsonProperty("backend", NullValueHandling = NullValueHandling.Ignore)]
        public int? Backend { get; set; }

        [JsonProperty("translit", NullValueHandling = NullValueHandling.Ignore)]
        public string Transliteration { get; set; }
    }
}
