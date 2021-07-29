using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs
{
    public static class ApiFlash
    {
        public const string ApiEndpoint = "https://api.apiflash.com/v1/urltoimage";

        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Takes a screenshot from an Url.
        /// </summary>
        /// <param name="accessKey">A valid access key allowing you to make API calls.</param>
        /// <param name="url">The complete URL of the website for which you want to capture a screenshot. The URL must include the protocol (http:// or https://) to be processed correctly.</param>
        /// <param name="format">The image format of the captured screenshot. Either jpeg or png.</param>
        /// <param name="failOnStatus">A comma separated list of HTTP status codes that should make the API call fail instead of returning a screenshot.
        /// Hyphen separated HTTP status codes can be used to define ranges. For example 400,404,500-511 would make the API call fail if the URL returns 400, 404 or any status code between 500 and 511.</param>
        /// <param name="ttl">Number of seconds the screenshot is cached. From 0 seconds to 2592000 seconds (30 days).</param>
        /// <param name="fresh">Force the API to capture a fresh new screenshot instead of returning a screenshot from the cache.</param>
        /// <param name="fullPage">Set this parameter to true to capture the entire page of the target website.</param>
        /// <param name="scrollPage">Set this parameter to true to scroll through the entire page before capturing a screenshot. This is useful to trigger animations or lazy loaded elements.</param>
        /// <returns>A task containing a ApiFlashResponse object with the Url of the image or the error message.</returns>
        public static async Task<ApiFlashResponse> UrlToImageAsync(string accessKey, string url, FormatType format = FormatType.Jpeg, string failOnStatus = "",
            uint ttl = 86400, bool fresh = false, bool fullPage = false, bool scrollPage = false)
        {
            if (string.IsNullOrEmpty(accessKey))
            {
                throw new ArgumentNullException(nameof(accessKey));
            }
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                throw new ArgumentException("The Url is not well formed.", nameof(url));
            }

            string q = $"access_key={accessKey}"
                       + $"&url={Uri.EscapeDataString(url)}"
                       + $"&response_type={ResponseType.Json.ToString().ToLowerInvariant()}";

            if (format != FormatType.Jpeg)
            {
                q += $"&format={format.ToString().ToLowerInvariant()}";
            }
            if (!string.IsNullOrEmpty(failOnStatus)) // check valid
            {
                q += $"&fail_on_status={failOnStatus}";
            }
            if (ttl != 86400)
            {
                if (ttl > 2592000)
                {
                    throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "Value must be between 0 and 2592000.");
                }
                q += $"&ttl={ttl}";
            }
            if (fresh)
            {
                q += "&fresh=true";
            }
            if (fullPage)
            {
                q += "&full_page=true";
            }
            if (scrollPage)
            {
                q += "&scroll_page=true";
            }

            string json = await _httpClient.GetStringAsync($"{ApiEndpoint}?{q}");
            return JsonConvert.DeserializeObject<ApiFlashResponse>(json);
        }

        public static async Task<ApiFlashQuotaResponse> GetQuotaAsync(string accessKey)
        {
            if (string.IsNullOrEmpty(accessKey))
            {
                throw new ArgumentNullException(nameof(accessKey));
            }

            string json = await _httpClient.GetStringAsync($"{ApiEndpoint}/quota?access_key={accessKey}");
            return JsonConvert.DeserializeObject<ApiFlashQuotaResponse>(json);
        }

        public enum FormatType
        {
            Jpeg,
            Png
        }

        public enum ResponseType
        {
            Image,
            Json
        }
    }

    public class ApiFlashResponse
    {
        /// <summary>
        /// Url of the screenshot.
        /// </summary>
        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }

        /// <summary>
        /// The error message on fail.
        /// </summary>
        [JsonProperty("error_message", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorMessage { get; set; }
    }

    public class ApiFlashQuotaResponse
    {
        /// <summary>
        /// The maximum number of API calls you can make per billing period.
        /// </summary>
        [JsonProperty("limit")]
        public int Limit { get; set; }

        /// <summary>
        /// The number of API calls remaining for the current billing period.
        /// </summary>
        [JsonProperty("remaining")]
        public int Remaining { get; set; }

        /// <summary>
        /// The time, in UTC epoch seconds, at which the current billing period ends and the remaining number of API calls resets.
        /// </summary>
        [JsonProperty("reset")]
        public uint Reset { get; set; }
    }
}