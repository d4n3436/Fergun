using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
        public async Task<TranslationResult> TranslateAsync(string text, string to, string from = "auto")
        {
            string q = "?client=dict-chrome-ex" +
                       $"&sl={from}" +
                       $"&tl={to}" +
                       $"&q={Uri.EscapeDataString(text)}";

            // TODO:
            // Find a way to avoid getting incomplete translations (has_untranslatable_chunk).
            string json = await _httpClient.GetStringAsync(new Uri(q, UriKind.Relative)).ConfigureAwait(false);
            var model = JsonConvert.DeserializeObject<TranslationModel>(json);

            var sentence = model.Sentences.Count == 0 ? null : model.Sentences[0];
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
                ld.Add(new LanguageDetection(modelLd.SourceLanguages[i], modelLd.SourceLanguageConfidences[i]));
            }

            return new TranslationResult(sentence?.Translation, text, to, model.Source, sentence?.Transliteration,
                model.Confidence, alts.AsReadOnly(), ld.AsReadOnly());
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
    }
}