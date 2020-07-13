using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs
{
    public static class Translators
    {
        public const string YandexHost = "http://translate.yandex.net";
        public const string BingHost = "https://www.bing.com";
        private static readonly HttpClient _httpClient = new HttpClient();

        private static readonly Regex _sidRegex = new Regex(@"SID: '(.+)',");
        private static string _sid = null;

        static Translators()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Connection.ParseAdd("close");
            _httpClient.DefaultRequestHeaders.ConnectionClose = true;
        }

        private static async Task<string> GetYandexSIDAsync()
        {
            string response = await _httpClient.GetStringAsync(new Uri("https://translate.yandex.com"));
            var match = _sidRegex.Match(response);
            if (match.Success)
            {
                string[] split = match.Groups[1].Value.Split('.');

                if (split.Length < 3) return null;
                return $"{Reverse(split[0])}.{Reverse(split[1])}.{Reverse(split[2])}";
            }
            return null;
        }

        public static async Task<YandexResult> TranslateYandexAsync(string text, string toLanguage, string fromLanguage = "auto")
        {
            _sid ??= await GetYandexSIDAsync() ?? throw new Exception("Unable to get the SID.");

            string query = $"/api/v1/tr.json/translate?id={_sid}-0-0&srv=tr-text";
            query += $"&text={Uri.EscapeDataString(text)}";
            query += $"&lang={(fromLanguage == "auto" ? toLanguage : $"{fromLanguage}-{toLanguage}")}";
            query += "&reason=auto";
            query += "&format=text";

            string response = await _httpClient.GetStringAsync(new Uri($"{YandexHost}{query}"));
            return JsonConvert.DeserializeObject<YandexResult>(response);
        }

        public static async Task<List<BingResult>> TranslateBingAsync(string text, string toLanguage, string fromLanguage = "auto-detect")
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

        public class YandexResult
        {
            [JsonProperty("code")]
            public long Code { get; set; }

            [JsonProperty("lang")]
            public string Lang { get; set; }

            [JsonProperty("text")]
            public List<string> Text { get; set; }

            public string MergedTranslation => string.Concat(Text);
        }

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

        private static string Reverse(string input)
        {
            return new string(input.Reverse().ToArray());
        }

        public static IList<string> SupportedLanguages => _bingLanguageCodes;

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