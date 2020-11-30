using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.BingTranslator
{
    public static class BingTranslatorApi
    {
        public const string BingHost = "https://www.bing.com";

        private static readonly HttpClient _httpClient = new HttpClient();

        static BingTranslatorApi()
        {
            _httpClient.DefaultRequestHeaders.Referrer = new Uri($"{BingHost}/ttranslatev3");
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Connection.ParseAdd("close");
            _httpClient.DefaultRequestHeaders.ConnectionClose = true;
        }

        public static async Task<List<BingResult>> TranslateAsync(string text, string toLanguage, string fromLanguage = "auto-detect")
        {
            if (SupportedLanguages.All(x => x != toLanguage))
            {
                throw new ArgumentException("Invalid target language.", nameof(toLanguage));
            }
            if (fromLanguage != "auto-detect" && SupportedLanguages.All(x => x != fromLanguage))
            {
                throw new ArgumentException("Invalid source language.", nameof(fromLanguage));
            }

            // Convert Google Translate language codes to Bing Translator equivalent.
            switch (toLanguage)
            {
                case "no":
                    toLanguage = "nb";
                    break;

                case "pt":
                    toLanguage = "pt-pt";
                    break;

                case "zh-CN":
                    toLanguage = "zh-Hans";
                    break;

                case "zh-TW":
                    toLanguage = "zh-Hant";
                    break;
            }

            var data = new Dictionary<string, string>
            {
                { "fromLang", fromLanguage },
                { "text", text },
                { "to", toLanguage }
            };

            string json;
            using (var content = new FormUrlEncodedContent(data))
            {
                var response = await _httpClient.PostAsync(new Uri($"{BingHost}/ttranslatev3"), content);
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }

            return JsonConvert.DeserializeObject<List<BingResult>>(json);
        }

        public static IReadOnlyList<string> SupportedLanguages { get; } = new List<string>
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
        }.AsReadOnly();
    }
}