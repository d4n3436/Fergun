using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace Fergun.APIs
{
    public static class ApiFlash
    {
        private const string ApiEndpoint = "https://api.apiflash.com/v1/urltoimage";

        /// <summary>
        /// Takes an screenshot from an Url.
        /// </summary>
        /// <param name="AccessKey">A valid access key allowing you to make API calls.</param>
        /// <param name="Url">The complete URL of the website for which you want to capture a screenshot. The URL must include the protocol (http:// or https://) to be processed correctly.</param>
        /// <param name="Format">The image format of the captured screenshot. Either jpeg or png.</param>
        /// <param name="FailOnStatus">A comma separated list of HTTP status codes that should make the API call fail instead of returning a screenshot. Hyphen separated HTTP status codes can be used to define ranges. For example 400,404,500-511 would make the API call fail if the URL returns 400, 404 or any status code between 500 and 511.</param>
        /// <param name="ttl">Number of seconds the screenshot is cached. API calls with the same parameters do return a cached screenshot and don't count in your monthly quota. From 0 seconds to 2592000 seconds (30 days).</param>
        /// <param name="Fresh">Force the API to capture a fresh new screenshot instead of returning a screenshot from the cache.</param>
        /// <param name="FullPage">Set this parameter to true to capture the entire page of the target website.</param>
        /// <param name="ScrollPage">Set this parameter to true to scroll through the entire page before capturing a screenshot. This is useful to trigger animations or lazy loaded elements.</param>
        /// <param name="Width">The width, in pixels, of the viewport to use.</param>
        /// <param name="Height">The height, in pixels, of the viewport to use. This is ignored if FullPage is true.</param>
        /// <param name="Delay">The delay, in seconds, to wait after the page is loaded (load event fired and no more network connections for at least 500ms) before capturing the screenshot. From 0 seconds to a maximum of 10 seconds.</param>
        /// <param name="WaitFor">Wait until the provided CSS selector matches an element present in the page before capturing a screenshot. The process times out after 40 seconds.</param>
        /// <param name="Quality">The quality of the image between 0 and 100. This only works with the jpeg format.</param>
        /// <param name="Transparent">Hides the default background and allows capturing screenshots with transparency. This only works with the png format.</param>
        /// <param name="ThumbnailWidth">The width, in pixels, of the thumbnail to generate. The aspect ratio will be preserved. This is ignored if FullPage is true.</param>
        /// <param name="ScaleFactor">The device scale factor to use when capturing the screenshot. A scale factor of 2 will produce a high definition screenshot suited to be displayed on retina devices. The bigger the scale factor is, the heavier the produced screenshot will be.</param>
        /// <param name="CSS">A CSS string to inject in the web page when capturing the screenshot. This CSS string needs to be URL encoded to be processed correctly.</param>
        /// <param name="JS">Additional JavaScript code to be injected into the page before capturing. The JS string needs to be URL encoded to be processed correctly.</param>
        /// <param name="ExtractHtml">Extract the HTML of the page at the same time the screenshot is made. When this parameter is set to true, an ExtractedHtml attribute is added to the returned json document.</param>
        /// <param name="ExtractText">Extract the text of the page at the same time the screenshot is made. When this parameter is set to true, an ExtractedText attribute is added to the returned json document.</param>
        /// <param name="AcceptLanguage">Sets the Accept-Language header on requests to the target URL allowing you to make screenshots of a website with a specific language.</param>
        /// <param name="UserAgent">Sets the User-Agent header to emulate a particular device when making screenshots. It should be URL encoded to be processed correctly.</param>
        /// <param name="Headers">A semicolon separated list of headers to be used when capturing the screenshot. Each header should be supplied as a key value pair and multiple pairs should be separated by a semicolon. The Headers parameter value should be URL encoded to be processed correctly. For example, Header1=value1;Header2=value2 would have to be URL encoded into Header1%3Dvalue1%3BHeader2%3Dvalue2.</param>
        /// <param name="Cookies">A semicolon separated list of cookies to be used when capturing the screenshot. Each cookie should be supplied as a name value pair and multiple pairs should be separated by a semicolon. The Cookies parameter value should be URL encoded to be processed correctly. For example, cookie1=value1;cookie2=value2 would have to be URL encoded into cookie1%3Dvalue1%3Bcookie2%3Dvalue2.</param>
        /// <param name="Latitude">The latitude to use when emulating geo-location between -90 and 90.</param>
        /// <param name="Longitude">The longitude to use when emulating geo-location between -180 and 180.</param>
        /// <param name="Accuracy">Accuracy value to use when emulating geo-location.</param>
        /// <param name="Proxy">The address of a proxy server through which the screenshot should be captured. The proxy address should be formatted as address:port or user:password@address:port if authentication is needed.</param>
        /// <returns>An ApiFlashResponse object containing the Url of the image or the error message.</returns>
        public static async Task<ApiFlashResponse> UrlToImageAsync(string AccessKey, string Url, FormatType Format = FormatType.jpeg, string FailOnStatus = "", uint ttl = 86400, bool Fresh = false, bool FullPage = false, bool ScrollPage = false,
            uint Width = 1920, uint Height = 1080, uint Delay = 0, string WaitFor = "", uint Quality = 80,
            bool Transparent = false, uint ThumbnailWidth = 0, uint ScaleFactor = 1, string CSS = "", string JS = "", bool ExtractHtml = false,
            bool ExtractText = false, string AcceptLanguage = "", string UserAgent = "", string Headers = "", string Cookies = "", int Latitude = 0,
            int Longitude = 0, uint Accuracy = 0, string Proxy = "")
        {

            if (string.IsNullOrEmpty(AccessKey))
            {
                throw new ArgumentNullException(nameof(AccessKey));
            }
            if (string.IsNullOrEmpty(Url))
            {
                throw new ArgumentNullException(nameof(Url));
            }
            if (!Uri.IsWellFormedUriString(Url, UriKind.Absolute))
            {
                throw new ArgumentException("The Url is not well formed.", nameof(Url));
            }

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["access_key"] = AccessKey;
            query["url"] = Url;
            query["response_type"] = ResponseType.json.ToString();

            if (Format != FormatType.jpeg)
            {
                query["format_type"] = Format.ToString();
            }
            if (!string.IsNullOrEmpty(FailOnStatus)) // check valid
            {
                query["fail_on_status"] = FailOnStatus;
            }
            if (ttl != 86400)
            {
                if (ttl > 2592000)
                {
                    throw new ArgumentOutOfRangeException(nameof(ttl), "Value must be between 0 and 2592000.");
                }
                query["ttl"] = ttl.ToString();
            }
            if (Fresh != false)
            {
                query["fresh"] = "true";
            }
            if (FullPage != false)
            {
                query["full_page"] = "true";
            }
            if (ScrollPage != false)
            {
                query["scroll_page"] = "true";
            }
            if (Width != 1920)
            {
                query["width"] = Width.ToString();
            }
            if (Height != 1080 && FullPage != true)
            {
                query["height"] = Height.ToString();
            }
            if (Delay != 0)
            {
                if (Delay > 10)
                {
                    throw new ArgumentOutOfRangeException(nameof(Delay), "Value must be between 0 and 10.");
                }
                query["delay"] = Delay.ToString();
            }
            if (!string.IsNullOrEmpty(WaitFor))
            {
                query["wait_for"] = WaitFor;
            }
            if (Quality != 80)
            {
                if (Quality > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(Quality), "Value must be between 0 and 100.");
                }
                query["quality"] = Quality.ToString();
            }
            if (Transparent != false && Format == FormatType.png)
            {
                query["transparent"] = "true";
            }
            if (ThumbnailWidth != 0 && FullPage != true)
            {
                query["thumbnail_width"] = ThumbnailWidth.ToString();
            }
            if (ScaleFactor != 1)
            {
                query["scale_factor"] = ScaleFactor.ToString();
            }
            if (!string.IsNullOrEmpty(CSS))
            {
                query["css"] = CSS;
            }
            if (!string.IsNullOrEmpty(JS))
            {
                query["js"] = JS;
            }
            if (ExtractHtml != false)
            {
                query["extract_html"] = "true";
            }
            if (ExtractText != false)
            {
                query["extract_text"] = "true";
            }
            if (!string.IsNullOrEmpty(AcceptLanguage))
            {
                query["accept_language"] = AcceptLanguage;
            }
            if (!string.IsNullOrEmpty(UserAgent))
            {
                query["user_agent"] = UserAgent;
            }
            if (!string.IsNullOrEmpty(Headers))
            {
                query["headers"] = Headers;
            }
            if (!string.IsNullOrEmpty(Cookies))
            {
                query["cookies"] = Cookies;
            }
            if (Latitude != 0)
            {
                if (Latitude < -90 || Latitude > 90)
                {
                    throw new ArgumentOutOfRangeException(nameof(Latitude), "Value must be between -90 and 90.");
                }
                query["latitude"] = Latitude.ToString();
            }
            if (Longitude != 0)
            {
                if (Longitude < -180 || Longitude > 180)
                {
                    throw new ArgumentOutOfRangeException(nameof(Longitude), "Value must be between -180 and 180.");
                }
                query["longitude"] = Longitude.ToString();
            }
            if (Accuracy != 0)
            {
                query["accuracy"] = Accuracy.ToString();
            }
            if (!string.IsNullOrEmpty(Proxy)) // check valid
            {
                query["proxy"] = Proxy;
            }

            string json;

            using (var wc = new WebClient())
            {
                json = await wc.DownloadStringTaskAsync($"{ApiEndpoint}?{query}");
            }
            return JsonConvert.DeserializeObject<ApiFlashResponse>(json);
        }

        public static async Task<ApiFlashQuotaResponse> GetQuotaAsync(string accessKey)
        {
            if (string.IsNullOrEmpty(accessKey))
            {
                throw new ArgumentNullException(nameof(accessKey));
            }
            string json;
            using (WebClient wc = new WebClient())
            {
                json = await wc.DownloadStringTaskAsync($"{ApiEndpoint}/quota?access_key={accessKey}");
            }
            return JsonConvert.DeserializeObject<ApiFlashQuotaResponse>(json);
        }

        public enum FormatType
        {
            jpeg,
            png
        }

        public enum ResponseType
        {
            image,
            json
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
        /// Url of the extracted Html.
        /// </summary>
        [JsonProperty("extracted_html", NullValueHandling = NullValueHandling.Ignore)]
        public string ExtractedHtml { get; set; }

        /// <summary>
        /// Url of the extracted text.
        /// </summary>
        [JsonProperty("extracted_text", NullValueHandling = NullValueHandling.Ignore)]
        public string ExtractedText { get; set; }

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