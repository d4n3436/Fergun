using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fergun.APIs
{
    // Based on:
    // https://github.com/pndurette/gTTS
    // https://github.com/Boudewijn26/gTTS-token

    /// <summary>
    /// A C# library to request Google TTS audio and URLs.
    /// </summary>
    public static class GoogleTTS
    {
        public const string ApiEndpoint = "https://translate.google.com/translate_tts";

        private const string _salt1 = "+-a^+6";
        private const string _salt2 = "+-3^+b+-f";
        private const float _slowSpeed = 0.3f;
        private const float _normalSpeed = 1;
        private const int _maxLength = 100;

        private static Regex _tokenizer;
        private static readonly Regex _allPunctuation =
            new Regex(@$"^[{Regex.Escape(Symbols.PunctuationMarks).Replace("]", @"\]", StringComparison.InvariantCultureIgnoreCase)}]*$", RegexOptions.Compiled);

        private static readonly HttpClient _client = new HttpClient();

        static GoogleTTS()
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.24 Safari/537.36");
        }

        /// <summary>
        /// Sends a request to Google Translate's Text-to-Speech API.
        /// </summary>
        /// <param name="text">The text to be read.</param>
        /// <param name="language">The language (IETF language tag) to read the text in.</param>
        /// <param name="slow">Reads text more slowly.</param>
        /// <returns>A task representing the TTS API request(s).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="language"/> are null or empty.</exception>
        public static async Task<byte[]> GetTtsAsync(string text, string language = "en", bool slow = false)
        {
            var uris = GetUris(text, language, slow);
            var list = new List<byte[]>();
            foreach (var uri in uris)
            {
                var response = await _client.GetByteArrayAsync(uri).ConfigureAwait(false);
                list.Add(response);
            }

            //return list.SelectMany(x => x).ToArray();
            // It's faster
            var result = new byte[list.Sum(arr => arr.Length)];
            int index = 0;
            foreach (var bytes in list)
            {
                bytes.CopyTo(result, index);
                index += bytes.Length;
            }

            return result;
        }

        /// <summary>
        /// Get the TTS API request URLs that would be sent to the TTS API.
        /// </summary>
        /// <param name="text">The text to be read.</param>
        /// <param name="language">The language (IETF language tag) to read the text in.</param>
        /// <param name="slow">Reads text more slowly.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> containing the TTS API request URLs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="language"/> are null or empty.</exception>
        public static IEnumerable<string> GetUrls(string text, string language = "en", bool slow = false)
        {
            return GetUris(text, language, slow).Select(x => x.AbsoluteUri);
        }

        /// <summary>
        /// Get the TTS API request URIs that would be sent to the TTS API.
        /// </summary>
        /// <param name="text">The text to be read.</param>
        /// <param name="language">The language (IETF language tag) to read the text in.</param>
        /// <param name="slow">Reads text more slowly.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> containing the TTS API request URIs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="language"/> are null or empty.</exception>
        public static IEnumerable<Uri> GetUris(string text, string language = "en", bool slow = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (string.IsNullOrEmpty(language))
            {
                throw new ArgumentNullException(nameof(language));
            }

            var list = new List<Uri>();
            var textParts = Tokenize(text).ToArray();

            for (int i = 0; i < textParts.Length; i++)
            {
                string query = "?"
                    + "ie=UTF-8"
                    + $"&q={Uri.EscapeDataString(textParts[i])}"
                    + $"&tl={language}"
                    + $"&ttsspeed={(slow ? _slowSpeed : _normalSpeed)}"
                    + $"&total={textParts.Length}"
                    + $"&idx={i}"
                    + "&client=tw-ob"
                    + $"&textlen={textParts[i].Length}"
                    + $"&tk={MakeToken(textParts[i])}";

                list.Add(new Uri($"{ApiEndpoint}{query}"));
            }

            return list;
        }

        /// <summary>
        /// Preprocesses, tokenizes, cleans and minimizes the text.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static IEnumerable<string> Tokenize(string text)
        {
            text = text.Trim();

            // Apply preprocessors

            // Tone marks
            text = RegexPreprocess(text, Regex.Escape(Symbols.ToneMarks), x => $"(?<={x})", " ");

            // End of line
            text = RegexPreprocess(text, "-", x => $"{x}\n", string.Empty);

            // Abbreviations
            text = RegexPreprocess(text, Symbols.Abbreviations, x => @$"(?<={x})(?=\.).", string.Empty, RegexOptions.IgnoreCase);

            if (text.Length <= _maxLength)
            {
                return CleanTokens(new[] { text });
            }

            if (_tokenizer == null)
            {
                // Prepare tokenizer

                var otherChars = new[] { Symbols.PunctuationMarks, Symbols.ToneMarks, Symbols.PeriodAndComma, ":" }.SelectMany(x => x).Distinct();

                var patterns = new List<string>
                {
                    // Tone marks
                    BuildPattern(Symbols.ToneMarks, x => $"(?<={x})."),

                    // Period and comma
                    BuildPattern(Symbols.PeriodAndComma, x => @$"(?<!\.[a-z]){x} "),

                    // Colon
                    BuildPattern(":", x => @$"(?<!\d){x}"),

                    // Other punctuation characters
                    BuildPattern(otherChars, x => x)
                };

                _tokenizer = new Regex(string.Join('|', patterns), RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            // Apply tokenizer
            var tokens = _tokenizer.Split(text);

            // Return minimized tokens
            return CleanTokens(tokens).Select(x => Minimize(x, " ", 100)).SelectMany(x => x);
        }

        /// <summary>
        /// Cleans tokens.
        /// </summary>
        /// <param name="tokens">An <see cref="IEnumerable{T}"/> of strings (tokens) to clean.</param>
        /// <returns>Stripped strings without the original elements that only consisted of whitespace and/or punctuation characters.</returns>
        private static IEnumerable<string> CleanTokens(IEnumerable<string> tokens)
        {
            return tokens.Where(x => !string.IsNullOrWhiteSpace(x) && !_allPunctuation.IsMatch(x.Trim()));
        }

        /// <summary>
        /// Runs a series of regex substitutions for each regex created.
        /// </summary>
        /// <param name="searchArgs">String element(s) to be each passed to <paramref name="searchFunc"/> to create a regex pattern.</param>
        /// <param name="searchFunc">
        /// A lamdba that takes a string and returns a string.
        /// It should take an element of <paramref name="searchArgs"/> and return a valid regex search pattern string.
        /// </param>
        /// <param name="options">The regex options to use in the regex.</param>
        private static string RegexPreprocess<T>(string text, IEnumerable<T> searchArgs, Func<string, string> searchFunc, string replacement, RegexOptions options = RegexOptions.None)
        {
            foreach (var arg in searchArgs)
            {
                var regex = new Regex(BuildPattern(new[] { arg }, searchFunc), options);
                text = regex.Replace(text, replacement);
            }

            return text;
        }

        /// <summary>
        /// Builds a regex pattern using arguments passed into a pattern template.
        /// </summary>
        /// <param name="patterns">String element(s) to be each passed to <paramref name="patternFunc"/> to create a regex pattern.</param>
        /// <param name="patternFunc">
        /// A lamdba that takes a string and returns a string.
        /// It should take an element of <paramref name="patterns"/> and return a valid regex pattern group string.
        /// </param>
        private static string BuildPattern<T>(IEnumerable<T> patterns, Func<string, string> patternFunc)
        {
            var alts = patterns.Select(arg => patternFunc(Regex.Escape(arg.ToString())));
            return string.Join('|', alts);
        }

        /// <summary>
        /// Recursively split a string in the largest chunks possible from the highest position of a delimiter all the way to a maximum size.
        /// </summary>
        /// <param name="text">The string to split.</param>
        /// <param name="delimiter">The delimiter to split on.</param>
        /// <param name="maxSize">The maximum size of a chunk.</param>
        /// <returns>The minimized string in tokens.</returns>
        private static IEnumerable<string> Minimize(string text, string delimiter, int maxSize)
        {
            if (text.StartsWith(delimiter, StringComparison.InvariantCultureIgnoreCase))
            {
                text = text.Substring(delimiter.Length);
            }

            if (text.Length > maxSize)
            {
                int index = -1;
                try
                {
                    index = text.LastIndexOf(delimiter, maxSize, StringComparison.InvariantCultureIgnoreCase);
                }
                catch (ArgumentOutOfRangeException) { }
                if (index == -1)
                {
                    index = maxSize;
                }

                return new[] { text.Substring(0, index) }.Concat(Minimize(text.Substring(index), delimiter, maxSize));
            }
            else
            {
                return new[] { text };
            }
        }

        /// <summary>
        /// Calculates the request token (tk) of a string.
        /// </summary>
        /// <param name="text">The text to calculate a token for.</param>
        /// <returns></returns>
        private static string MakeToken(string text)
        {
            long a, b;
            // Get the hours since epoch
            // Other methods:
            // a = b = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalHours;
            // a = b = (long)TimeSpan.FromSeconds(DateTimeOffset.Now.ToUnixTimeSeconds()).TotalHours;
            a = b = DateTimeOffset.Now.ToUnixTimeSeconds() / 3600;
            foreach (char ch in text.ToCharArray())
            {
                a = WorkToken(a + ch, _salt1);
            }

            a = WorkToken(a, _salt2);

            if (a < 0)
            {
                a = (a & int.MaxValue) + int.MaxValue + 1;
            }

            a %= 1000000;

            return $"{a}.{a ^ b}";
        }

        /// <summary>
        /// Used by the token calculation algorithm.
        /// </summary>
        private static long WorkToken(long num, string seed)
        {
            for (int i = 0; i < seed.Length - 2; i += 3)
            {
                int d = seed[i + 2];

                if (d >= 'a') // 97
                {
                    d -= 'W'; // 87
                }

                if (seed[i + 1] == '+') // 43
                {
                    num = (num + (num >> d)) & uint.MaxValue;
                }
                else
                {
                    num ^= num << d;
                }
            }
            return num;
        }

        /// <summary>
        /// Symbols used to build regex patterns.
        /// </summary>
        private static class Symbols
        {
            public static IReadOnlyList<string> Abbreviations { get; } = new List<string>()
            {
                "dr", "jr", "mr", "mrs", "ms", "msgr", "prof", "sr", "st"
            };

            public const string PunctuationMarks = "?!？！.,¡()][¿…‥،;:—。，、：\n";

            public const string ToneMarks = "?!？！";

            public const string PeriodAndComma = ".,";
        }
    }
}