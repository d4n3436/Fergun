using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.Genius
{
    public class GeniusApi
    {
        public static string ApiEndpoint => "https://api.genius.com";

        private readonly string _apiToken;

        public GeniusApi(string apiToken)
        {
            _apiToken = apiToken;
        }

        public async Task<GeniusResponse> SearchAsync(string query)
        {
            if (string.IsNullOrEmpty(_apiToken))
            {
                throw new ArgumentNullException(nameof(_apiToken), "You must provide a valid token.");
            }
            string response;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
                response = await client.GetStringAsync(new Uri($"/search?q={Uri.EscapeDataString(query)}", UriKind.Relative));
            }
            return JsonConvert.DeserializeObject<GeniusResponse>(response);
        }
    }
}