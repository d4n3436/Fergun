using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.BingTranslator
{
    public static class BingTranslatorApi
    {
        public static IList<string> SupportedLanguages => _bingLanguageCodes;
        public const string BingHost = "https://www.bing.com";

        private static readonly HttpClient _httpClient = new HttpClient();

        static BingTranslatorApi()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Connection.ParseAdd("close");
            _httpClient.DefaultRequestHeaders.ConnectionClose = true;
        }

        public static async Task<List<BingResult>> TranslateAsync(string text, string toLanguage, string fromLanguage = "auto-detect")
        {
            switch (toLanguage)
            {
                case "bs":
                    toLanguage = "bs-Latn";
                    break;

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

            if (SupportedLanguages.IndexOf(toLanguage) == -1)
            {
                throw new ArgumentException("Invalid target language.", nameof(toLanguage));
            }
            if (fromLanguage != "auto-detect" && SupportedLanguages.IndexOf(fromLanguage) == -1)
            {
                throw new ArgumentException("Invalid source language.", nameof(fromLanguage));
            }

            var data = new Dictionary<string, string>
            {
                { "fromLang", fromLanguage },
                { "text", text },
                { "to", toLanguage }
            };

            var content = new FormUrlEncodedContent(data);
            string responseString;

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{BingHost}/ttranslatev3")))
            {
                request.Headers.Referrer = new Uri($"{BingHost}/ttranslatev3");
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                responseString = await response.Content.ReadAsStringAsync();
            }

            return JsonConvert.DeserializeObject<List<BingResult>>(responseString);
        }

        private static readonly string[] _bingLanguageCodes =
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
}