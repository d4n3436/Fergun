using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.OCRSpace
{
    public static class OCRSpaceApi
    {
        // https://api.ocr.space/parse/image
        public const string ApiEndpoint = "https://api.ocr.space/parse/imageurl";

        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Performs OCR from a Url.
        /// </summary>
        /// <param name="apiKey">The API Key.</param>
        /// <param name="url">URL of remote image file.</param>
        /// <param name="language">Language used for OCR. If no language is specified, English is taken as default, if Engine 2 is used, the language is auto detected.</param>
        /// <param name="isOverlayRequired">If true, returns the coordinates of the bounding boxes for each word. If false, the OCR'ed text is returned only as a text block (this makes the JSON response smaller).</param>
        /// <param name="fileType">Overwrites the automatic file type detection based on content-type. Supported image file formats are png, jpg (jpeg), gif, tif (tiff) and bmp. For document ocr, the api supports the Adobe PDF format. Multi-page TIFF files are supported.</param>
        /// <param name="detectOrientation">If set to true, the api autorotates the image correctly and sets the TextOrientation parameter in the JSON response. If the image is not rotated, then TextOrientation=0, otherwise it is the degree of the rotation, e. g. "270".</param>
        /// <param name="isCreateSearchablePdf">If true, API generates a searchable PDF. This parameter automatically sets IsOverlayRequired = true.</param>
        /// <param name="isSearchablePdfHideTextLayer">If true, the text layer is hidden (not visible)</param>
        /// <param name="scale">If set to true, the api does some internal upscaling. This can improve the OCR result significantly, especially for low-resolution PDF scans.</param>
        /// <param name="isTable">If set to true, the OCR logic makes sure that the parsed text result is always returned line by line. This switch is recommended for table OCR, receipt OCR, invoice processing and all other type of input documents that have a table like structure.</param>
        /// <param name="ocrEngine">The default is engine 1. OCR Engine 2 is a new image-processing method.</param>
        public static async Task<OCRSpaceResponse> PerformOcrFromUrlAsync(string apiKey, string url, string language = "", bool isOverlayRequired = false, FileType? fileType = null, bool detectOrientation = false,
            bool isCreateSearchablePdf = false, bool isSearchablePdfHideTextLayer = false, bool scale = false, bool isTable = false, OcrEngine ocrEngine = OcrEngine.Engine1)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            string q = $"apikey={apiKey}"
                       + $"&url={Uri.EscapeDataString(url)}"
                       + $"&isOverlayRequired={isOverlayRequired}"
                       + $"&detectOrientation={detectOrientation}"
                       + $"&isCreateSearchablePdf={isCreateSearchablePdf}"
                       + $"&isSearchablePdfHideTextLayer={isSearchablePdfHideTextLayer}"
                       + $"&scale={scale}"
                       + $"&isTable={isTable}"
                       + $"&OCREngine={ocrEngine:D}";

            if (!string.IsNullOrEmpty(language))
            {
                q += $"&language={language}";
            }
            if (fileType != null)
            {
                q += $"&filetype={fileType.ToString()?.ToUpperInvariant()}";
            }

            string json = await _httpClient.GetStringAsync($"{ApiEndpoint}?{q}");
            return JsonConvert.DeserializeObject<OCRSpaceResponse>(json);
        }
    }

    /// <summary>
    /// The supported image file formats.
    /// </summary>
    public enum FileType
    {
        Pdf,
        Gif,
        Png,
        Jpg,
        Jpeg,
        Tif,
        Bmp
    }

    /// <summary>
    /// The OCR engines.
    /// </summary>
    public enum OcrEngine
    {
        Engine1 = 1,
        Engine2
    }
}