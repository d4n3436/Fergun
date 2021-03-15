using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.UrbanDictionary
{
    public static class UrbanApi
    {
        public const string ApiEndpoint = "https://api.urbandictionary.com/v0";

        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<UrbanResponse> SearchWordAsync(string word)
        {
            string response = await _httpClient.GetStringAsync($"{ApiEndpoint}/define?term={Uri.EscapeDataString(word)}");
            return JsonConvert.DeserializeObject<UrbanResponse>(response);
        }

        public static async Task<UrbanResponse> GetRandomWordsAsync()
        {
            string response = await _httpClient.GetStringAsync($"{ApiEndpoint}/random");
            return JsonConvert.DeserializeObject<UrbanResponse>(response);
        }
    }
}