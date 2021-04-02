using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Fergun.APIs.GTranslate
{
    /// <summary>
    /// Represents the Google Translator.
    /// </summary>
    public class GTranslator : IDisposable
    {
        /// <summary>
        /// Returns the default API endpoint.
        /// </summary>
        public const string DefaultApiEndpoint = "https://clients5.google.com/translate_a/t";

        /// <summary>
        /// Returns the default User-Agent header.
        /// </summary>
        public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36";

        private readonly HttpClient _httpClient = new HttpClient();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GTranslator"/> class.
        /// </summary>
        public GTranslator()
        {
            Init(DefaultApiEndpoint, DefaultUserAgent);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GTranslator"/> class with the provided API endpoint.
        /// </summary>
        public GTranslator(string apiEndpoint)
        {
            Init(apiEndpoint, DefaultUserAgent);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GTranslator"/> class with the provided API endpoint and User-Agent header.
        /// </summary>
        public GTranslator(string apiEndpoint, string userAgent)
        {
            Init(apiEndpoint, userAgent);
        }

        private void Init(string apiEndpoint, string userAgent)
        {
            _httpClient.BaseAddress = new Uri(apiEndpoint);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        /// <summary>
        /// Translates a text to the specified language.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="to">The target language.</param>
        /// <param name="from">The source language.</param>
        /// <returns>A task that represents the asynchronous translation operation. The task contains the translation result.</returns>
        /// <exception cref="TranslationException">Thrown when an error occurs during the translation process.</exception>
        public async Task<TranslationResult> TranslateAsync(string text, string to, string from = "auto")
        {
            string q = "?client=dict-chrome-ex" +
                       $"&sl={from}" +
                       $"&tl={to}" +
                       $"&q={Uri.EscapeDataString(text)}";

            string json = await _httpClient.GetStringAsync(new Uri(q, UriKind.Relative)).ConfigureAwait(false);
            try
            {
                var model = JsonConvert.DeserializeObject<TranslationModel>(json);

                var alts = new List<AlternativeTranslation>();
                if (model.AlternativeTranslations.Count > 0)
                {
                    foreach (var alt in model.AlternativeTranslations[0].Alternative)
                    {
                        alts.Add(new AlternativeTranslation(alt.WordPostProcess, alt.Score));
                    }
                }

                var ld = new List<LanguageDetection>();
                var modelLd = model.LanguageDetection;

                for (int i = 0; i < modelLd.SourceLanguages.Count; i++)
                {
                    ld.Add(new LanguageDetection(model.LanguageDetection.SourceLanguages[i], modelLd.SourceLanguageConfidences[i]));
                }

                return new TranslationResult(string.Concat(model.Sentences.Select(x => x.Translation)), text, to,
                    model.Source, string.Concat(model.Sentences.Select(x => x.Transliteration)), model.Confidence,
                    alts.AsReadOnly(), ld.AsReadOnly());
            }
            catch (JsonSerializationException)
            {
                var response = JToken.Parse(json)
                .FirstOrDefault()?
                .FirstOrDefault();

                string translation = response?
                    .FirstOrDefault()?
                    .FirstOrDefault()?
                    .FirstOrDefault()?
                    .ToString();

                if (translation == null)
                {
                    throw new TranslationException("Error parsing the translation response.");
                }

                string sourceLanguage = response
                    .ElementAtOrDefault(2)?
                    .ToString() ?? "";

                return new TranslationResult(translation, text, to, sourceLanguage);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc cref="Dispose()"/>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _httpClient.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Gets a read-only dictionary containing the ISO 639-1 language codes (keys) and English names (values) of the supported languages.
        /// </summary>
        public static IReadOnlyDictionary<string, string> SupportedLanguages { get; } = new Dictionary<string, string>
        {
            { "af", "Afrikaans" },
            { "sq", "Albanian" },
            { "am", "Amharic" },
            { "ar", "Arabic" },
            { "hy", "Armenian" },
            { "az", "Azerbaijani" },
            { "eu", "Basque" },
            { "be", "Belarusian" },
            { "bn", "Bengali" },
            { "bs", "Bosnian" },
            { "bg", "Bulgarian" },
            { "ca", "Catalan" },
            { "ceb", "Cebuano" },
            { "ny", "Chichewa" },
            { "zh-CN", "Chinese Simplified" },
            { "zh-TW", "Chinese Traditional" },
            { "co", "Corsican" },
            { "hr", "Croatian" },
            { "cs", "Czech" },
            { "da", "Danish" },
            { "nl", "Dutch" },
            { "en", "English" },
            { "eo", "Esperanto" },
            { "et", "Estonian" },
            { "tl", "Filipino" },
            { "fi", "Finnish" },
            { "fr", "French" },
            { "fy", "Frisian" },
            { "gl", "Galician" },
            { "ka", "Georgian" },
            { "de", "German" },
            { "el", "Greek" },
            { "gu", "Gujarati" },
            { "ht", "Haitian Creole" },
            { "ha", "Hausa" },
            { "haw", "Hawaiian" },
            { "iw", "Hebrew" },
            { "hi", "Hindi" },
            { "hmn", "Hmong" },
            { "hu", "Hungarian" },
            { "is", "Icelandic" },
            { "ig", "Igbo" },
            { "id", "Indonesian" },
            { "ga", "Irish" },
            { "it", "Italian" },
            { "ja", "Japanese" },
            { "jw", "Javanese" },
            { "kn", "Kannada" },
            { "kk", "Kazakh" },
            { "km", "Khmer" },
            { "ko", "Korean" },
            { "ku", "Kurdish (Kurmanji)" },
            { "ky", "Kyrgyz" },
            { "lo", "Lao" },
            { "la", "Latin" },
            { "lv", "Latvian" },
            { "lt", "Lithuanian" },
            { "lb", "Luxembourgish" },
            { "mk", "Macedonian" },
            { "mg", "Malagasy" },
            { "ms", "Malay" },
            { "ml", "Malayalam" },
            { "mt", "Maltese" },
            { "mi", "Maori" },
            { "mr", "Marathi" },
            { "mn", "Mongolian" },
            { "my", "Myanmar (Burmese)" },
            { "ne", "Nepali" },
            { "no", "Norwegian" },
            { "ps", "Pashto" },
            { "fa", "Persian" },
            { "pl", "Polish" },
            { "pt", "Portuguese" },
            { "ma", "Punjabi" },
            { "ro", "Romanian" },
            { "ru", "Russian" },
            { "sm", "Samoan" },
            { "gd", "Scots Gaelic" },
            { "sr", "Serbian" },
            { "st", "Sesotho" },
            { "sn", "Shona" },
            { "sd", "Sindhi" },
            { "si", "Sinhala" },
            { "sk", "Slovak" },
            { "sl", "Slovenian" },
            { "so", "Somali" },
            { "es", "Spanish" },
            { "su", "Sundanese" },
            { "sw", "Swahili" },
            { "sv", "Swedish" },
            { "tg", "Tajik" },
            { "ta", "Tamil" },
            { "te", "Telugu" },
            { "th", "Thai" },
            { "tr", "Turkish" },
            { "uk", "Ukrainian" },
            { "ur", "Urdu" },
            { "uz", "Uzbek" },
            { "vi", "Vietnamese" },
            { "cy", "Welsh" },
            { "xh", "Xhosa" },
            { "yi", "Yiddish" },
            { "yo", "Yoruba" },
            { "zu", "Zulu" }
        };
    }
}