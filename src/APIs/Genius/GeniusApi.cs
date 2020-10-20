using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.Genius
{
    public class GeniusApi
    {
        public const string ApiEndpoint = "https://api.genius.com";

        private static readonly HttpClient _httpClient = new HttpClient { BaseAddress = new Uri(ApiEndpoint) };

        public GeniusApi(string apiToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }

        public async Task<GeniusResponse> SearchAsync(string query)
        {
            var response = await _httpClient.GetStringAsync(new Uri($"/search?q={Uri.EscapeDataString(query)}", UriKind.Relative));
            return JsonConvert.DeserializeObject<GeniusResponse>(response);
        }
    }
}