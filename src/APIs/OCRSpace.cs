using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace Fergun.APIs
{
    public static class OCRSpace
    {
        // https://api.ocr.space/parse/image
        public const string ApiEndpoint = "https://api.ocr.space/parse/imageurl";

        /// <summary>
        /// Performs OCR from an Url.
        /// </summary>
        /// <param name="apiKey">The API Key.</param>
        /// <param name="url">URL of remote image file.</param>
        /// <param name="language">Language used for OCR. If no language is specified, English is taken as default, if Engine 2 is used, the language is auto detected.</param>
        /// <param name="isOverlayRequired">If true, returns the coordinates of the bounding boxes for each word. If false, the OCR'ed text is returned only as a text block (this makes the JSON reponse smaller).</param>
        /// <param name="fileType">Overwrites the automatic file type detection based on content-type. Supported image file formats are png, jpg (jpeg), gif, tif (tiff) and bmp. For document ocr, the api supports the Adobe PDF format. Multi-page TIFF files are supported.</param>
        /// <param name="detectOrientation">If set to true, the api autorotates the image correctly and sets the TextOrientation parameter in the JSON response. If the image is not rotated, then TextOrientation=0, otherwise it is the degree of the rotation, e. g. "270".</param>
        /// <param name="isCreateSearchablePdf">If true, API generates a searchable PDF. This parameter automatically sets IsOverlayRequired = true.</param>
        /// <param name="isSearchablePdfHideTextLayer">If true, the text layer is hidden (not visible)</param>
        /// <param name="scale">If set to true, the api does some internal upscaling. This can improve the OCR result significantly, especially for low-resolution PDF scans.</param>
        /// <param name="isTable">If set to true, the OCR logic makes sure that the parsed text result is always returned line by line. This switch is recommended for table OCR, receipt OCR, invoice processing and all other type of input documents that have a table like structure.</param>
        /// <param name="ocrEngine">The default is engine 1. OCR Engine 2 is a new image-processing method.</param>
        public static async Task<OCRSpaceResponse> PerformOcrFromUrlAsync(string apiKey, string url, string language = "", bool isOverlayRequired = false, FileType? fileType = null, bool detectOrientation = false,
            bool isCreateSearchablePdf = false, bool isSearchablePdfHideTextLayer = false, bool scale = false, bool isTable = false, OCREngine ocrEngine = OCREngine.Engine1)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }
            //if (!Uri.IsWellFormedUriString(Url, UriKind.Absolute))
            //{
            //    throw new ArgumentException("The Url is not well formed.", nameof(Url));
            //}

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["apikey"] = apiKey;
            query["url"] = url;

            //if (string.IsNullOrEmpty(Language) && OCREngine == OCREngine.Engine1)
            //{
            //    throw new ArgumentException("Automatic language detection can only be used on Engine 2.", nameof(Language));
            //}
            if (!string.IsNullOrEmpty(language))
            {
                query["language"] = language;
            }
            query["isOverlayRequired"] = isOverlayRequired.ToString();
            if (fileType != null)
            {
                query["filetype"] = fileType.ToString();
            }
            query["detectOrientation"] = detectOrientation.ToString();
            query["isCreateSearchablePdf"] = isCreateSearchablePdf.ToString();
            query["isSearchablePdfHideTextLayer"] = isSearchablePdfHideTextLayer.ToString();
            query["scale"] = scale.ToString();
            query["isTable"] = isTable.ToString();
            query["OCREngine"] = ocrEngine.ToString("D");

            string json;

            using (var wc = new WebClient())
            {
                json = await wc.DownloadStringTaskAsync($"{ApiEndpoint}?{query}");
            }
            return JsonConvert.DeserializeObject<OCRSpaceResponse>(json);
        }

        public class OCRSpaceResponse
        {
            /// <summary>
            /// The OCR results for the image or for each page of PDF. For PDF: Each page has its own OCR result and error message (if any)
            /// </summary>
            [JsonProperty("ParsedResults", NullValueHandling = NullValueHandling.Ignore)]
            public List<ParsedResult> ParsedResults { get; set; }

            /// <summary>
            /// <para>The exit code shows if OCR completed successfully, partially or failed with error</para>
            /// 1: Parsed Successfully(Image / All pages parsed successfully)<br/>
            /// 2: Parsed Partially(Only few pages out of all the pages parsed successfully)<br/>
            /// 3: Image / All the PDF pages failed parsing(This happens mainly because the OCR engine fails to parse an image)<br/>
            /// 4: Error occurred when attempting to parse(This happens when a fatal error occurs during parsing)
            /// </summary>
            [JsonProperty("OCRExitCode")]
            public int OcrExitCode { get; set; }

            /// <summary>
            /// If an error occurs when parsing the Image / PDF pages
            /// </summary>
            [JsonProperty("IsErroredOnProcessing")]
            public bool IsErroredOnProcessing { get; set; }

            /// <summary>
            /// The error message of the error occurred when parsing the image
            /// </summary>
            [JsonProperty("ErrorMessage", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> ErrorMessage { get; set; }

            /// <summary>
            /// Detailed error message
            /// </summary>
            [JsonProperty("ErrorDetails", NullValueHandling = NullValueHandling.Ignore)]
            public string ErrorDetails { get; set; }

            [JsonProperty("ProcessingTimeInMilliseconds")]
            public string ProcessingTimeInMilliseconds { get; set; }

            [JsonProperty("SearchablePDFURL", NullValueHandling = NullValueHandling.Ignore)]
            public string SearchablePdfurl { get; set; }
        }

        /// <summary>
        /// OCR Result
        /// </summary>
        public class ParsedResult
        {
            /// <summary>
            /// Only if 'IsOverlayRequired' is set to 'True'
            /// </summary>
            [JsonProperty("TextOverlay", NullValueHandling = NullValueHandling.Ignore)]
            public TextOverlay TextOverlay { get; set; }

            [JsonProperty("TextOrientation")]
            public string TextOrientation { get; set; }

            /// <summary>
            /// <para>The exit code returned by the parsing engine</para>
            /// 0: File not found</br>
            /// 1: Success</br>
            /// -10: OCR Engine Parse Error</br>
            /// -20: Timeout</br>
            /// -30: Validation Error</br>
            /// -99: Unknown Error
            /// </summary>
            [JsonProperty("FileParseExitCode")]
            public int FileParseExitCode { get; set; }

            /// <summary>
            /// The parsed text for an image
            /// </summary>
            [JsonProperty("ParsedText")]
            public string ParsedText { get; set; }

            /// <summary>
            /// Error message returned by the parsing engine
            /// </summary>
            [JsonProperty("ErrorMessage", NullValueHandling = NullValueHandling.Ignore)]
            public string ErrorMessage { get; set; }

            /// <summary>
            /// Detailed error message returned from the parsing engine for debugging purposes
            /// </summary>
            [JsonProperty("ErrorDetails", NullValueHandling = NullValueHandling.Ignore)]
            public string ErrorDetails { get; set; }
        }

        /// <summary>
        /// Overlay data for the text in the image/pdf
        /// </summary>
        public class TextOverlay
        {
            /// <summary>
            /// This contains an array of all the lines. Each line will contain an array of words
            /// </summary>
            [JsonProperty("Lines")]
            public List<Line> Lines { get; set; }

            /// <summary>
            /// True/False depending upon if the overlay for the parsed result is present or not
            /// </summary>
            [JsonProperty("HasOverlay")]
            public bool HasOverlay { get; set; }

            [JsonProperty("Message")]
            public string Message { get; set; }
        }

        /// <summary>
        /// Lines in the overlay text
        /// </summary>
        public class Line
        {
            /// <summary>
            /// This contains the words with the specific details of a word like its text and position
            /// </summary>
            public List<Word> Words { get; set; }

            /// <summary>
            /// Contains the height (in px) of the line
            /// </summary>
            public int MaxHeight { get; set; }

            /// <summary>
            /// Contains the distance (in px) of the line from the top edge in the original size of image
            /// </summary>
            public int MinTop { get; set; }
        }

        /// <summary>
        /// Words in a line
        /// </summary>
        public class Word
        {
            /// <summary>
            /// This contains the text of that specific word
            /// </summary>
            public string WordText { get; set; }

            /// <summary>
            /// Contains the distance (in px) of the word from the left edge of the image
            /// </summary>
            public int Left { get; set; }

            /// <summary>
            /// Contains the distance (in px) of the word from the top edge of the image
            /// </summary>
            public int Top { get; set; }

            /// <summary>
            /// Contains the height (in px) of the word
            /// </summary>
            public int Height { get; set; }

            /// <summary>
            /// Contains the width (in px) of the word
            /// </summary>
            public int Width { get; set; }
        }

        /// <summary>
        /// The supported image file formats.
        /// </summary>
        public enum FileType
        {
            PDF,
            GIF,
            PNG,
            JPG,
            JPEG,
            TIF,
            BMP
        }

        public enum OCREngine
        {
            Engine1 = 1,
            Engine2
        }

        public static class Language
        {
            public const string Arabic = "ara";
            public const string Bulgarian = "bul";
            public const string ChineseSimplified = "chs";
            public const string ChineseTraditional = "cht";
            public const string Croatian = "hrv";
            public const string Czech = "cze";
            public const string Danish = "dan";
            public const string Dutch = "dut";
            public const string English = "eng";
            public const string Finnish = "fin";
            public const string French = "fre";
            public const string German = "ger";
            public const string Greek = "gre";
            public const string Hungarian = "hun";
            public const string Korean = "kor";
            public const string Italian = "ita";
            public const string Japanese = "jpn";
            public const string Polish = "pol";
            public const string Portuguese = "por";
            public const string Russian = "rus";
            public const string Slovenian = "slv";
            public const string Spanish = "spa";
            public const string Swedish = "swe";
            public const string Turkish = "tur";
        }
    }
}