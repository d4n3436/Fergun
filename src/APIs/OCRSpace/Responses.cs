using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.OCRSpace
{
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
}