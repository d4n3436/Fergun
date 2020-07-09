using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using GoogleTranslateFreeApi;

namespace Fergun.APIs
{
    //https://github.com/Boudewijn26/gTTS-token/blob/master/gtts_token/gtts_token.py
    //https://github.com/mahirgul/GoogleTTS.Net/blob/master/ttsGenerator/Generate.cs
    public static class GoogleTTS
    {
        public const string ApiEndpoint = "https://translate.google.com/translate_tts";
        private const string _salt1 = "+-a^+6";
        private const string _salt2 = "+-3^+b+-f";
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
        /// <param name="speed">The reading speed of the text. From 0.1 to 1.0</param>
        /// <returns>A MP3 file in a byte array.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="language"/> are null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="speed"/> is lower than 0.</exception>
        public static async Task<byte[]> GetTtsAsync(string text, string language = "en", float speed = 1)
            => await _client.GetByteArrayAsync(GetUri(text, language, speed)).ConfigureAwait(false);

        public static async Task<Stream> GetTtsStreamAsync(string text, string language = "en", float speed = 1)
            => await _client.GetStreamAsync(GetUri(text, language, speed)).ConfigureAwait(false);

        /// <summary>
        /// Get the TTS API request URL that would be sent to the TTS API.
        /// </summary>
        /// <param name="text">The text to be read.</param>
        /// <param name="language">The language (IETF language tag) to read the text in.</param>
        /// <param name="speed">The reading speed of the text. From 0.1 to 1.0</param>
        /// <returns>A MP3 file in a byte array.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="language"/> are null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="speed"/> is lower than 0.</exception>
        public static string GetUrl(string text, string language = "en", float speed = 1)
        {
            return GetUri(text, language, speed).AbsoluteUri;
        }

        /// <summary>
        /// Get the TTS API request URI that would be sent to the TTS API.
        /// </summary>
        /// <param name="text">The text to be read.</param>
        /// <param name="language">The language (IETF language tag) to read the text in.</param>
        /// <param name="speed">The reading speed of the text. From 0.1 to 1.0</param>
        /// <returns>A MP3 file in a byte array.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="language"/> are null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="speed"/> is lower than 0.</exception>
        public static Uri GetUri(string text, string language = "en", float speed = 1)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (string.IsNullOrEmpty(language))
            {
                throw new ArgumentNullException(nameof(language));
            }
            if (speed < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be higher than 0.");
            }

            string token = MakeToken(text);

            //string url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={HttpUtility.UrlEncode(text)}&tl={language}&ttsspeed=1&total=1&idx=0&client=tw-ob&textlen={Text.Length}&tk={token}&prev=input";

            string query = "?";
            query += "ie=UTF-8";
            query += $"&q={Uri.EscapeDataString(text)}";
            query += $"&tl={language}";
            query += $"&ttsspeed={speed}";
            query += "&total=1";
            query += "&idx=0";
            query += "&client=tw-ob";
            query += $"&textlen={text.Length}";
            query += $"&tk={token}";

            var builder = new UriBuilder(ApiEndpoint)
            {
                Port = -1,
                Query = query.ToString()
            };

            return builder.Uri;
        }

        // :)
        public static bool IsLanguageSupported(Language language) => GoogleTranslator.IsLanguageSupported(language);

        /// <summary>
        /// Where the magic happens.
        /// </summary>
        private static string MakeToken(string text)
        {
            // hours since epoch
            //int time = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalHours
            //int time = (int)TimeSpan.FromSeconds(DateTimeOffset.Now.ToUnixTimeSeconds()).TotalHours
            long a, b;
            a = b = DateTimeOffset.Now.ToUnixTimeSeconds() / 3600;
            //long stamp = time;
            foreach (char ch in text.ToCharArray())
            {
                a = WorkToken(a + ch, _salt1);
            }

            a = WorkToken(a, _salt2);

            if (a < 0)
            {
                a = (a & int.MaxValue) + int.MaxValue + 1;
            }
                
            a %= 1000000; //0x1E6
            //stamp %= (long)Math.Pow(10.00, 6.00);
            //stamp %= long.Parse(Math.Pow(10.00, 6.00).ToString());

            return $"{a}.{a ^ b}";
        }

        /// <summary>
        /// Where the magic happens.
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
    }
}