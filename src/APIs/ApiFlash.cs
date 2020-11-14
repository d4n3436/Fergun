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
        /// <param name="accessKey">A valid access key allowing you to make API calls.</param>
        /// <param name="url">The complete URL of the website for which you want to capture a screenshot. The URL must include the protocol (http:// or https://) to be processed correctly.</param>
        /// <param name="format">The image format of the captured screenshot. Either jpeg or png.</param>
        /// <param name="failOnStatus">A comma separated list of HTTP status codes that should make the API call fail instead of returning a screenshot. Hyphen separated HTTP status codes can be used to define ranges. For example 400,404,500-511 would make the API call fail if the URL returns 400, 404 or any status code between 500 and 511.</param>
        /// <param name="ttl">Number of seconds the screenshot is cached. API calls with the same parameters do return a cached screenshot and don't count in your monthly quota. From 0 seconds to 2592000 seconds (30 days).</param>
        /// <param name="fresh">Force the API to capture a fresh new screenshot instead of returning a screenshot from the cache.</param>
        /// <param name="fullPage">Set this parameter to true to capture the entire page of the target website.</param>
        /// <param name="scrollPage">Set this parameter to true to scroll through the entire page before capturing a screenshot. This is useful to trigger animations or lazy loaded elements.</param>
        /// <param name="width">The width, in pixels, of the viewport to use.</param>
        /// <param name="height">The height, in pixels, of the viewport to use. This is ignored if FullPage is true.</param>
        /// <param name="delay">The delay, in seconds, to wait after the page is loaded (load event fired and no more network connections for at least 500ms) before capturing the screenshot. From 0 seconds to a maximum of 10 seconds.</param>
        /// <param name="waitFor">Wait until the provided CSS selector matches an element present in the page before capturing a screenshot. The process times out after 40 seconds.</param>
        /// <param name="quality">The quality of the image between 0 and 100. This only works with the jpeg format.</param>
        /// <param name="transparent">Hides the default background and allows capturing screenshots with transparency. This only works with the png format.</param>
        /// <param name="thumbnailWidth">The width, in pixels, of the thumbnail to generate. The aspect ratio will be preserved. This is ignored if FullPage is true.</param>
        /// <param name="scaleFactor">The device scale factor to use when capturing the screenshot. A scale factor of 2 will produce a high definition screenshot suited to be displayed on retina devices. The bigger the scale factor is, the heavier the produced screenshot will be.</param>
        /// <param name="css">A CSS string to inject in the web page when capturing the screenshot. This CSS string needs to be URL encoded to be processed correctly.</param>
        /// <param name="js">Additional JavaScript code to be injected into the page before capturing. The JS string needs to be URL encoded to be processed correctly.</param>
        /// <param name="extractHtml">Extract the HTML of the page at the same time the screenshot is made. When this parameter is set to true, an ExtractedHtml attribute is added to the returned json document.</param>
        /// <param name="extractText">Extract the text of the page at the same time the screenshot is made. When this parameter is set to true, an ExtractedText attribute is added to the returned json document.</param>
        /// <param name="acceptLanguage">Sets the Accept-Language header on requests to the target URL allowing you to make screenshots of a website with a specific language.</param>
        /// <param name="userAgent">Sets the User-Agent header to emulate a particular device when making screenshots. It should be URL encoded to be processed correctly.</param>
        /// <param name="headers">A semicolon separated list of headers to be used when capturing the screenshot. Each header should be supplied as a key value pair and multiple pairs should be separated by a semicolon. The Headers parameter value should be URL encoded to be processed correctly. For example, Header1=value1;Header2=value2 would have to be URL encoded into Header1%3Dvalue1%3BHeader2%3Dvalue2.</param>
        /// <param name="cookies">A semicolon separated list of cookies to be used when capturing the screenshot. Each cookie should be supplied as a name value pair and multiple pairs should be separated by a semicolon. The Cookies parameter value should be URL encoded to be processed correctly. For example, cookie1=value1;cookie2=value2 would have to be URL encoded into cookie1%3Dvalue1%3Bcookie2%3Dvalue2.</param>
        /// <param name="latitude">The latitude to use when emulating geo-location between -90 and 90.</param>
        /// <param name="longitude">The longitude to use when emulating geo-location between -180 and 180.</param>
        /// <param name="accuracy">Accuracy value to use when emulating geo-location.</param>
        /// <param name="proxy">The address of a proxy server through which the screenshot should be captured. The proxy address should be formatted as address:port or user:password@address:port if authentication is needed.</param>
        /// <returns>An ApiFlashResponse object containing the Url of the image or the error message.</returns>
        public static async Task<ApiFlashResponse> UrlToImageAsync(string accessKey, string url, FormatType format = FormatType.jpeg, string failOnStatus = "",
            uint ttl = 86400, bool fresh = false, bool fullPage = false, bool scrollPage = false,
            uint width = 1920, uint height = 1080, uint delay = 0, string waitFor = "", uint quality = 80,
            bool transparent = false, uint thumbnailWidth = 0, uint scaleFactor = 1, string css = "", string js = "", bool extractHtml = false,
            bool extractText = false, string acceptLanguage = "", string userAgent = "", string headers = "", string cookies = "", int latitude = 0,
            int longitude = 0, uint accuracy = 0, string proxy = "")
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

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["access_key"] = accessKey;
            query["url"] = url;
            query["response_type"] = ResponseType.json.ToString();

            if (format != FormatType.jpeg)
            {
                query["format"] = format.ToString();
            }
            if (!string.IsNullOrEmpty(failOnStatus)) // check valid
            {
                query["fail_on_status"] = failOnStatus;
            }
            if (ttl != 86400)
            {
                if (ttl > 2592000)
                {
                    throw new ArgumentOutOfRangeException(nameof(ttl), "Value must be between 0 and 2592000.");
                }
                query["ttl"] = ttl.ToString();
            }
            if (fresh)
            {
                query["fresh"] = "true";
            }
            if (fullPage)
            {
                query["full_page"] = "true";
            }
            if (scrollPage)
            {
                query["scroll_page"] = "true";
            }
            if (width != 1920)
            {
                query["width"] = width.ToString();
            }
            if (height != 1080 && !fullPage)
            {
                query["height"] = height.ToString();
            }
            if (delay != 0)
            {
                if (delay > 10)
                {
                    throw new ArgumentOutOfRangeException(nameof(delay), "Value must be between 0 and 10.");
                }
                query["delay"] = delay.ToString();
            }
            if (!string.IsNullOrEmpty(waitFor))
            {
                query["wait_for"] = waitFor;
            }
            if (quality != 80)
            {
                if (quality > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(quality), "Value must be between 0 and 100.");
                }
                query["quality"] = quality.ToString();
            }
            if (transparent && format == FormatType.png)
            {
                query["transparent"] = "true";
            }
            if (thumbnailWidth != 0 && !fullPage)
            {
                query["thumbnail_width"] = thumbnailWidth.ToString();
            }
            if (scaleFactor != 1)
            {
                query["scale_factor"] = scaleFactor.ToString();
            }
            if (!string.IsNullOrEmpty(css))
            {
                query["css"] = css;
            }
            if (!string.IsNullOrEmpty(js))
            {
                query["js"] = js;
            }
            if (extractHtml)
            {
                query["extract_html"] = "true";
            }
            if (extractText)
            {
                query["extract_text"] = "true";
            }
            if (!string.IsNullOrEmpty(acceptLanguage))
            {
                query["accept_language"] = acceptLanguage;
            }
            if (!string.IsNullOrEmpty(userAgent))
            {
                query["user_agent"] = userAgent;
            }
            if (!string.IsNullOrEmpty(headers))
            {
                query["headers"] = headers;
            }
            if (!string.IsNullOrEmpty(cookies))
            {
                query["cookies"] = cookies;
            }
            if (latitude != 0)
            {
                if (latitude < -90 || latitude > 90)
                {
                    throw new ArgumentOutOfRangeException(nameof(latitude), "Value must be between -90 and 90.");
                }
                query["latitude"] = latitude.ToString();
            }
            if (longitude != 0)
            {
                if (longitude < -180 || longitude > 180)
                {
                    throw new ArgumentOutOfRangeException(nameof(longitude), "Value must be between -180 and 180.");
                }
                query["longitude"] = longitude.ToString();
            }
            if (accuracy != 0)
            {
                query["accuracy"] = accuracy.ToString();
            }
            if (!string.IsNullOrEmpty(proxy)) // check valid
            {
                query["proxy"] = proxy;
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