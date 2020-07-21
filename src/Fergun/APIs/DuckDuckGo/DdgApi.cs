using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.DuckDuckGo
{
    public static class DdgApi
    {
        public const string ApiEndpoint = "https://duckduckgo.com";
        private static readonly HttpClient _client = new HttpClient();
        //private static readonly HttpRequestMessage _defaultRequestMessage = new HttpRequestMessage();
        private static readonly Regex _tokenExtractor = new Regex(@"vqd=([\d-]+)\&", RegexOptions.IgnoreCase);

        static DdgApi()
        {
            _client.BaseAddress = new Uri(ApiEndpoint);
        }

        public static async Task<DdgResponse> SearchImagesAsync(string keywords, SafeSearch filter = SafeSearch.Moderate)
        {
            string token = await GetTokenAsync(keywords);

            string query = "?";
            query += "l=us-en";
            query += "&o=json";
            query += $"&q={Uri.EscapeDataString(keywords)}";
            query += $"&vqd={token}";
            query += "&f=,,,";
            query += $"&p={(filter == SafeSearch.Off ? "-1" : "1")}";
            //query += $"&p={(filter == SafeSearch.Strict || filter == SafeSearch.Moderate ? "1" : "-1")}";
            query += "&v7exp=a";

            string content;

            using (var requestMessage = GenerateRequestMessage(HttpMethod.Get, new Uri($"i.js{query}", UriKind.Relative)))
            {
                if (filter == SafeSearch.Strict)
                {
                    requestMessage.Headers.Add("cookie", "p=1");
                }
                else if (filter == SafeSearch.Off)
                {
                    requestMessage.Headers.Add("cookie", "p=-2");
                }
                var response = await _client.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                content = await response.Content.ReadAsStringAsync();
            }

            return JsonConvert.DeserializeObject<DdgResponse>(content);
        }

        private static async Task<string> GetTokenAsync(string keywords)
        {
            var content = await _client.GetStringAsync(new Uri($"?q={Uri.EscapeDataString(keywords)}", UriKind.Relative));

            Match match = _tokenExtractor.Match(content);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            throw new TokenNotFoundException("Token not found.");
        }

        private static HttpRequestMessage GenerateRequestMessage(HttpMethod method, Uri uri)
        {
            var request = new HttpRequestMessage(method, uri);
            request.Headers.Add("authority", "duckduckgo.com");
            request.Headers.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("x-requested-with", "XMLHttpRequest");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.24 Safari/537.36");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("referer", "https://duckduckgo.com/");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            return request;
        }
    }

    [Serializable]
    public class TokenNotFoundException : Exception
    {
        public TokenNotFoundException()
        { }

        public TokenNotFoundException(string message)
            : base(message)
        { }

        public TokenNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        { }

        protected TokenNotFoundException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }
    }

    public enum SafeSearch
    {
        Off,
        Moderate,
        Strict
    }
}