using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs
{
    public static class Hastebin
    {
        public const string ApiEndpoint = "https://hastebin.com";

        private static readonly HttpClient _client = new HttpClient { BaseAddress = new Uri(ApiEndpoint) };

        public static async Task<HastebinResponse> UploadAsync(string content)
        {
            using var stringContent = new StringContent(content, Encoding.UTF8);
            var response = await _client.PostAsync(new Uri("/documents", UriKind.Relative), stringContent);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<HastebinResponse>(json);
        }

        public static string GetLink(this HastebinResponse response) => $"{ApiEndpoint}/{response.Key}";
    }

    public class HastebinResponse
    {
        [JsonProperty("key")]
        public string Key { get; set; }
    }
}