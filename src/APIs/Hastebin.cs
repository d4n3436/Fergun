using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs
{
    public static class Hastebin
    {
        public const string HastebinEndpoint = "https://hastebin.com";

        public const string HatebinEndpoint = "https://hatebin.com";

        private static readonly HttpClient _client = new HttpClient();

        public static async Task<string> UploadAsync(string content)
        {
            using var stringContent = new StringContent(content, Encoding.UTF8);
            var response = await _client.PostAsync(new Uri($"{HastebinEndpoint}/documents"), stringContent);

            if (!response.IsSuccessStatusCode)
            {
                // Fallback to Hatebin

                using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "text", content }
                });

                response = await _client.PostAsync(new Uri($"{HatebinEndpoint}/index.php"), formContent);
                response.EnsureSuccessStatusCode();

                string key = await response.Content.ReadAsStringAsync();
                return GetUrl(HatebinEndpoint, key);
            }

            string json = await response.Content.ReadAsStringAsync();
            var temp = JsonConvert.DeserializeObject<HastebinResponse>(json);
            return GetUrl(HastebinEndpoint, temp.Key);
        }

        private static string GetUrl(string endpoint, string key) => $"{endpoint}/{key.Trim()}";

        private class HastebinResponse
        {
            public string Key { get; }
        }
    }
}