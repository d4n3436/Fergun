using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.WaybackMachine
{
    public static class WaybackApi
    {
        public const string ApiEndpoint = "http://archive.org/wayback/available";

        private static readonly HttpClient _httpClient = new HttpClient { BaseAddress = new Uri(ApiEndpoint) };

        public static async Task<WaybackResponse> GetSnapshotAsync(string url, ulong timestamp)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }
            double length = Math.Floor(Math.Log10(timestamp) + 1);
            if (length < 4 || length > 14)
            {
                throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "Timestamp length must be between 1 and 14.");
            }

            string json = await _httpClient.GetStringAsync(new Uri($"?url={url}&timestamp={timestamp}", UriKind.Relative));
            return JsonConvert.DeserializeObject<WaybackResponse>(json);
        }
    }
}