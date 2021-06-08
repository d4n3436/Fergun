using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.BingTranslator
{
    public class BingTranslator : IDisposable
    {
        /// <summary>
        /// Returns the default API host.
        /// </summary>
        public const string DefaultApiEndpoint = "https://www.bing.com/ttranslatev3";

        /// <summary>
        /// Returns the default User-Agent header.
        /// </summary>
        public const string DefaultUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36";

        private readonly HttpClient _httpClient = new HttpClient();
        private string _apiEndpoint;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BingTranslator"/> class.
        /// </summary>
        public BingTranslator()
        {
            Init(DefaultApiEndpoint, DefaultUserAgent);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BingTranslator"/> class with the provided API endpoint.
        /// </summary>
        public BingTranslator(string apiEndpoint)
        {
            Init(apiEndpoint, DefaultUserAgent);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BingTranslator"/> class with the provided API endpoint and User-Agent header.
        /// </summary>
        public BingTranslator(string apiEndpoint, string userAgent)
        {
            Init(apiEndpoint, userAgent);
        }

        private void Init(string apiEndpoint, string userAgent)
        {
            _apiEndpoint = apiEndpoint;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        /// <summary>
        /// Translates a text with Bing Translator.
        /// </summary>
        /// <param name="text">The text to translate.</param>
        /// <param name="toLanguage">The target language.</param>
        /// <param name="fromLanguage">The source language.</param>
        /// <returns>>A task that represents the asynchronous translation operation. The task contains the translation result.</returns>
        /// <exception cref="BingTokenNotFoundException">Thrown when the Bing token cannot not be found.</exception>
        public async Task<IReadOnlyList<BingResult>> TranslateAsync(string text, string toLanguage, string fromLanguage = "auto-detect")
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (SupportedLanguages.All(x => x != toLanguage))
            {
                throw new ArgumentException("Invalid target language.", nameof(toLanguage));
            }
            if (fromLanguage != "auto-detect" && SupportedLanguages.All(x => x != fromLanguage))
            {
                throw new ArgumentException("Invalid source language.", nameof(fromLanguage));
            }

            // Convert Google Translate language codes to Bing Translator equivalent.
            toLanguage = toLanguage switch
            {
                "no" => "nb",
                "pt" => "pt-pt",
                "zh-CN" => "zh-Hans",
                "zh-TW" => "zh-Hant",
                _ => toLanguage
            };

            (string key, string token) = await GetCredentialsAsync();

            var data = new Dictionary<string, string>
            {
                { "fromLang", fromLanguage },
                { "text", text },
                { "to", toLanguage },
                { "token", token },
                { "key", key }
            };

            string json;
            using (var content = new FormUrlEncodedContent(data))
            {
                var response = await _httpClient.PostAsync(new Uri(_apiEndpoint), content);
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }

            return JsonConvert.DeserializeObject<List<BingResult>>(json);
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

        private async Task<(string, string)> GetCredentialsAsync()
        {
            const string credentialsStart = "var params_RichTranslateHelper = [";

            string content = await _httpClient.GetStringAsync(new Uri("https://www.bing.com/translator"));

            int credentialsStartIndex = content.IndexOf(credentialsStart, StringComparison.Ordinal);
            if (credentialsStartIndex == -1)
            {
                throw new BingTokenNotFoundException("Unable to find the Bing credentials.");
            }

            int keyStartIndex = credentialsStartIndex + credentialsStart.Length;
            int keyEndIndex = content.IndexOf(',', keyStartIndex);
            if (keyEndIndex == -1)
            {
                throw new BingTokenNotFoundException("Unable to find the Bing key.");
            }

            string key = content.Substring(keyStartIndex, keyEndIndex - keyStartIndex);

            int tokenStartIndex = keyEndIndex + 2;
            int tokenEndIndex = content.IndexOf('"', tokenStartIndex);
            if (tokenEndIndex == -1)
            {
                throw new BingTokenNotFoundException("Unable to find the Bing token.");
            }

            string token = content.Substring(tokenStartIndex, tokenEndIndex - tokenStartIndex);

            return (key, token);
        }

        /// <summary>
        /// Gets a read-only list containing the supported ISO 639-1 language codes.
        /// </summary>
        public static IReadOnlyList<string> SupportedLanguages { get; } = new[]
        {
            "af",
            "ar",
            "bn",
            "bs",
            "bg",
            "ca",
            "zh-CN",
            "zh-TW",
            "hr",
            "cs",
            "da",
            "nl",
            "no",
            "en",
            "et",
            "fi",
            "fr",
            "de",
            "el",
            "gu",
            "ht",
            "hi",
            "hu",
            "is",
            "id",
            "ga",
            "it",
            "ja",
            "kn",
            "kk",
            "ko",
            "lv",
            "lt",
            "mg",
            "ml",
            "mt",
            "mi",
            "mr",
            "fa",
            "pl",
            "pt",
            "ro",
            "ru",
            "sm",
            "sk",
            "sl",
            "es",
            "sw",
            "sv",
            "ta",
            "te",
            "th",
            "tr",
            "uk",
            "ur",
            "vi",
            "cy"
        };
    }

    [Serializable]
    public class BingTokenNotFoundException : Exception
    {
        public BingTokenNotFoundException()
        {
        }

        public BingTokenNotFoundException(string message) : base(message)
        {
        }

        public BingTokenNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}